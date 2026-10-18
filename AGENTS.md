# AGENTS.md

## Project

njord — Multi-model weather intelligence for Home Assistant. A .NET service
(Docker container) on Akka.NET + Akka.Streams: polls the Open-Meteo API for
multiple weather models per location, processes forecasts through an enrichment
pipeline (consensus, alerts, derived values, trends, indices),
and serves the results to Home Assistant over gRPC (the ha-njord integration),
with optional MQTT state publishing.

### Architecture guardrails

- **Three zones that only meet in the domain model:** Ingest (Open-Meteo client,
  DTOs, parsing) → Domain (`Forecast`, analysis, consensus) → Consumers
  (MQTT state payloads, gRPC streaming). Ingest and consumers never reference
  each other.
- **Streams for data flow, actors for lifecycle; the stable side owns the hubs.**
  Forecast data and pipeline events travel through Akka.Streams (stream refs
  between actors); requests, outcomes and scheduling travel as messages.
  **Polling** is one persistent `PollWorker` per (location, model) target in the
  `poll-worker` shard region (`IPollRegion`; entity id `"{location}|{model}"`,
  messages implement `IWithTargetId`, routed by `ShardMessageExtractor` in Core;
  no passivation, no remember-entities). A worker owns its poll state
  (`PollWorkerState`: phase, learned cycle, next poll, miss/failure counts, last
  hash; every transition persisted as an event, snapshot every 50), its timer
  and at most one request in flight, and reports every start and outcome
  (`PollWorkerReport`) to `PollManager` (`IPollManager`, a non-persistent cluster
  singleton with BackoffOnFailure). The manager derives the target set from the
  startup config, sends `StartPolling` on start and on a changed discovery
  interval, `StopPolling` to running workers of removed targets, restarts
  terminated workers, forwards `TriggerImmediatePoll`, and answers
  `QueryPollStates`, the poll-cycle summaries/metrics and the poll health from
  the reports.
  **Gateway** (`OpenMeteoGateway`) is request/response: each `PollWorker` asks
  `FetchForecast` (named reply timeout, `FetchTimeouts`) and gets exactly one
  `FetchForecastCompleted` (hash only, no data), `FetchForecastFailed` or
  `FetchForecastRejected` (`QueueFull`, `Budget`, `PipelineUnavailable`,
  `Expired`). Its private stream `Source.Queue<FetchTarget>(DropNew)` →
  `GatewayThrottleStage` (token bucket; rejects instead of holding a request
  past its admission window) → `SelectAsyncUnordered(2, fetch)` →
  `Sink.ActorRef(Self)` dies with the actor; an ended stream answers open
  requests and restarts the gateway (BackoffOnFailure). Successful forecasts go
  into a small branch (`Source.Queue<Forecast>` → the pipeline's
  `SinkRef<Forecast>`, `RequestForecastSink` handshake); only this branch is
  re-attached (with backoff) when the pipeline terminates or the branch ends.
  **Pipeline** (`ForecastPipeline`) owns both hubs, materialized once per
  incarnation in `PreStart`: `MergeHub<Forecast>` (one SinkRef per producer; a
  producer ending never ends the hub) → `ZipWith(sensor map, settings)` →
  `StatefulSelectMany(computeAll)` → `BroadcastHub<PipelineEvent>`. It depends
  on no other actor; an ended analysis graph restarts it (BackoffOnFailure).
  Settings (`AnalysisSettingsChanged`) and sensor snapshot maps enter via
  `Source.ActorRef` + `RepeatPrevious` side channels; the **SensorHub pushes**
  `SensorSnapshots` (all locations with fresh readings) on every accepted
  reading and expiry tick. All analysis state (`ModelSnapshot`, previous
  `ConsensusSnapshot`) lives inside the `StatefulSelectMany` closure, so a
  gateway restart never loses it. Consumers attach via SourceRef.
- **The entity set is static, derived from config** (locations × models ×
  parameters × horizons) — never from what the API happens to return. A missing
  value is an `unavailable` state, not a missing entity.
- **Never `Zip` model sub-streams for consensus.** Aggregate per poll cycle
  (cycle id = tick timestamp) with timeout and quorum — consensus from whatever
  arrived, plus `models_used`/spread diagnostics.
- **`TimeProvider` everywhere**, never `DateTime.Now`/`UtcNow` directly.
- **SensorHub for external sensor input.** The SensorHub actor receives readings
  via gRPC (`SensorGrpcService`, stored under the configured location spelling),
  stores latest values per `SensorKind` (closed enum) and pushes a
  `SensorSnapshots` map (every location with fresh readings) to the pipeline on
  each accepted reading and expiry tick. The pipeline uses the latest map's
  entry for the forecast's location (latest-value, no reactive re-computation);
  a missing or stale entry falls back to configured values.
- **Each analysis feature is a self-contained library.** Feature libraries
  (Alerts, Derived, Trends, Indices) define compute logic and DI extensions.
  Each analyzer takes `ConsensusSnapshot` directly (plus per-feature extras:
  `Previous` for Trends, `SensorSnapshot` for Indices). No input interfaces
  or PollContext god-object. `PipelineEvent` variants are typed records
  (`ForecastReceived`, `ConsensusComputed`, `AlertComputed`, `DerivedComputed`,
  `TrendComputed`, `IndexComputed`) — no `object` wrapping. Pipeline
  orchestrates all feature libraries directly (`Njord.Pipeline` references
  analysis libraries).
- **Consumers are output adapters via SourceRef.** The MQTT state publisher and
  the API's read model (`ForecastReadModel` in `Njord.Pipeline`, marker
  `IForecastReadModel`) request a `SourceRef<PipelineEvent>` from `ForecastPipeline`
  and run their own sinks. No intermediate relay actors, no presenters, no discovery
  payloads. Their base `StreamConsumerActor` attaches when the consumer starts (request
  id, 10 s reply timeout, capped backoff), watches the pipeline incarnation that
  answered and re-attaches only when that incarnation terminates or the consumer's own
  graph completes or fails (Warning, `njord_stream_consumer_rewires_total`); there is no
  waiting phase or stash, so queries are answered at any time. No consumer slows the
  pipeline hub: the read model builds its streaming hub (`MergeHub` → `BroadcastHub` with
  a permanent `Sink.Ignore` drain) once at start, keeps its latest values and its hub
  across a re-attach and gives each gRPC streaming client a bounded drop-oldest buffer
  (256 events); the MQTT publisher runs events → `Buffer(256, DropHead)` → payloads →
  `Sink.ForEachAsync(1, dedup + publish)`, so a slow broker drops its own oldest events,
  and republishes every topic once per new subscription. A started (or restarted)
  pipeline asks the `ConfigManager` (`QueryAnalysisSettings`) and the `SensorHub`
  (`RequestSensorSnapshots`) for the current inputs.
- **The gRPC API is a pure adapter.** `Njord.Grpc` holds no actors and reads no
  options: every RPC resolves an actor marker, asks with a message from
  `Njord.Messages` through `ActorAsk` (`Unavailable` when the actor is not resolvable,
  `DeadlineExceeded` on timeout) and maps the result with the per-domain `ToProto()`
  extensions in `Njord.Grpc/Mapping/`. Configuration is read and written through
  `ConfigManager` (`QueryConfig`, `QueryCatalog`, `Update*` commands,
  `RequestConfigStream`), budget status through `BudgetTracker` (`QueryBudgetStatus`).
- **`Njord.Messages` is the Akka-free contract project.** Every public command, query,
  result and event lives there, one file per command or query, in domain folders
  (`Budget/`, `Config/`, `Forecast/`, `Pipeline/`, `Sensors/`; nothing MQTT-specific, see the MQTT boundary rule).
  Records that carry Akka types (stream handshakes with `ISourceRef`) live in
  `Njord.Core/Actors/StreamWiringMessages.cs`; actor-internal records stay private.
- **MQTT-specific code lives only in `Njord.Mqtt`.** Any type with `Mqtt` in its name or
  namespace (actors, their messages, transport, topics, health check, telemetry, later the payload
  records) is declared there; the egress stays an optional adapter that can change without touching
  domain projects. Allow-list in `Njord.Core`, because the host options, the actor registry and the
  health monitor must name MQTT: `MqttOptions`, `MqttOptionsValidator`, the `MqttOptions` entry in
  `ConfigReloadPolicy`, `IMqttConnectionActor`, `IMqttStatePublisher`, `MqttHealthSnapshot`.
  Enforced by `MqttBoundarySpec`.
- **Zone rules are enforced by tests** in `src/Njord.Architecture.Tests/` (lateral
  independence of the feature libraries, Domain independence,
  sealed/`Spec` conventions,
  `LayerReferenceSpec` for the assembly reference direction: Weather, Sensors and
  Persistence reference no Njord assembly, Compute only Weather and Sensors,
  Messages only Weather, Sensors and Compute (and no Akka package), Core only those,
  feature libraries (OpenMeteo, Budget, SensorHub, Grpc, Pipeline, Mqtt,
  Alerts, Derived, Trends, Indices) only Core and below; the host references
  all. Feature libraries never reference each other, checked by the lateral
  rule in `ZoneArchitectureSpec`, which also keeps actors, options, the config
  writer, `BudgetCalculator` and `ModelCoverageRegistry` out of `Njord.Grpc` and
  public nested records out of every actor).

### Decisions

- Open-Meteo free tier (non-commercial) is the assumed plan: the request budget
  defaults to its soft limits (300k/month, 600/min) with an optional
  `BudgetOverride` for self-throttling. Startup validates projected usage
  against 80 % of the resolved monthly budget. Commercial use would need the
  paid tier (out of scope).
- Poll interval and request budget are config values (default 60 min). Any change
  that adds or alters polling needs a budget estimate against the free-tier soft
  limits (300k/month, 10k/day) — and politeness toward a free service.
- gRPC closed sets are proto enums with `*_UNSPECIFIED = 0` (poll phase, trend
  direction, stability, dew point comfort, VPD category, consensus method), mapped
  by exhaustive switches with a throwing default (`WireEnumMappings`). Clients treat
  unknown enum numbers as `UNSPECIFIED`, so adding a member is not a breaking change.
  MQTT and metric text comes from `ToWireName()` / `FetchOutcomeTag`, never from
  `ToString()`.
- Feels-like comes from the API (`apparent_temperature` per model) — no own
  Steadman computation.
- HA device cut: per location, one device per weather model plus one device per
  enabled analysis feature. Analysis features (consensus, alerts, derived,
  trends, indices) are independently toggleable.
- Entity grid per model device: one sensor per (parameter, horizon) — horizons
  configurable, default +3/+6/+12/+24/+48/+72 h. No JSON series attribute, no
  `state_class` (forecasts are not measurements). Recommend a `recorder:` exclude
  for `sensor.njord_*` in HA docs/snippets.
- Sensor fallback chain: live sensor reading (SensorHub via gRPC) → config value
  → hardcoded default. `SensorKind` is closed: `IndoorTemperature`,
  `IndoorHumidity`.
- MQTT egress (optional, `Mqtt:Enabled`): state only, no MQTT Discovery. One
  retained flat state JSON per device per cycle, values a model does not deliver
  are omitted (no `unavailable`, no `expire_after`); availability only via the
  retained `online`/`offline` service status topic (LWT). MQTTnet sits behind the
  `IMqttTransport` seam; the connection actor owns the connection lifecycle.
- MQTT payloads are typed records (`Njord.Mqtt/Payloads`), built by mappers in
  `Mapping/` and written by one source-generated serializer context
  (`MqttPayloadJsonContext`, converters in `Serialization/`); no `JsonObject`.
  Null rule: forecast horizon payloads omit absent values; enrichment payloads
  (alerts, derived, trends, indices, consensus) write a known member without a
  value as explicit `null` (alerts always carry `peak_value`, `hours_until`,
  `duration_hours`). `soil_frozen` (ice alert) is a JSON boolean.

## Language

All code, specs, docs, and communication in English.

## Solution structure

```
src/
  Njord.slnx

  # /Foundation/ — shared types, messages, persistence, computation, infrastructure
  Njord.Weather/               # Pure weather records, ModelCoverageRegistry, HorizonProjection,
                              #   ParameterRegistry (no Njord references)
  Njord.Sensors/               # Pure sensor records (no Njord references)
  Njord.Persistence/          # Persistence DTOs (extend-only; no Njord references): Events/Budget, Events/Polling
  Njord.Messages/             # Commands, queries, results, PipelineEvent; no Akka
                              #   (-> Weather, Sensors, Compute)
    Budget/ Config/ Forecast/ Pipeline/ Sensors/
  Njord.Compute/              # Shared analysis building blocks only (-> Weather, Sensors only)
    Consensus/                # ConsensusBuilder, ConsensusStatistics, consensus records
    Querying/                 # Query vocabulary on consensus data (ConsensusQueries,
                              #   HorizonWindow, HorizonHit)
    Contracts/                # Result records, SeverityThresholds, result enums
    Settings/                 # Immutable per-feature settings records
  Njord.Core/                 # Options, actor keys, NjordHealthMonitor, StreamSupervision
                              #   (-> Weather, Sensors, Messages, Persistence, Compute)
    Actors/                   # Marker interfaces, ShardMessageExtractor, StreamConsumerActor, stream handshakes
                              #   (StreamWiringMessages: pipeline output, forecast,
                              #   read-model and config streams)
    Configuration/            # Options and validators, budget calculator, ConfigWriter,
                              #   ConfigSnapshotMappings (options -> Messages/Config contracts),
                              #   ConfigUpdateMappings (commands -> options, options -> messages),
                              #   ParameterOptions.Resolve(), PollingOptions.ToInitialBudgetSnapshot()

  # /Domain/ — feature libraries (each -> Core only)
  Njord.OpenMeteo/            # OpenMeteoGateway, OpenMeteoClient (HTTP, status handling, outcomes), DTOs,
                              #   JSON source generator
    Mapping/                  # OpenMeteoSeriesMappings (ToForecastSeries, ToDailySeries),
                              #   OpenMeteoRequestMappings (ToRequestUri), OpenMeteoUnitCheck
  Njord.Budget/               # BudgetTracker persistent actor, usage accounting
  Njord.SensorHub/            # SensorHub
  Njord.Pipeline/             # PollManager, PollWorker (+ PollWorkerState), ForecastPipeline,
                              #   PipelineEvent BroadcastHub,
                              #   ForecastReadModel (API read model)
  Njord.Mqtt/                 # Everything MQTT-specific (boundary rule below); namespaces follow folders
    Actors/                   # MqttConnection, MqttStatePublisher, internal actor messages
    Topics/                   # TopicScheme, TopicSlug
    Mapping/                  # PipelineEventMqttMappings (ToMqttMessages with MqttPublishContext,
                              #   ToLocationName), payload mappings (Forecast, Analysis, Consensus)
    Health/                   # MqttConnectionHealthCheck (registered by MqttSetupContainer)
    Transport/                # IMqttConnection, IMqttTransport, MQTTnet publisher
    Configuration/            # MqttSetupContainer
                              #   (root: StatePayloadBuilder, MqttMessage, MqttEgressTuning, Telemetry)
  Njord.Grpc/                 # gRPC services (ask and map only), ActorAsk, protos
    Mapping/                  # One <Domain>ProtoMappings class per domain (Alert, Index, Trend,
                              #   Derived, Consensus, Forecast, Sensor, Ops, Catalog), EnrichmentEventMappings,
                              #   ConfigRequestMappings (ToCommand), ConfigResponseMappings (ToProto) with the
                              #   ConfigResponses helper, WireEnumMappings: extension methods only

  # /Analysis/ — per-feature analysis libraries (each -> Core only), own their computation
  Njord.Alerts/               # AlertAnalyzer, IAlertRule, Rules/ (one rule per AlertType),
                              #   AlertWindow
  Njord.Derived/              # DerivedAnalyzer, DerivedFormulas
  Njord.Trends/               # TrendAnalyzer, TrendFormulas
  Njord.Indices/              # IndexAnalyzer, IIndexScore, Scores/ (one score per IndexKind),
                              #   DaySlicer, FrostProtection, VaporPressureDeficit

  # Host
  Njord/                      # Service host: Program.cs, DI, actors, streams (-> all above)
    Configuration/            # Host setup, ConfigManager (config queries, commands, stream);
                              #   containers in Program.cs order: CoreSetupContainer (TimeProvider, meter
                              #   allow-list), ConfigSetupContainer (options, writer, ConfigManager),
                              #   HealthSetupContainer (monitor, health check), then the feature containers
    Health/

  # /Tests/ — one test project per production project
  Njord.Weather.Tests/        # Weather record specs
  Njord.Sensors.Tests/        # Sensor record specs
  Njord.Persistence.Tests/    # Persistence roundtrip and inline wire-format specs (no Verify)
  Njord.Compute.Tests/        # Consensus, query vocabulary and contract specs (no Akka)
  Njord.Core.Tests/           # Configuration, StreamConsumerActor
  Njord.OpenMeteo.Tests/      # OpenMeteoClient, OptionsBudgetProvider specs
  Njord.Budget.Tests/         # BudgetTracker specs
  Njord.SensorHub.Tests/      # SensorHub specs
  Njord.Pipeline.Tests/       # PollWorker, PollManager, polling cluster, ForecastPipeline,
                              #   ForecastReadModel specs
  Njord.Mqtt.Tests/           # MQTT connection, state payload, consumer specs
  Njord.Grpc.Tests/           # gRPC service, ActorAsk and mapping specs, golden masters
  Njord.Alerts.Tests/         # AlertAnalyzer specs, Rules/ (one spec per rule)
  Njord.Derived.Tests/        # DerivedAnalyzer, DerivedFormulas specs
  Njord.Trends.Tests/         # TrendAnalyzer, TrendFormulas specs
  Njord.Indices.Tests/        # IndexAnalyzer, DaySlicer specs, Scores/ (one spec per score)
  Njord.Tests/                # Host specs: ConfigManager, options binding
  Njord.Architecture.Tests/   # ArchUnit zone/layer/convention rules
  Njord.Tests.Shared/         # Shared fakes, fixtures, helpers (not a test project)
```

Reference direction: Weather/Sensors <- Compute <- Messages; Weather/Sensors/Compute/Messages/Persistence <- Core <- feature libs
(OpenMeteo, Budget, SensorHub, Grpc, Pipeline, Mqtt, Alerts, Derived, Trends,
Indices) <- host. Feature libs reference only Njord.Core (and below, including
Compute transitively), never each other and never the host; they reach each other's actors
through the marker interfaces in `Njord.Core/Actors/ActorKeys.cs`. Registration
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

Current total: 1474 tests (Weather 117, Sensors 11, Persistence 14, Compute 176, Core 236,
Grpc 150, Pipeline 144, Mqtt 151, OpenMeteo 62, Budget 28, SensorHub 32, Architecture 79,
Alerts 67, Derived 36, Trends 27, Indices 63, host `Njord.Tests` 81).
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
- `appsettings.json` — logging and Kestrel plus the `Njord` section with the default of
  every scalar, flag, enum and duration option (the single source of those defaults;
  option classes carry no initializer for them). List-valued defaults (`Horizons`,
  `Parameters.Groups`, threshold arrays) and structural initializers (dictionaries with a
  comparer, nested `new()`) stay in code, because configuration merges arrays by index;
  `ReplaceCollectionDefaults` makes a configured list replace its default. A new scalar
  option gets its default in this file, not as a property initializer.
- `appsettings.Development.json` — dev overrides (locations, models, MQTT
  disabled). Loaded automatically by `dotnet run` (`ASPNETCORE_ENVIRONMENT=Development`).
- `data/appsettings.Override.json` — runtime override written by `ConfigWriter` (Docker
  volume mount), loaded last. "Reset to defaults" removes the override keys, so the value
  falls back to `appsettings.json` (or the code default of a list).
- Environment variables — Docker-compose / `docker run` (`Njord__Mqtt__Host`, etc.).
- `appsettings.Example.json` — copy-in template with deployment-specific keys only; a spec
  fails when it repeats a default of `appsettings.json`.

Tests obtain default options through `AppSettingsDefaults` (`Njord.Tests.Shared`, binds the
shipped `appsettings.json`) instead of `new XOptions()`; `DefaultOptionsSpec` pins the bound
defaults with a Verify golden master and validates them with every validator.

MQTT is disabled by default (`Mqtt:Enabled = false`). Enable explicitly with
`Njord__Mqtt__Enabled=true` + `Njord__Mqtt__Host=...`.

`DisabledTestArchitectureSpec` (`src/Njord.Architecture.Tests`) guards against
skipped or ignored tests (`[Fact(Skip = ...)]`, `[Theory(Skip = ...)]`, `[Ignore]`)
across all `*Spec.cs` test files.

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
  use a unique name. Retry-backoff specs set `AddFastRetryBackoff()` instead of
  waiting real seconds. Pure synchronous specs use a plain `[Fact]`, a timeout
  cannot be enforced there.
- C#: records for messages/DTOs, value objects in the domain, `sealed` by
  default, nullable enabled.
- Enum to wire text or to another enum is an extension method with an explicit
  `switch` expression, one arm per member and a default that throws
  `ArgumentOutOfRangeException` (`ToWireName()`, `ToTopicSegment()`, `ToProto()`). No casts
  between enum types, `Enum.Parse`/`TryParse` or `ToString().ToLower*()` for wire values;
  `EnumConversionArchitectureSpec` scans production code, allow-list: `ParameterRegistry`
  (user-written configuration text). Each conversion has a spec pinning every member.
- Mappings (domain to proto, request to command, options to message) are public extension methods in
  `internal static` classes named `<Domain><Target>Mappings`, one per domain; no state, no options, no
  registry lookups, never inside actors or services. Names follow the direction: `ToProto`, `ToCommand`,
  `ToDomain`, `ToMessage`, never `Map*`. `DateTimeOffset` to protobuf uses Google.Protobuf's
  `ToTimestamp()`. `ConfigSnapshotMappings` and `ConfigUpdateMappings` (Njord.Core) stay `public`
  because the host consumes them without `InternalsVisibleTo`. `MappingConventionArchitectureSpec`
  enforces this; each mapping class has a spec pinning every field. Actors and services declare no
  methods named `Map*`, `Parse*Array` or `Build*Uri` (the same spec; allow-list: `MapNjordGrpc`, the
  ASP.NET `Map*` endpoint idiom): event-to-MQTT, Open-Meteo series/URI and log-level conversions live in
  `Mapping/` extensions that take settings as arguments or a small context record
  (`MqttPublishContext`).
- Enrichment types use `EnrichmentKind` enum (`Njord.Messages.Pipeline`), never bare
  strings. Enrichment `PipelineEvent` variants implement `IEnrichmentEvent`.
- Consensus method is `ConsensusMethod` enum (`Njord.Compute.Settings`),
  not a string.
- `ParameterRegistry` (`Njord.Weather`): use typed static properties
  (`ParameterRegistry.Rain`, `.PrecipitationSum`, etc.) in production code.
  `GetByApiName` is for deserialization only — never call it with a string
  literal.
- Persistence records (`Njord.Persistence`): follow FunkArr conventions
  (`akka-skills:persistence`). Positional records (no `[JsonProperty]`,
  no init-properties). `DateTimeOffset` for timestamps, `TimeSpan` for
  durations — never `long` ticks. No `Dto`/`Event` suffix — use past-tense
  for events (`SomethingHappened`), `Persisted<Name>State` for snapshots.
  New properties must be nullable or have a default. While the project is 0.x,
  breaking persistence changes are allowed (`refactor!:`).
- Persistent actors use `LastSequenceNr % SnapshotInterval == 0` for snapshot
  scheduling (FunkArr pattern). No counters on domain state records.
- Journal cleanup after snapshot: `DeleteMessages(seqNr)` +
  `DeleteSnapshots(seqNr - 1)` in `SaveSnapshotSuccess` handler.
- `Apply` methods on state take the event record directly (e.g.
  `Apply(ApiCallRecorded evt, ...)`), not decomposed primitive values.
- `NjordHealthMonitor` (`Njord.Core.Health`) provides health state via atomic
  immutable snapshot records (`MqttHealthSnapshot`, `PollHealthSnapshot`,
  `BudgetHealthSnapshot`). Not `*State` — no mutable fields.

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
  `IMqttConnectionActor`, ... in `Njord.Core/Actors/ActorKeys.cs`), registered
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

Known deviations: 4 remaining `!.` uses on nullable results in the test projects
(2× `ToString()!.Contains` in `BudgetTrackerSpec.cs` lambdas,
2× `_queue!.OfferAsync` in `PipelineConnectionSpec.cs`/`SinkRefConnectionSpec.cs`
test actors), not counting the allowed `JsonNode` indexers; new code follows the rule.

## Metrics conventions

- One `Meter("Njord.<Domain>")` per feature library (OpenMeteo, Budget, Pipeline,
  Mqtt; plus `Njord.Core` for its shared base classes), owned by an `internal static class Telemetry` in that project with
  `internal static readonly` instrument fields (`Telemetry.MqttDedup.Add(...)`).
  Each `Telemetry` exposes `MeterName`; `CoreSetupContainer.NjordMeters` is built from these constants
  (hence `InternalsVisibleTo Njord` on those libraries), so a new meter is added to that list.
  `BudgetSetupContainer` registers the budget gauges (hosted service over the `NjordHealthMonitor`).
- Names are `njord_<area>_<name>` in snake_case; counters end in `_total`,
  time histograms in `_seconds`. Keep tag cardinality bounded.
- Metrics are exported via prometheus-net.

## References

- Open-Meteo API: https://open-meteo.com/en/docs (endpoint: `https://api.open-meteo.com/v1/forecast`)
