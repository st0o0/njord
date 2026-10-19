# AGENTS.md

## Project

njord — Weather intelligence for Home Assistant. A .NET service
(Docker container) on Akka.NET + Akka.Streams: fetches the Open-Meteo API per
location and domain (weather ensemble, air quality, flood; solar via parameter
group, marine planned), runs the data through projection groups (weather alerts,
air quality alerts, flood risk and cross-domain flood-rain alerts),
and serves the results to Home Assistant over gRPC (the ha-njord integration),
with optional MQTT state publishing.

### Architecture guardrails

- **Three zones that only meet in the domain model:** Ingest (Open-Meteo client,
  DTOs, parsing) → Domain (`DomainForecast<T>`, `LocationForecast`, projection
  groups) → Consumers (MQTT state payloads, gRPC streaming). Ingest and consumers
  never reference each other.
- **Streams for data flow, actors for lifecycle; the stable side owns the hubs.**
  Domain data and pipeline events travel through Akka.Streams (stream refs
  between actors); requests, outcomes and scheduling travel as messages.
  **Polling** is one persistent `PollWorker` per location in the `poll-worker`
  shard region (`IPollRegion`; entity id = location name, messages implement
  `IWithLocationId`, routed by `ShardMessageExtractor` in Core; no passivation, no
  remember-entities). On each tick a worker fetches all enabled domains (weather
  ensemble, air quality, flood) in one round. It owns its poll state (`PollWorkerState`: phase, learned
  cycle, next poll, miss/failure counts, last hash; every transition persisted as
  an event, snapshot every 50), its timer and at most one round in flight, and
  reports every start and outcome (`PollWorkerReport`) to `PollManager`
  (`IPollManager`, a non-persistent cluster singleton with BackoffOnFailure). The
  manager derives the target set from the startup config, sends `StartPolling` on
  start and on a changed discovery interval, `StopPolling` to running workers of
  removed targets, restarts terminated workers, forwards `TriggerImmediatePoll`,
  and answers `QueryPollStates`, the poll-cycle summaries/metrics and the poll
  health from the reports.
  **Gateway** (`OpenMeteoGateway`) is request/response: each `PollWorker` asks
  `FetchDomain` (named reply timeout, `FetchTimeouts`) and gets exactly one
  `FetchDomainCompleted` (hash only, no data), `FetchDomainFailed` or
  `FetchDomainRejected` (`QueueFull`, `Budget`, `PipelineUnavailable`,
  `Expired`). Successful data goes into a small branch
  (`Source.Queue<DomainDataReceived>` → the pipeline's `SinkRef<DomainDataReceived>`,
  `RequestForecastSink` handshake); only this branch is re-attached (with backoff)
  when the pipeline terminates or the branch ends.
  **Pipeline** (`ForecastPipeline`) owns both hubs, materialized once per
  incarnation in `PreStart`: `MergeHub<DomainDataReceived>` (one SinkRef per
  producer; a producer ending never ends the hub) →
  `StatefulSelectMany(ProjectionEngine.Process)` → `BroadcastHub<PipelineEvent>`.
  The `ProjectionEngine` keeps the latest and previous `LocationForecast` per
  location inside the stage closure, so a gateway restart never loses it, and runs
  every `IDomainHandler` triggered by the changed domain. The pipeline
  depends on no other actor; an ended analysis graph restarts it (BackoffOnFailure).
  Consumers attach via SourceRef.
- **The entity set is static, derived from config** (locations × domains ×
  parameters) — never from what the API happens to return. A missing value is
  omitted or `null`, not a missing entity.
- **`TimeProvider` everywhere**, never `DateTime.Now`/`UtcNow` directly.
- **Analysis is a domain handler.** Each domain project (`Njord.Weather`, `Njord.AirQuality`,
  `Njord.Flood`) implements `IDomainHandler` (`Njord.Core/Domain`): it names the domains that
  trigger it and handles the current `LocationForecast` against the previous one, returning
  `PipelineEvent`s directly (`AlertRaised`). `PipelineEvent` variants are typed records
  (`WeatherDataReceived`, `AirQualityDataReceived`, `FloodDataReceived`,
  `SolarDataReceived`, `MarineDataReceived`, `AlertRaised`) — no
  `object` wrapping. Domain projects register their handlers in their own DI
  extension; each exposes one `Add<Name>Domain` extension (rules as `IAlertRule<T>`, handlers as `IDomainHandler`); `IAlertRule<T>`, `AlertOptions` and `AlertClock` live in `Njord.Core`.
- **Consumers are output adapters via SourceRef.** The MQTT state publisher and
  the API's gRPC consumer (`GrpcConsumer` in `Njord.Grpc`, marker
  `IGrpcConsumer`) request a `SourceRef<PipelineEvent>` from `ForecastPipeline`
  and run their own sinks. No intermediate relay actors, no presenters, no discovery
  payloads. Their base `StreamConsumerActor` attaches when the consumer starts (request
  id, 10 s reply timeout, capped backoff), watches the pipeline incarnation that
  answered and re-attaches only when that incarnation terminates or the consumer's own
  graph completes or fails (Warning, `njord_stream_consumer_rewires_total`); there is no
  waiting phase or stash, so queries are answered at any time. No consumer slows the
  pipeline hub: the gRPC consumer builds its streaming hub (`Source.ActorRef` →
  `BroadcastHub` with a permanent `Sink.Ignore` drain) once at start, keeps its latest
  values as actor state and its hub across a re-attach, and gives each gRPC streaming
  client a bounded drop-oldest buffer (256 events); the MQTT publisher runs events →
  `Buffer(256, DropHead)` → payloads → `Sink.ForEachAsync(1, dedup + publish)`, so a slow
  broker drops its own oldest events, and republishes every topic once per new
  subscription and after every broker reconnect.
- **The gRPC API is an adapter.** `Njord.Grpc` holds `GrpcConsumer` (the pipeline's
  streaming output consumer for gRPC clients, marker `IGrpcConsumer`) and reads no
  options: every RPC resolves an actor marker, asks with a message from
  `Njord.Messages` through `ActorAsk` (`Unavailable` when the actor is not resolvable,
  `DeadlineExceeded` on timeout) and maps the result with the per-domain `ToProto()`
  extensions in `Njord.Grpc/Mapping/`. The catalog, the effective request budget and the enabled enrichments are
  fixed at startup and injected as the `ServiceInfo` contract (built by the host from the options);
  budget status goes through `BudgetTracker` (`QueryBudgetStatus`).
- **`Njord.Messages` is the Akka-free contract project.** Every public command, query,
  result and event lives there, one file per command or query, in domain folders
  (`Alerts/`, `Budget/`, `Config/`, `Forecast/`, `Pipeline/`; nothing MQTT-specific, see the MQTT boundary rule).
  Records that carry Akka types (stream handshakes with `ISourceRef`) live in
  `Njord.Core/Actors/*StreamMessages.cs`; actor-internal records stay private.
- **MQTT-specific code lives only in `Njord.Mqtt`.** Any type with `Mqtt` in its name or
  namespace (actors, their messages, transport, topics, health check, telemetry, the payload
  records) is declared there; the egress stays an optional adapter that can change without touching
  domain projects. Allow-list in `Njord.Core`, because the host options and the actor registry
  must name MQTT: `MqttOptions`, `MqttOptionsValidator`, `IMqttConnectionActor`, `IMqttStatePublisher`.
  Enforced by `MqttBoundarySpec`.
- **Zone rules are enforced by tests** in `src/Njord.Architecture.Tests/` (lateral
  independence of the feature libraries, Domain independence,
  sealed/`Spec` conventions,
  `LayerReferenceSpec` for the assembly reference direction: Model and
  Persistence reference no Njord assembly, Messages only Model (and no Akka package,
  no `*Analyzer`/`*Rule`/`*Formulas` type), Core only Model, Messages and Persistence,
  feature libraries (OpenMeteo, Budget, Grpc, Mqtt, Pipeline, Weather, AirQuality, Flood) only Core and below;
  the host references all. Feature libraries
  never reference each other, checked by the lateral rule in `ZoneArchitectureSpec`,
  which also keeps actors, options, the config writer, `BudgetCalculator` and
  out of `Njord.Grpc` and public nested records out of every actor).

### Decisions

- Open-Meteo free tier (non-commercial) is the assumed plan: the request budget
  defaults to its soft limits (300k/month, 600/min) with an optional
  `Polling:BudgetOverride` for self-throttling. Startup validates projected usage
  against 80 % of the resolved monthly budget. Commercial use would need the
  paid tier (out of scope).
- Poll interval and request budget are config values (default 60 min). Any change
  that adds or alters polling needs a budget estimate against the free-tier soft
  limits (300k/month, 10k/day) — and politeness toward a free service.
- gRPC closed sets are proto enums with `*_UNSPECIFIED = 0` (poll phase, alert
  type and severity), mapped by exhaustive switches with a throwing default
  (`WireEnumMappings`). Clients treat unknown enum numbers as `UNSPECIFIED`, so
  adding a member is not a breaking change. MQTT and metric text comes from
  `ToWireName()`, never from `ToString()`.
- Feels-like comes from the API (`apparent_temperature`) — no own Steadman
  computation. Derived values (dew point, ...) are API-native too.
- Weather values come from the Open-Meteo ensemble endpoint: `WeatherPoint` carries
  the ensemble value and `EnsembleStdDev`; there is no own multi-model consensus.
  Solar radiation variables (shortwave, direct, diffuse, DNI, sunshine duration) are
  included when the `Solar` parameter group is enabled.
- Air quality data comes from `air-quality-api.open-meteo.com` (separate endpoint,
  +1 API call per cycle). Provides PM2.5, PM10, ozone, European AQI, and pollen
  types (birch, grass, alder, mugwort, ragweed). Enrichments: PollenAlert, Pm25Alert.
- Flood data comes from `flood-api.open-meteo.com` (separate endpoint, daily
  resolution, +1 API call per cycle). Provides river discharge forecast with
  percentiles (p25/p75) and historical statistics. GloFAS model, auto-selects the
  largest river within 5 km. Enrichments: FloodRiskAlert (discharge vs percentiles),
  FloodRainAlert (cross-domain: heavy rain from Weather + rising discharge from Flood).
- MQTT egress (optional, `Mqtt:Enabled`): state only, no MQTT Discovery. One
  retained flat state JSON per location and domain per cycle, values the API does not
  deliver are omitted (no `unavailable`, no `expire_after`); availability only via the
  retained `online`/`offline` service status topic (LWT). MQTTnet sits behind the
  `IMqttTransport` seam; the connection actor owns the connection lifecycle.
- MQTT payloads are typed records (`Njord.Mqtt/Payloads`), built by mappers in
  `Mapping/` and written by one source-generated serializer context
  (`MqttPayloadJsonContext`, converters in `Serialization/`); no `JsonObject`.
  Null rule: domain data payloads omit absent values; alert payloads write a known
  member without a value as explicit `null` (alerts always carry `peak_value`,
  `hours_until`, `duration_hours`). `soil_frozen` (ice alert) is a JSON boolean.

## Language

All code, specs, docs, and communication in English.

## Solution structure

```
src/
  Njord.slnx

  # /Model/ — vocabulary types (zero Njord dependencies)
  Njord.Model/                # Location, DomainKind, TimeResolution, DomainForecast<T>, WeatherPoint,
                              #   AirQualityPoint, FloodPoint, SolarPoint, MarinePoint, Horizon (no Njord references)

  # /Contracts/ — message contracts
  Njord.Messages/             # Commands, queries, results, PipelineEvent and the alert contracts
                              #   (result records, enums, settings); no Akka (-> Model)
    Alerts/ Budget/ Config/ Forecast/ Pipeline/

  # /Infrastructure/ — persistence and framework plumbing
  Njord.Persistence/          # Persistence DTOs (extend-only; no Njord references): Events/Budget, Events/Polling
  Njord.Core/                 # Options, actor keys, StreamSupervision (-> Model, Messages, Persistence)
    Actors/                   # Marker interfaces, ShardMessageExtractor, StreamConsumerActor, stream handshakes
                              #   (PipelineStreamMessages, ForecastStreamMessages, GrpcStreamMessages)
    Domain/                   # LocationForecast, DomainDataReceived, IDomainHandler, AlertClock,
                              #   ForecastQueries
    Configuration/            # Cross-cutting options and validators (scope, polling, OpenMeteo, storage,
                              #   persistence, MQTT, LocationOptions), budget calculator,
                              #   ConfigView (scope + polling), AddNjordOptions,
                              #   ParameterOptions.Resolve(), PollingOptions.ToBudgetAllowance();
                              #   no analysis feature options

  # /Ingest/ — data acquisition (each -> Core only)
  Njord.OpenMeteo/            # OpenMeteoGateway, OpenMeteoClient (HTTP, status handling, outcomes), DTOs,
                              #   JSON source generator
    Mapping/                  # OpenMeteoEnsembleMappings (ToWeatherForecast), OpenMeteoAirQualityMappings,
                              #   OpenMeteoFloodMappings, OpenMeteoRequestMappings (ToRequestUri, ToFloodRequestUri)
  Njord.Budget/               # BudgetTracker persistent actor, usage accounting
  Njord.Pipeline/             # PollManager, PollWorker (+ PollWorkerState), ForecastPipeline, ProjectionEngine,
                              #   PipelineEvent BroadcastHub
    Configuration/            # PipelineSetupContainer
  Njord.Mqtt/                 # Everything MQTT-specific (boundary rule below); namespaces follow folders
    Actors/                   # MqttConnection, MqttStatePublisher, internal actor messages
    Topics/                   # TopicScheme, TopicSlug
    Mapping/                  # PipelineEventMqttMappings (ToMqttMessages with MqttPublishContext,
                              #   ToLocationName), payload mappings (Domain, Analysis)
    Health/                   # MqttConnectionHealthCheck (registered by MqttSetupContainer)
    Transport/                # IMqttConnection, IMqttTransport, MQTTnet publisher
    Configuration/            # MqttSetupContainer
                              #   (root: StatePayloadBuilder, MqttMessage, MqttEgressTuning, Telemetry)
  Njord.Grpc/                 # gRPC services (ask and map), ActorAsk, protos,
                              #   GrpcConsumer (pipeline output consumer for gRPC clients)
    Mapping/                  # One <Domain>ProtoMappings class per domain (Alert, DomainData, Ops, Catalog),
                              #   EnrichmentEventMappings, WireEnumMappings: extension methods only

  # /Analysis/ — projection-group libraries (each -> Core only), own their computation
  Njord.Weather/              # WeatherAlertHandler (IDomainHandler),
                              #   Rules/ (one rule per AlertType), AlertWindow
  Njord.AirQuality/           # AirQualityAlertHandler, PollenAlertRule, Pm25AlertRule
  Njord.Flood/                # FloodRiskHandler, FloodRiskAlertRule, FloodRainHandler,
                              #   FloodRainAlertRule (cross-domain Weather x Flood)

  # Host
  Njord/                      # Service host: Program.cs, DI, actors, streams (-> all above)
    Configuration/            # Host setup, ServiceInfoMappings (options to the ServiceInfo contract);
                              #   containers in Program.cs order: CoreSetupContainer (TimeProvider, meter
                              #   allow-list), ConfigSetupContainer (options, ConfigView, ServiceInfo),
                              #   HealthSetupContainer (health options, journal check), then the feature containers
    Health/

  # /Tests/ — one test project per production project
  Njord.Model.Tests/          # Vocabulary type specs (Location, DomainKind, Horizon, etc.)
  Njord.Persistence.Tests/    # Persistence roundtrip and wire-format specs (Verify for the poll worker records)
  Njord.Messages.Tests/       # Alert contract specs (enums, thresholds, details)
  Njord.Core.Tests/           # Configuration, StreamConsumerActor
  Njord.OpenMeteo.Tests/      # OpenMeteoClient, gateway, OptionsBudgetProvider specs
  Njord.Budget.Tests/         # BudgetTracker specs
  Njord.Pipeline.Tests/       # PollWorker, PollManager, polling cluster, ForecastPipeline, ProjectionEngine specs
  Njord.Mqtt.Tests/           # MQTT connection, state payload, consumer specs
  Njord.Grpc.Tests/           # gRPC service, ActorAsk, mapping specs, golden masters,
                              #   GrpcConsumer specs
  Njord.Weather.Tests/        # WeatherAlertHandler, domain registration specs, Rules/ (one spec per rule)
  Njord.Tests/                # Host specs: options binding, setup containers
  Njord.IntegrationTests/     # Whole host via NjordHost + TestServer (gRPC channel, SQLite restart); Mosquitto via Testcontainers (needs Docker)
  Njord.Architecture.Tests/   # ArchUnit zone/layer/convention rules
  Njord.Tests.Shared/         # Shared fakes, fixtures, helpers (not a test project)
```

Reference direction: Model <- Messages; Model/Messages/Persistence <- Core <- feature libs
(OpenMeteo, Budget, Grpc, Pipeline, Mqtt, Weather, AirQuality, Flood) <- host. Feature libs reference only
Njord.Core (and below, including Model transitively), never each other and never the host
they reach each other's actors
through the marker interfaces in `Njord.Core/ActorKeys.cs`. Registration
stays central in the host. Enforced by project references plus `Njord.Architecture.Tests/LayerReferenceSpec.cs` and the
lateral/upward ArchUnit rules in `Njord.Architecture.Tests/ZoneArchitectureSpec.cs`.

## Build & test

All commands run from `src/` (where `global.json` lives):

```powershell
dotnet build Njord.slnx
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj                          # one test project (no Docker)
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj -- -class "<FullyQualifiedName>"   # single class
```

Every `Njord.*Tests` project is its own executable; run all of them with a loop
(a new test project is picked up automatically, `Njord.Tests.Shared` is not a test project):

```bash
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build   # build first: dotnet build Njord.slnx
done
```

Current total: 1279 tests (Model 90, Persistence 16, Messages 87, Core 129, Grpc 90, Pipeline 146,
Mqtt 137, OpenMeteo 65, Budget 34, Architecture 100, Alerts 218, host `Njord.Tests` 72, IntegrationTests 15).
Note: Alerts grew from 173 to 218 with air quality (Pollen, PM2.5) and flood (FloodRisk, FloodRain) rules.
The count is one test per `[Fact]` and one per data row of a `[Theory]`.
CI's `dotnet test --solution Njord.slnx` runs every test project of the solution.
Each project is its own process with its own thread pool: running many at once on a small
runner can slow the load-sensitive actor specs, so prefer the sequential loop above and
limit parallel test modules (`--max-parallel-test-modules`) if flakes appear.

Tests are xUnit v3 on Microsoft.Testing.Platform — `dotnet run`, **not** `dotnet test`.
Shared test infrastructure (fixtures, fakes, helpers) lives in `Njord.Tests.Shared`.

End-to-end verification of the full stack (njord + ha-njord + Home Assistant,
via Docker Compose + browser automation) is the `e2e-test` skill, not a
`dotnet test` project — see `.claude/skills/e2e-test/SKILL.md` and
`e2e/E2E-TEST-PLAN.md`.

Run the service itself from `src/Njord/` (`dotnet run`). Configuration layers:
- `appsettings.json` — logging and Kestrel only (no `Njord` section; all option defaults
  live as property initializers on the options classes). List-valued defaults (`Horizons`,
  `Parameters.Groups`, threshold arrays) and structural initializers (dictionaries with a
  comparer, nested `new()`) also live on the property; `ReplaceCollectionDefaults` makes a
  configured list replace its code default. A new option gets its default as a property
  initializer, not in `appsettings.json`.
- `appsettings.Development.json` — dev overrides (locations, models, MQTT
  disabled). Loaded automatically by `dotnet run` (`ASPNETCORE_ENVIRONMENT=Development`).
- Environment variables — Docker-compose / `docker run` (`Njord__Mqtt__Host`, etc.).
- `appsettings.Example.json` — copy-in template with deployment-specific keys only.
- `docker-compose.example.yml` — the commented variables show each option's default; a spec
  (`ComposeExampleSpec`) fails when one differs from the bound default (property initializers
  for scalars, code defaults for lists). A comment containing "Alternative" starts a block of
  values that are not defaults and are skipped until the next blank line.

Sections: one options class per concern, each with its own `SectionName` (no two share one,
`OptionsAccessArchitectureSpec`): `Njord` (forecast scope: `Locations`, `Models`, `Horizons`,
`ForecastDays`, `Parameters`), `Njord:Polling`, `Njord:OpenMeteo`, `Njord:Storage`,
`Njord:Persistence`, `Njord:Health`, `Njord:Mqtt`, `Njord:Enrichment:Alerts`. Every
options type is registered with `AddNjordOptions<T>(configuration)` (`Njord.Core`: binds
the section, lets a configured list replace its default, registers `<T>Validator` from
`T`'s namespace, `ValidateOnStart`); setup containers do not repeat those steps.

Configuration is static: it is read once at startup and a change needs a restart. Options are read
through `IOptions<T>` or `ConfigView` (scope and polling), never `IOptionsMonitor<T>`
(`OptionsAccessArchitectureSpec`, constructor parameters; `ConfigurationArchitectureSpec` for actors and fields).
Setup containers resolve options from the service provider and are outside the rule.

All option defaults live as property initializers; `new XOptions()` yields correct defaults.
Tests that need list-default replacement still use `AppSettingsDefaults` (`Njord.Tests.Shared`,
binds `appsettings.json` with `ReplaceCollectionDefaults`). `DefaultOptionsSpec` validates
defaults with every validator.

MQTT is disabled by default (`Mqtt:Enabled = false`). Enable explicitly with
`Njord__Mqtt__Enabled=true` + `Njord__Mqtt__Host=...`.

`DisabledTestArchitectureSpec` (`src/Njord.Architecture.Tests`) guards against
skipped or ignored tests (`[Fact(Skip = ...)]`, `[Theory(Skip = ...)]`, `[Ignore]`)
across all `*Spec.cs` test files.

Mutation testing (Stryker.NET 5, MTP runner, per-test coverage; pure-logic projects only: Model,
Alerts and parts of Core): `bash scripts/run-mutation.sh <Project>`
from the repo root (runs in the .NET SDK container; `--local` where the SDK's MSBuild resolves, as in CI).
Config in `src/stryker-config.json`, tool pinned in `.config/dotnet-tools.json`, weekly and manual run in
`.github/workflows/mutation.yml` (report only, never a required check). Scores and exclusions:
`docs/development/mutation-testing.md`.

OpenSpec checks (run from the repo root):

```bash
openspec validate --all --no-interactive
bash scripts/check-openspec-root.sh    # fails if an openspec/ root exists outside <repo root>/openspec
```

These checks run locally only; they are deliberately not part of CI.

## Open-Meteo API (verified 2026-07-11 via live probes)

- Endpoint `GET https://api.open-meteo.com/v1/forecast?latitude=&longitude=` —
  **no API key, no auth header**. Free tier is non-commercial; soft limits
  600/min, 5k/h, 10k/day, 300k/month. Calls are weighted: >10 hourly variables
  or >2 weeks of data count fractionally as multiple calls (njord requests
  exactly 9 variables × 4 days = weight 1.0).
- Single-model requests (`&models=icon_d2`) return flat arrays under `hourly`
  with **unsuffixed** variable names + an `hourly_units` object; multi-model
  requests suffix every variable with the model id.
- Wind defaults to km/h — always send `wind_speed_unit=ms`. Times default to
  naive ISO strings — always send `timeformat=unixtime` (epoch seconds). Always
  send `timezone=UTC` so all timestamps (hourly, daily, sunrise/sunset) align to
  midnight UTC; the `timezone` field in the response is ignored. Using
  `timezone=auto` would shift timestamps to midnight local time even with
  `timeformat=unixtime`, causing off-by-one day errors for UTC+ timezones.
- Values beyond a model's horizon are `null` entries (e.g. `icon_d2` ends
  ~+48–64 h). A single requested model outside its geographic coverage →
  HTTP 400 `{"error":true,"reason":"No data is available for this location"}`;
  invalid model ids → HTTP 400 with an "invalid String value" reason.
- Model ids verified working: `icon_d2`, `icon_eu`, `icon_global`,
  `ecmwf_ifs025`, `gfs_seamless`, `ukmo_global_deterministic_10km`,
  `meteoswiss_icon_ch1`, `meteoswiss_icon_ch2` (~1 km, Alps coverage — in
  range for the default Lucerne location). No warnings endpoint.
- Air Quality API: `GET https://air-quality-api.open-meteo.com/v1/air-quality?latitude=&longitude=`
  — same auth/rate-limit as forecast. Hourly resolution, 5–7 days. Variables:
  `pm2_5`, `pm10`, `ozone`, `nitrogen_dioxide`, `european_aqi`, `alder_pollen`,
  `birch_pollen`, `grass_pollen`, `mugwort_pollen`, `ragweed_pollen`. CAMS model
  (European 11 km / Global 45 km, auto-selected).
- Flood API: `GET https://flood-api.open-meteo.com/v1/flood?latitude=&longitude=`
  — **daily** resolution, up to 210 days. Variables: `river_discharge`,
  `river_discharge_mean`, `river_discharge_median`, `river_discharge_max`,
  `river_discharge_min`, `river_discharge_p25`, `river_discharge_p75`. GloFAS v4
  (5 km), returns the largest river within 5 km. Response uses `daily` (not `hourly`).

## Conventions

- OpenSpec lives only in `<repo root>/openspec`. Never delete `openspec/changes/archive`
  (it is tracked, no longer gitignored).
- **Git: NEVER `git push`** — the user pushes. Commit messages are Conventional
  Commits (commitlint-enforced).
- Versioning is release-please. Never edit `<Version>` in
  `src/Directory.Build.props` by hand.
- Central package management with transitive pinning: versions live only in
  `src/Directory.Packages.props`; add packages via `dotnet add package`, never
  edit csproj XML for versions.
- Tests: `Spec` suffix, `sealed` classes, BDD-style method names. Async and
  actor tests carry an explicit `[Fact(Timeout = ...)]` and pass
  `TestContext.Current.CancellationToken` to awaited calls (xUnit1069 is an
  error). The xUnit timeout is an outer safety net and must exceed every Akka
  wait it wraps: TestKit waits dilate by `akka.test.timefactor = 3`
  (`TestTimeouts.TimeFactor`), and the dilation applies to explicit durations
  too, not just the defaults (3 s default becomes 9 s, an explicit 5 s becomes
  15 s). Specs that start a hosted actor system or host use
  `TestTimeouts.Hosted` (`Njord.Tests.Shared`, 30 s) and bound `AwaitAssert` /
  `AwaitCondition` / `ExpectMsg` explicitly with `TestTimeouts.AwaitAssertMax`,
  an un-dilated 5 s that TestKit stretches to 15 s; waits TestKit does not
  dilate (`Task.WaitAsync`, manual deadlines) use `TestTimeouts.RealTimeWaitMax`
  (15 s). Never pass a raw `TimeSpan` whose dilated value comes within 10 s of
  the xUnit timeout. `TestTimeoutsSpec` (`Njord.Core.Tests`) enforces this
  margin for the shared constants. Plain
  `Timeout = 5000` is only for specs that make no dilated Akka wait. Never
  reuse an actor name after `GracefulStop` (name release lags `Terminated`):
  use a unique name. Retry-backoff specs pass a short `BackoffPolicy` (`Backoff.Create(50 ms, maxDelay: 400 ms)`) instead of
  waiting real seconds. Pure synchronous specs use a plain `[Fact]`, a timeout
  cannot be enforced there.
- C#: records for messages/DTOs, value objects in the domain, `sealed` by
  default, nullable enabled.
- Enum to wire text or to another enum is an extension method with an explicit
  `switch` expression, one arm per member and a default that throws
  `ArgumentOutOfRangeException` (`ToWireName()`, `ToTopicSegment()`, `ToProto()`). No casts
  between enum types, `Enum.Parse`/`TryParse` or `ToString().ToLower*()` for wire values;
  `EnumConversionArchitectureSpec` scans production code, no allow-list entries.
  Each conversion has a spec pinning every member.
- Mappings (domain to proto, request to command, options to message) are public extension methods in
  `internal static` classes named `<Domain><Target>Mappings`, one per domain; no state, no options, no
  registry lookups, never inside actors or services. Names follow the direction: `ToProto`, `ToCommand`,
  `ToDomain`, `ToMessage`, never `Map*`. `DateTimeOffset` to protobuf uses Google.Protobuf's
  `ToTimestamp()`. `ServiceInfoMappings` (host) stays `public`
  because the test projects consume them without `InternalsVisibleTo`. `MappingConventionArchitectureSpec`
  enforces this; each mapping class has a spec pinning every field. Mapping logic
  (event-to-MQTT, Open-Meteo series/URI and log-level conversions) lives in
  `Mapping/` extensions that take settings as arguments or a small context record
  (`MqttPublishContext`).
- **"Snapshot" is reserved for persisted actor state.** No production type name contains
  `Snapshot`; the persisted state of a persistent actor is `Persisted<Name>State`, and the Akka
  members (`SaveSnapshot`, `SnapshotOffer`, `SaveSnapshotSuccess`, `SnapshotInterval`) keep their
  names. Contract types are named for what they are: `LocationForecast`, `DomainForecast<T>`,
  `ServiceInfo`, `MqttConnectionStatus` / `PollHealth` /
  `BudgetUsage`. `NamingArchitectureSpec` enforces the `Snapshot` reservation.
- Enrichment wire names come from `EnrichmentNames` (`Njord.Messages.Pipeline`), never bare
  strings. `AlertRaised` is the enrichment `PipelineEvent` variant.
- `ParameterRegistry` (`Njord.Model`): one `static readonly ParameterDef` field per
  parameter (declared once, registered into the catalog; daily variants of a shared API name
  are `DailyWeatherCode`, `DailySunshineDuration`, `DailyEt0FaoEvapotranspiration`). Use the
  fields (`ParameterRegistry.Rain`, `.PrecipitationSum`, etc.) in production code.
  `GetByApiName` is for deserialization only — never call it with a string literal.
  `ParameterDef` is identity only (API name, unit, group, granularity, value type): the MQTT
  payload key is `ToMqttKey()` (`Njord.Mqtt/Mapping/ParameterMqttKeys`), the configuration
  parsing is `ParameterSelection` (`Njord.Core/Configuration`, via `ParameterOptions.Resolve()`).
  A new parameter needs a field, a key in `ParameterMqttKeys` and an updated catalog count in
  `ParameterCatalogSpec`.
- Persistence records (`Njord.Persistence`): follow FunkArr conventions
  (`akka-skills:persistence`). Positional records (no `[JsonProperty]`,
  no init-properties). `DateTimeOffset` for timestamps, `TimeSpan` for
  durations — never `long` ticks. No `Dto`/`Event` suffix — use past-tense
  for events (`SomethingHappened`), `Persisted<Name>State` for snapshots.
  New properties must be nullable or have a default. While the project is 0.x,
  breaking persistence changes are allowed (`refactor!:`).
  Closed sets in a persisted record are `Persisted*` enums with explicit numeric
  values (`PersistedPollPhase`); `Njord.Persistence` types reference only `System`
  and `Njord.Persistence` types (`PersistenceBoundarySpec`). Mapping between state
  and persisted records lives in `<Owner>PersistenceMappings` (`GetPersistenceState`,
  `FromPersistence`, `FromPersistedEvent`, `ToPersistence()`/`ToDomain()` for
  enums) in the owning project, never in the state file; state to message contract
  (`ToPollStateEntry()`, `ToBudgetUsage()`) lives in `<Owner>MessageMappings`.
- Persistent actors use `LastSequenceNr % SnapshotInterval == 0` for snapshot
  scheduling (FunkArr pattern). No counters on domain state records.
- Journal cleanup after snapshot: `DeleteMessages(seqNr)` +
  `DeleteSnapshots(seqNr - 1)` in `SaveSnapshotSuccess` handler.
- `Apply` methods on state take the event record directly (e.g.
  `Apply(ApiCallRecorded evt, ...)`), not decomposed primitive values.
- Health is feature-owned (FunkArr shape): `PollHealthState` (Pipeline), `MqttConnectionState` (Mqtt)
  and `BudgetUsageState` (Budget) are written only by their actor, as one atomic swap of an immutable
  record (`PollHealth`, `MqttConnectionStatus`, `BudgetUsage`), and read only by the feature's own check.
  Each check is a sealed `IHealthCheck` over an `internal static Evaluate(...)` that takes plain values
  and is registered by the owning container with an explicit `failureStatus` (`pipeline`, `budget`,
  `mqtt-connection`; the host adds `journal`). Checks never ask actors. Thresholds
  are `Njord:Health` options (`PollOverdueFactor`, `PollStalledFactor`, `MqttUnhealthyAfter`,
  `BudgetDegradedBelowPercent`). `/healthz` answers 200 for Healthy and
  Degraded and 503 for Unhealthy within a 5 s request timeout; `/alive` is unconditional.

## C# conventions

- Style (namespaces, `using` order, `_camelCase` fields, braces, `var`) is
  enforced by `src/.editorconfig` — do not restate it here.
- No XML docs. Code speaks through naming.
- Never use `async void`, `.Result`, or `.Wait()`.
- Always pass `CancellationToken` through async call chains.

## Akka conventions

Rules apply to production code (`src/Njord/`).

- **No `ContinueWith`** in actor code. Use `PipeTo` with `success:` and
  `failure:` mappers.
- **No `Status.Failure`/`Status.Success`.** Use project-owned
  `XxxFailed(Exception Cause)` records (plus the `RequestId` when answering a
  correlated request); in `PipeTo` always provide
  `failure: ex => new XxxFailed(ex)`. A failure message must be handled by its
  requester, never left to the stash or dead letters.
- **No `EventStream`** in production code. Use explicit actor references.
  TestKit probes may subscribe to `DeadLetter`/`Warning` in tests.
- **No `IActorRef` constructor parameters** in production actors.
- **Actor naming:** domain-role names (e.g. `PollManager`, `PollWorker`, `ForecastPipeline`), sealed.
  `*State` suffix is reserved for immutable state records, never for actors.
- **Actor logging:** `private readonly ILoggingAdapter _log = Context.GetLogger();`
  — inline field init, `readonly`, named `_log`. Never `null!` + PreStart.
  The host's `ActorCategoryLogger` (registered in `AkkaSetupContainer.ConfigureLogging`) writes
  each event with the logging class's full type name as category (`Njord.Pipeline.PollWorker`,
  Akka internals keep `Akka.*`); `akka.loglevel` follows the lowest `Serilog:MinimumLevel` value.
- **Actors are resolved by marker interfaces** (`IPollManager`, `IPollRegion`,
  `IMqttConnectionActor`, ... in `Njord.Core/ActorKeys.cs`), registered
  in the host (`AkkaSetupContainer`); never key a registry lookup by the actor
  class.

## Test assertion conventions

- Prefer `Assert.NotNull(value)` before accessing nullable members over the
  null-forgiving operator (`!.`). Exception: `JsonNode` indexers
  (`payload["x"]!.GetValue<int>()`).
- `var item = Assert.Single(collection)` instead of `collection[0]`.
- Assert the count (`Assert.Equal(2, list.Count)`) before indexing several
  elements.
- **No vacuous assertion after an already-proven value**: once a value's
  type/non-null state is already proven (e.g. by `ExpectMsg<T>()`), the
  following assertion must inspect actual content (property values, message
  text, counts), not just `Assert.NotNull(result)` again. A `Assert.NotNull`
  used as a genuine null guard before further property assertions is not
  vacuous.
- **Endpoint-registration tests assert real behavior**: a test for a mapped
  Minimal API group must send a request to at least one mapped route and
  assert on the response (status code and/or body), not only that
  `IEndpointRouteBuilder.DataSources` is non-empty.
- **Assert the exact computed value, not just existence.** When behavior
  produces a concrete value (a string, a count, a computed field), assert on
  that value directly instead of settling for "it's not null" or "it's not
  empty."
- **Exceptions: capture and inspect, not just the type.**
  `var ex = Assert.Throws<T>(...)` / `await Assert.ThrowsAsync<T>(...)`, then
  assert on `ex`'s actual fields (`ex.ParamName`, `ex.Message`, custom
  properties) — not just the exception type.
- **Mock-call assertions specify exact args and count** —
  `mock.Received(1).Method(Arg.Is<T>(x => ...))`, not just "was called" /
  "didn't throw."
- **NSubstitute for plain-interface mocking, when actually needed.** Not
  currently a dependency here - no project collaborator has needed a
  hand-rolled stub class yet (actor collaborators go through
  `TestProbe`/real actors, never mocked). Reach for
  `Substitute.For<T>()` + `sub.Member(...).Returns(...)` the first time a
  non-actor interface needs faking, instead of writing a new stub class by
  hand (see FunkArr's `IDataFiles`/`IHttpClientFactory` tests for the
  pattern). A stateful shared fake with its own small, purpose-built API
  (like `Njord.Tests.Shared.FakeOpenMeteoClient`) is still the better choice
  over NSubstitute when many test files need the same simple toggle
  behavior.
- **`Assert.IsType<T>(obj, exactMatch: false)`** to prove interface/base-type
  conformance instead of an unchecked cast.
- **No `Thread.Sleep`/arbitrary delay for async or eventually-consistent
  state.** Use a bounded wait with a descriptive timeout failure instead.
  Akka actor tests already get this for free via `TestProbe.ExpectMsg`/
  `AwaitAssert`.
- **`Assert.Multiple`** for grouped assertions on the same object/outcome, so
  one run reports every failing assertion instead of stopping at the first.
- **`Assert.Equal(expected, actual)` over `Assert.True(actual == expected)`**
  — better failure messages.
- Prefer `Theory`/`InlineData`/`MemberData` for small input/output sweeps;
  prefer separate named `[Fact]`s when each case needs a large distinct
  fixture (e.g. an embedded JSON payload) — a readable failure name beats
  DRY in that case.

### Enforcement

Machine-checked by `TestSourceConventionArchitectureSpec` (scans every `*Spec.cs` of the test projects, comments and
literals ignored; allow-list entries carry a reason and fail when stale):

- an `async` `[Fact]`/`[Theory]` carries an explicit `Timeout`;
- no `Thread.Sleep`, no `Task.Delay` with a literal duration;
- no `DateTime.UtcNow`/`Now`, `DateTimeOffset.UtcNow`/`Now` (use `FakeTimeProvider` or a fixed timestamp);
- no single-argument `Assert.True(a == b)` / `Assert.False(a != b)` (xUnit has no message overload for `Assert.Equal`,
  so a call that passes a message is a deliberate `Assert.True`);
- no `!.` except on an indexer result or `JsonNode.Parse(...)`; reflection helpers in `Njord.Architecture.Tests` are exempt.

Machine-checked elsewhere: sealed `Spec` classes (`ConventionArchitectureSpec`), no skipped or ignored tests
(`DisabledTestArchitectureSpec`), the timeout margin of the shared constants (`TestTimeoutsSpec`), CancellationToken
passing (`xUnit1069` compiler error), and that the files, specs and methods named in this document and in the project skills
exist (`DocumentationArchitectureSpec`).

Review-only: no vacuous assertion, assert the exact value, inspect captured exceptions, exact args in mock assertions,
real behavior in endpoint tests, `Assert.Multiple` for grouped assertions, count before indexing and `Assert.Single`,
`Assert.IsType(exactMatch: false)`, `Theory` versus separate `Fact`s, and when to reach for NSubstitute.

## Metrics conventions

- One `Meter("Njord.<Domain>")` per feature library (OpenMeteo, Budget, Pipeline,
  Mqtt; plus `Njord.Core` for its shared base classes), owned by an `internal static class Telemetry` in that project with
  `internal static readonly` instrument fields (`Telemetry.MqttDedup.Add(...)`).
  Each `Telemetry` exposes `MeterName`; `CoreSetupContainer.NjordMeters` is built from these constants
  (hence `InternalsVisibleTo Njord` on those libraries), so a new meter is added to that list.
  `BudgetSetupContainer` registers the budget gauges (hosted service over `BudgetUsageState` and the effective budget from `ConfigView`).
- Names are `njord_<area>_<name>` in snake_case; counters end in `_total`,
  time histograms in `_seconds`. Keep tag cardinality bounded.
- Metrics are exported via prometheus-net.

## References

- Open-Meteo API: https://open-meteo.com/en/docs (endpoint: `https://api.open-meteo.com/v1/forecast`)
