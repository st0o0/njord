# AGENTS.md

## Project

njord — Multi-model weather intelligence for Home Assistant. A .NET service
(Docker container) on Akka.NET + Akka.Streams: polls the Open-Meteo APIs (weather ensemble,
air quality, flood, marine) per location, runs the data through enrichment handlers (alerts,
lifestyle advisories, trends), and serves the results to Home Assistant over gRPC (the ha-njord
integration), with optional MQTT state publishing.

### Architecture guardrails

- **Enrichment-driven configuration.** The operator configures locations and enrichment
  toggles (`Njord:Enrichments`, `EnrichmentOptions`); everything else is derived. Every
  enrichment is an `IDomainHandler` (`Njord.Core/Domain`): `ConfigKey` (the toggle name),
  `Triggers` (the `DomainKind`s it reacts to) and
  `Handle(current, previous, time)` returning `PipelineEvent`s. The domains that are fetched
  are the union of the `Triggers` of the enabled handlers (`EnrichmentResolution.RequiredDomains`,
  registered as `IReadOnlySet<DomainKind>` by `DomainSetupContainer`). The 13 toggles are
  `WeatherAlerts`, `CommuteSafety`, `PollenAlert`, `Pm25Alert` (on by default) and `FloodRisk`,
  `FloodRain`, `SwimmingConditions`, `VentilationWindow`, `ExerciseAdvisory`, `LaundryScore`,
  `PollenTrend`, `NightVentilation`, `ConfidenceTrend` (off by default).
  `EnrichmentConfigKeySpec` fails when a registered handler's `ConfigKey` matches no option.
  There are no model, horizon or parameter settings: the weather variables are internal
  (`WeatherVariables`, 31 standard, 36 with `Weather:IncludeSolar`), the weather request goes to
  the Open-Meteo ensemble endpoint, and the entity set follows locations x enabled handlers.
- **Domains and typed clients.** `DomainKind`: `Weather`, `AirQuality`, `Flood`, `Solar`,
  `Marine` (`Njord.Model`). `Njord.OpenMeteo` holds one typed HTTP client per API
  (`IWeatherClient`, `IAirQualityClient`, `IFloodClient`, `IMarineClient`, `IGeocodingClient`),
  each with its own fixed base address.
  `IncludeSolar` adds the solar radiation variables to the weather request (the gateway fetches
  only Weather, AirQuality, Flood and Marine; `Solar` exists in the model and event vocabulary).
- **Geocoding resolves locations.** A location is `Name` only; `LocationResolutionService`
  (hosted service registered ahead of Akka) fills in latitude/longitude through
  `IGeocodingClient` for every location without coordinates and fails startup when there is
  no match. Explicit `Latitude`/`Longitude` skip the lookup.
- **Domain projects are plugins.** `Njord.Weather` (alerts, lifestyle handlers),
  `Njord.AirQuality`, `Njord.Flood`, `Njord.Marine` each register their `IDomainHandler`s via
  an `Add<Domain>Domain()` extension, reference only `Njord.Core`, never each other and never
  the adapters (`Njord.Grpc`, `Njord.Mqtt`); enforced in `ArchitectureSpec`.
- **Streams for data flow, actors for lifecycle; the stable side owns the hubs.**
  Domain data and pipeline events travel through Akka.Streams (stream refs between actors);
  requests, outcomes and scheduling travel as messages.
  **Polling** is one persistent `PollWorker` per location in the `poll-worker` shard region
  (`IPollRegion`; entity id is the location name, messages implement `IWithLocationId`, routed by
  `ShardMessageExtractor` in Core). A worker owns `PollWorkerState` (target, phase, per-domain
  `DomainPollState` with learned cycle, next poll, miss count and last hash; every transition
  is persisted as an event, snapshot every 50), its timer, and the current poll round
  (`PollRoundState`: one `FetchDomain` per due domain, replies collected per round id). It reports
  every start and outcome (`PollWorkerReport`) to `PollManager` (`IPollManager`, a non-persistent
  cluster singleton with BackoffOnFailure). The manager derives one target per configured location,
  sends `StartPolling`, `StopPolling` to running workers of removed targets, restarts terminated
  workers, forwards `TriggerImmediatePoll`/`TriggerTargetPoll`, and answers `QueryPollStates` and
  `QueryPollHealth` from the reports.
  **Per-domain discovery:** each domain of a location learns its own update cycle (phases
  `Discovery` -> learned cycle with miss tolerance) and is scheduled independently; a round only
  fetches the domains that are due.
  **Gateway** (`OpenMeteoGateway`, `IForecastGateway`) is request/response: each `PollWorker` asks
  `FetchDomain` (named reply timeout, `FetchTimeouts`) and gets exactly one `FetchDomainCompleted`
  (hash only, no data), `FetchDomainFailed` or `FetchDomainRejected` (`FetchRejectionReason`).
  The gateway checks the request budget (`GatewayBudgetGate`, token bucket from the effective
  budget), calls the typed client for the domain and offers the resulting `DomainDataReceived` to a
  small queue branch into the pipeline's `SinkRef` (`RequestForecastSink` handshake); only this
  branch is re-attached (with backoff) when the pipeline terminates or the branch ends.
  **Pipeline** (`ForecastPipeline`) owns both hubs, materialized once per incarnation:
  `MergeHub<DomainDataReceived>` (one SinkRef per producer; a producer ending never ends the hub)
  -> `StatefulSelectMany(ProjectionEngine)` -> `BroadcastHub<PipelineEvent>`. The `ProjectionEngine`
  keeps the latest `LocationForecast` and the previous value of every domain slot per location and
  runs every enabled `IDomainHandler` whose `Triggers` contain the received domain; each received
  domain also yields its `*DataReceived` event. All analysis state lives inside the closure, so a
  gateway restart never loses it. Consumers attach via SourceRef.
- **Never `Zip` domain sub-streams.** Domains arrive independently; handlers get whatever
  `LocationForecast` currently holds (`Previous` for trend handlers).
- **`TimeProvider` everywhere**, never `DateTime.Now`/`UtcNow` directly.
- **Consumers are output adapters via SourceRef.** The MQTT state publisher and the API's gRPC
  consumer (`GrpcConsumer` in `Njord.Grpc`, marker `IGrpcConsumer`) request a
  `SourceRef<PipelineEvent>` from `ForecastPipeline` and run their own sinks. No relay actors, no
  presenters. No consumer slows the pipeline hub: the gRPC consumer builds its streaming hub once
  at start and gives each client a bounded drop-oldest buffer; the MQTT publisher buffers with
  `DropHead`, dedups, and republishes every topic after a broker (re)connect.
- **The gRPC API is an adapter.** `Njord.Grpc` (`WeatherGrpcService`: `GetCatalog`,
  `GetDomainData`, `GetEnrichments`, `StreamDomainData`, `StreamEnrichments`; `OpsGrpcService`:
  `GetStatus`, `GetTargets`, `TriggerPoll`; protos in `protos/njord/v2`) resolves an actor marker,
  asks with a message from `Njord.Messages` through `ActorAsk` (`Unavailable` when the actor is
  not resolvable, `DeadlineExceeded` on timeout) and maps the result with `ToProto()` extensions
  in `Njord.Grpc/Mapping/`. Options are read-only startup config exposed through `ConfigView`
  (locations + polling) and `ServiceInfo`; there is no runtime config writer.
- **`Njord.Messages` is the Akka-free contract project.** Commands, queries, results, events and
  `PipelineEvent` live there in domain folders (`Alerts/`, `Budget/`, `Config/`, `Forecast/`,
  `Pipeline/`), referencing only `Njord.Model`. Records that carry Akka types (stream handshakes)
  live in `Njord.Core/Actors/*StreamMessages.cs`; actor-internal records stay private.
- **MQTT-specific code lives only in `Njord.Mqtt`** (actors, topics, transport, payload records,
  mappers, health check, telemetry). The egress is an optional adapter: state only, no MQTT
  Discovery, one retained JSON payload per domain/enrichment, availability via the retained
  `online`/`offline` status topic (LWT). MQTTnet sits behind `IMqttTransport`. Payloads are typed
  records (`Njord.Mqtt/Payloads`) written by a source-generated serializer context.
- **Zone rules are enforced by tests** in `src/Njord.Architecture.Tests/ArchitectureSpec.cs`:
  Model, Messages and Persistence reference no other Njord project (Messages only Model);
  Messages and Persistence have no Akka dependency; the domain projects do not depend on each
  other or on the adapters; all concrete classes are sealed.

### Decisions

- Open-Meteo free tier (non-commercial) is the assumed plan: the request budget defaults to its
  soft limits (300k/month, 600/min) with an optional `Polling:BudgetOverride`. Startup validates
  projected usage (`BudgetCalculator.Validate`: locations x required domains x poll cycles x API
  call weight) against 80 % of the monthly budget. Commercial use would need the paid tier.
- Any change that adds or alters polling needs a budget estimate against the free-tier soft limits
  (300k/month, 10k/day) and politeness toward a free service. The weather request weighs
  `ceil(variables / 10)` calls (4 standard, 4 with solar); other domains weigh 1.
- Poll interval and discovery interval are config values (defaults 60 min and 20 min).
- gRPC and MQTT wire text for closed sets comes from explicit mappings, never from `ToString()`.
- Feels-like comes from the API (`apparent_temperature`), no own computation.
- Alert thresholds are `Njord:Enrichment:Alerts:*` (`AlertOptions`, three-step severity arrays
  such as `FrostThresholds`, validated by `AlertOptionsValidator`).

## Language

All code, specs, docs, and communication in English.

## Solution structure

```
src/
  Njord.slnx

  Njord.Model/          # Vocabulary, no Njord references: DomainKind, Location, DomainForecast<T>,
                        #   WeatherPoint, AirQualityPoint, FloodPoint, SolarPoint, MarinePoint, Horizon
  Njord.Messages/       # Akka-free contracts (-> Model): PipelineEvent, Fetch*/Poll*/Query* messages,
                        #   Alerts/ (Alert, AlertType, AlertSeverity, thresholds), Budget/, Config/, Forecast/, Pipeline/
  Njord.Persistence/    # Persistence records (extend-only, no Njord references): Events/Budget, Events/Polling
  Njord.Core/           # (-> Model, Messages, Persistence)
    ActorKeys.cs        #   marker interfaces (IPollManager, IPollRegion, IBudgetTracker, IForecastPipeline,
                        #   IGrpcConsumer, IMqttConnectionActor, IMqttStatePublisher, IForecastGateway)
    Actors/             #   ShardMessageExtractor, StreamSupervision, stream handshake messages
    Domain/             #   IDomainHandler, IAlertRule, EnrichmentResolution, LocationForecast, DomainDataReceived
    Configuration/      #   NjordOptions (Locations only), LocationOptions, WeatherOptions, EnrichmentOptions,
                        #   AlertOptions, WeatherVariables, ConfigView, AddNjordOptions + validators
      Polling/ OpenMeteo/ Persistence/ Mqtt/ Health/   # PollingOptions, BudgetCalculator, FetchTimeouts,
                        #   OpenMeteoOptions, PersistenceOptions, MqttOptions, HealthOptions
  Njord.OpenMeteo/      # (-> Core) OpenMeteoGateway, GatewayBudgetGate, typed clients (Weather, AirQuality,
                        #   Flood, Marine, Geocoding), LocationResolutionService, response DTOs, Mapping/
  Njord.Budget/         # BudgetTracker persistent actor, usage accounting, health check
  Njord.Pipeline/       # PollManager, PollWorker (+ state), ForecastPipeline, ProjectionEngine, pipeline health
  Njord.Weather/        # WeatherAlertHandler + Rules/ (one rule per alert type), Lifestyle/ handlers
                        #   (CommuteSafety, VentilationWindow, ExerciseAdvisory, LaundryScore,
                        #   NightVentilation, ConfidenceTrend)
  Njord.AirQuality/     # AirQualityAlertHandler (pollen, PM2.5), PollenTrendHandler
  Njord.Flood/          # FloodRiskHandler, FloodRainHandler
  Njord.Marine/         # SwimmingConditionsHandler
  Njord.Mqtt/           # Actors/, Topics/, Mapping/, Payloads/, Serialization/, Transport/, Health/
  Njord.Grpc/           # WeatherGrpcService, OpsGrpcService, ActorAsk, GrpcConsumer, Mapping/
  Njord/                # Host: Program.cs, NjordHost, Configuration/ (setup containers, service info mappings), Health/

  # Tests, one project per production project (xUnit v3, Microsoft.Testing.Platform)
  Njord.Model.Tests/ Njord.Messages.Tests/ Njord.Persistence.Tests/ Njord.Core.Tests/
  Njord.OpenMeteo.Tests/ Njord.Budget.Tests/ Njord.Pipeline.Tests/ Njord.Weather.Tests/
  Njord.AirQuality.Tests/ Njord.Flood.Tests/ Njord.Marine.Tests/ Njord.Mqtt.Tests/ Njord.Grpc.Tests/
  Njord.IntegrationTests/        # whole host via NjordHost + TestServer
  Njord.Architecture.Tests/      # ArchUnitNET rules + EnrichmentConfigKeySpec
  Njord.Tests.Shared/            # fakes, builders, fixtures, NjordActorSpec (not a test project)
protos/njord/v2/                 # gRPC contracts (weather.proto, ops.proto, ...)
docs/                            # VitePress site incl. the Config Builder (docs/.vitepress/theme/builder)
```

Reference direction: Model <- Messages; Model/Messages/Persistence <- Core <- feature libraries
(OpenMeteo, Budget, Pipeline, Weather, AirQuality, Flood, Marine, Mqtt, Grpc) <- host. Feature
libraries reference only `Njord.Core` (and below), never each other and never the host; they reach
each other's actors through the marker interfaces in `Njord.Core/ActorKeys.cs`. Registration stays
central in the host (`AkkaSetupContainer`, `DomainSetupContainer`, ...).

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

Current total: 627 tests (Model 30, Messages 41, Persistence 18, Core 85, OpenMeteo 46, Budget 18,
Pipeline 168, Weather 55, AirQuality 17, Flood 18, Marine 15, Mqtt 53, Grpc 32, Architecture 16,
IntegrationTests 15). The count is one test per `[Fact]` and one per data row of a `[Theory]`.
CI's `dotnet test --solution Njord.slnx` runs every test project. Each project is its own process
with its own thread pool: running many at once on a small runner can slow the load-sensitive actor
specs, so prefer the sequential loop above.

Tests are xUnit v3 on Microsoft.Testing.Platform — `dotnet run`, **not** `dotnet test`.
Shared test infrastructure lives in `Njord.Tests.Shared`.

End-to-end verification of the full stack (njord + ha-njord + Home Assistant, via Docker Compose +
browser automation) is the `e2e-test` skill, not a `dotnet test` project — see
`.claude/skills/e2e-test/SKILL.md` and `e2e/E2E-TEST-PLAN.md`.

Run the service itself from `src/Njord/` (`dotnet run`). Configuration layers:
- `appsettings.json` — logging and Kestrel only (no `Njord` section; all option defaults live as
  property initializers on the options classes). `ReplaceCollectionDefaults` makes a configured list
  replace its code default. A new option gets its default as a property initializer.
- `appsettings.Development.json` — dev overrides, loaded by `dotnet run`.
- Environment variables — Docker-compose / `docker run` (`Njord__Mqtt__Host`, etc.).
- `appsettings.Example.json` — copy-in template with deployment-specific keys only.
- `docker-compose.example.yml` (repo root) — the commented variables show each option's default.

Option sections (one options class per concern, each with its own `SectionName`, registered with
`AddNjordOptions<T>(configuration)`: binds the section, lets a configured list replace its default,
registers `<T>Validator`, `ValidateOnStart`):

| Section | Class | Content |
|---------|-------|---------|
| `Njord` | `NjordOptions` | `Locations` only (`Name`, optional `Latitude`/`Longitude`) |
| `Njord:Weather` | `WeatherOptions` | `IncludeSolar` (false), `ForecastDays` (4) |
| `Njord:Enrichments` | `EnrichmentOptions` | the 13 enrichment toggles |
| `Njord:Enrichment:Alerts` | `AlertOptions` | alert thresholds |
| `Njord:Polling` | `PollingOptions` | `PollInterval`, `DiscoveryInterval`, `BudgetOverride` |
| `Njord:Persistence` | `PersistenceOptions` | `Provider` (Sqlite/PostgreSql), `ConnectionString`, `DataPath` |
| `Njord:Health` | `HealthOptions` | health thresholds |
| `Njord:Mqtt` | `MqttOptions` | `Enabled`, `Host`, `Port`, `Username`, `Password`, `DiscoveryPrefix`, `BaseTopic` |

All options are read at startup (`IOptions<T>`); there is no hot reload and no runtime config
writer. MQTT is disabled by default; enable with `Njord__Mqtt__Enabled=true` + `Njord__Mqtt__Host=...`.

The Config Builder (`docs/.vitepress/theme/builder`: `types.ts`, `budget.ts`, `serializer.ts`,
`ConfigBuilder.vue`, data in `docs/data/enrichment.json`) mirrors these options: the enrichment
toggles and their `Triggers` in `types.ts`, the budget formula of `BudgetCalculator` in `budget.ts`.
Change them together with the options.

Mutation testing (Stryker.NET, pure-logic projects): `bash scripts/run-mutation.sh <Project>` from the
repo root; config in `src/stryker-config.json`, notes in `docs/development/mutation-testing.md`.

OpenSpec checks (run from the repo root, local only, not part of CI):

```bash
openspec validate --all --no-interactive
bash scripts/check-openspec-root.sh    # fails if an openspec/ root exists outside <repo root>/openspec
```

## Open-Meteo API

- Weather: `GET {BaseUrl}v1/ensemble?latitude=&longitude=` (default `https://api.open-meteo.com/`);
  air quality, flood, marine and geocoding each use their own host (`air-quality-api`, `flood-api`,
  `marine-api`, `geocoding-api` `.open-meteo.com`). **No API key.** Free tier is non-commercial; soft
  limits 600/min, 5k/h, 10k/day, 300k/month. Calls are weighted: >10 hourly variables count
  fractionally as multiple calls.
- Wind in m/s, times as unix timestamps, timezone UTC (so hourly and daily timestamps align to
  midnight UTC). Values beyond a horizon are `null` entries. HTTP 400 carries
  `{"error":true,"reason":...}`.

## Conventions

- OpenSpec lives only in `<repo root>/openspec`. Never delete `openspec/changes/archive`.
- **Git: NEVER `git push`** — the user pushes. Commit messages are Conventional Commits
  (commitlint-enforced).
- Versioning is release-please. Never edit `<Version>` in `src/Directory.Build.props` by hand.
- Central package management: versions live only in `src/Directory.Packages.props`; add packages via
  `dotnet add package`, never edit csproj XML for versions.
- Tests: `Spec` suffix, `sealed` classes, BDD-style method names. Async and actor tests carry an
  explicit `[Fact(Timeout = ...)]` and pass `TestContext.Current.CancellationToken` to awaited calls
  (xUnit1069 is an error). The xUnit timeout is an outer safety net and must exceed every Akka wait
  it wraps: TestKit waits dilate by `akka.test.timefactor = 3`, and the dilation applies to explicit
  durations too. Specs that start a hosted actor system or host use `TestTimeouts.Hosted`
  (`Njord.Tests.Shared`) and bound `AwaitAssert`/`AwaitCondition`/`ExpectMsg` with
  `TestTimeouts.AwaitAssertMax`; waits TestKit does not dilate use `TestTimeouts.RealTimeWaitMax`.
  Never reuse an actor name after `GracefulStop`: use a unique name. Actor specs derive from
  `NjordActorSpec` (Akka.Hosting TestKit, in-memory persistence); never create an `ActorSystem` by hand.
  Retry-backoff specs pass a short `BackoffPolicy` instead of waiting real seconds. Pure synchronous
  specs use a plain `[Fact]`.
- C#: records for messages/DTOs, value objects in the domain, `sealed` by default, nullable enabled.
- Enum to wire text or to another enum is an extension method with an explicit `switch` expression,
  one arm per member and a default that throws `ArgumentOutOfRangeException` (`ToProto()`,
  `ToWireName()`). No casts between enum types, `Enum.Parse`/`TryParse` or `ToString().ToLower*()`
  for wire values. Each conversion has a spec pinning every member.
- Mappings (domain to proto, event to MQTT, response to domain) are extension methods in static
  classes named `<Domain><Target>Mappings`; no state, never inside actors. Names follow the direction:
  `ToProto`, `ToDomain`, `ToMessage`, never `Map*`. `DateTimeOffset` to protobuf uses
  `ToTimestamp()`.
- **"Snapshot" is reserved for persisted actor state**: the persisted state of a persistent actor is
  `Persisted<Name>State`; the Akka members (`SaveSnapshot`, `SnapshotOffer`, ...) keep their names.
- Persistence records (`Njord.Persistence`): follow FunkArr conventions (`akka-skills:persistence`).
  Positional records, `DateTimeOffset` for timestamps, `TimeSpan` for durations, past-tense event
  names (`ForecastChanged`), `Persisted<Name>State` for snapshots. New properties must be nullable or
  have a default. Closed sets are `Persisted*` enums with explicit numeric values. Mapping between
  state and persisted records lives in `<Owner>PersistenceMappings` in the owning project; state to
  message contract in `<Owner>MessageMappings`. While the project is 0.x, breaking persistence
  changes are allowed (`refactor!:`).
- Persistent actors use `LastSequenceNr % SnapshotInterval == 0` for snapshot scheduling. Journal
  cleanup after snapshot: `DeleteMessages(seqNr)` + `DeleteSnapshots(seqNr - 1)` in
  `SaveSnapshotSuccess`. `Apply` methods on state take the event record directly.
- Health is feature-owned: `PollHealthState` (Pipeline), `MqttConnectionState` (Mqtt) and
  `BudgetUsageState` (Budget) are written only by their actor as one atomic swap of an immutable
  record and read only by the feature's own `IHealthCheck` (`pipeline`, `budget`, `mqtt-connection`;
  the host adds `journal` and `data-directory`). Checks never ask actors. `/healthz` answers 200 for
  Healthy and Degraded and 503 for Unhealthy; `/alive` is unconditional.
- Adding an enrichment: implement `IDomainHandler` in the owning domain project, register it in that
  project's `Add<Domain>Domain()`, add the toggle to `EnrichmentOptions`, the contract/event to
  `Njord.Messages`, and update the Config Builder (`types.ts`) and `docker-compose.example.yml`
  (skill `njord-enrichment-feature`).

## C# conventions

- Style (namespaces, `using` order, `_camelCase` fields, braces, `var`) is enforced by `.editorconfig`.
- No XML docs. Code speaks through naming.
- Never use `async void`, `.Result`, or `.Wait()`.
- Always pass `CancellationToken` through async call chains.

## Akka conventions

Rules apply to production code.

- **No `ContinueWith`** in actor code. Use `PipeTo` with `success:` and `failure:` mappers.
- **No `Status.Failure`/`Status.Success`.** Use project-owned `XxxFailed(Exception Cause)` records
  (plus the request id when answering a correlated request); a failure message must be handled by its
  requester, never left to the stash or dead letters.
- **No `EventStream`** in production code. Use explicit actor references. TestKit probes may
  subscribe to `DeadLetter`/`Warning` in tests.
- **No `IActorRef` constructor parameters** in production actors.
- **Actor naming:** domain-role names (`PollManager`, `PollWorker`, `ForecastPipeline`), sealed.
  `*State` suffix is reserved for immutable state records, never for actors.
- **Actor logging:** `private readonly ILoggingAdapter _log = Context.GetLogger();` — inline field
  init, `readonly`, named `_log`. The host's `ActorCategoryLogger` writes each event with the logging
  class's full type name as category; `akka.loglevel` follows the lowest `Serilog:MinimumLevel`.
- **Actors are resolved by marker interfaces** (`ActorKeys.cs`), registered in the host
  (`AkkaSetupContainer`); never key a registry lookup by the actor class.

## Test assertion conventions

- Prefer `Assert.NotNull(value)` before accessing nullable members over the null-forgiving operator
  (`!.`). Exception: `JsonNode` indexers.
- `var item = Assert.Single(collection)` instead of `collection[0]`; assert the count before indexing
  several elements.
- No vacuous assertion after an already-proven value; assert the exact computed value, not just
  existence.
- Exceptions: `var ex = Assert.Throws<T>(...)` / `await Assert.ThrowsAsync<T>(...)`, then assert on
  `ex`'s fields (`ParamName`, `Message`), not just the type.
- `Assert.IsType<T>(obj, exactMatch: false)` to prove interface/base-type conformance.
- No `Thread.Sleep`/arbitrary delay for async state: use `TestProbe.ExpectMsg`/`AwaitAssert` or a
  bounded wait with a descriptive timeout.
- `Assert.Multiple` for grouped assertions on the same object; `Assert.Equal(expected, actual)` over
  `Assert.True(actual == expected)`.
- Use `FakeTimeProvider` or a fixed timestamp, never `DateTime.UtcNow`/`DateTimeOffset.UtcNow`.
- Prefer `Theory`/`InlineData`/`MemberData` for small input/output sweeps; separate named `[Fact]`s
  when each case needs a large distinct fixture.
- Endpoint tests send a request to a mapped route and assert status and/or body.

## Metrics conventions

- One `Meter("Njord.<Domain>")` per feature library (`Njord.Core`, `Njord.OpenMeteo`, `Njord.Budget`,
  `Njord.Pipeline`, `Njord.Mqtt`), owned by an `internal static class Telemetry` with
  `internal static readonly` instruments and a `MeterName` constant; `CoreSetupContainer` builds the
  meter allow-list from these constants (hence `InternalsVisibleTo Njord`), so a new meter is added
  there.
- Names are `njord_<area>_<name>` in snake_case; counters end in `_total`, time histograms in
  `_seconds`. Keep tag cardinality bounded. Metrics are exported via prometheus-net.

## References

- Open-Meteo API: https://open-meteo.com/en/docs
