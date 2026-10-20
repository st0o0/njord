# AGENTS.md

## Project

njord - Multi-domain weather intelligence for Home Assistant. A .NET service (Docker container)
on Akka.NET + Akka.Streams: polls the Open-Meteo APIs (weather ensemble, air quality, flood,
marine) per location, enriches the data via `IDomainHandler`s, and serves it to Home Assistant
over gRPC (the ha-njord integration).

## Solution structure

15 production projects, one test project each (except Host), plus `Njord.Tests.Shared`.

```
src/
  Njord.slnx
  Njord.Forecast/       # Vocabulary, no Njord refs; folders Forecast/ (DomainKind, Location, DomainForecast<T>),
                        #   Weather/ AirQuality/ Flood/ Marine/ Solar/ (XxxPoint + [Flags] XxxParam enums,
                        #   Point.HasFields(param)), Horizon/
  Njord.Messages/       # Akka-free contracts (-> Forecast): PipelineEvent, Fetch*/Poll*/Query* messages,
                        #   Alerts/, Budget/, Config/, Forecast/, Pipeline/ (LifestyleMappings)
  Njord.Persistence/    # Extend-only records, no Njord refs: Events/Budget, Events/Polling
  Njord.Core/           # (-> Forecast, Messages, Persistence)
    ActorKeys.cs        #   marker interfaces for every resolvable actor
    Actors/             #   ShardMessageExtractor, StreamSupervision, stream handshake messages
    Domain/             #   IDomainHandler, IAlertRule, FeatureKind, FeatureCategory,
                        #   FeatureDescriptor, FeatureResolution, LocationForecast, DomainDataReceived
    Configuration/      #   NjordOptions, LocationOptions, WeatherOptions, FeatureOptions, AlertOptions,
                        #   SolarYieldOptions, WeatherVariables, ConfigView, AddNjordOptions + validators
      Polling/ Persistence/ Health/   # PollingOptions, BudgetCalculator, FetchTimeouts, ...
  Njord.OpenMeteo/      # (-> Core) OpenMeteoGateway, GatewayBudgetGate, 5 typed clients (Weather, AirQuality, Flood, Marine, Seasonal), OpenMeteoRequestBuilder,
                        #   response DTOs, Mapping/
  Njord.Budget/         # BudgetTracker (persistent), usage accounting, health check
  Njord.Polling/        # PollManager, PollWorker (+ state, mappings), polling Telemetry, PollingSetupContainer
  Njord.Pipeline/       # Stream/ (ForecastPipeline, ProjectionEngine), Health/ (PipelineHealthCheck), Telemetry
  Njord.Weather/        # WeatherAlertHandler + Rules/ (one per alert type), Lifestyle/ handlers
  Njord.AirQuality/     # AirQualityAlertHandler (pollen, PM2.5), PollenTrendHandler
  Njord.Flood/          # FloodRiskHandler, FloodRainHandler, FloodTimelineHandler
  Njord.Marine/         # SwimmingConditionsHandler
  Njord.Grpc/           # WeatherGrpcService, OpsGrpcService, ActorAsk, GrpcConsumer, Mapping/
  Njord/                # Host: Program.cs, NjordHost, Configuration/ (setup containers), Health/

  Njord.Forecast.Tests/ Njord.Messages.Tests/ Njord.Persistence.Tests/ Njord.Core.Tests/
  Njord.OpenMeteo.Tests/ Njord.Budget.Tests/ Njord.Pipeline.Tests/ Njord.Polling.Tests/
  Njord.Weather.Tests/
  Njord.AirQuality.Tests/ Njord.Flood.Tests/ Njord.Marine.Tests/ Njord.Grpc.Tests/
  Njord.IntegrationTests/       # whole host via NjordHost + TestServer
  Njord.Architecture.Tests/     # ArchUnitNET rules (ArchitectureSpec) + FeatureConfigKeySpec
  Njord.Tests.Shared/           # fakes, builders, fixtures, NjordActorSpec (not a test project)
protos/njord/v2/                # gRPC contracts: weather.proto, ops.proto, common.proto
docs/                           # VitePress site incl. the Config Builder (docs/.vitepress/theme/builder)
```

Reference direction: Forecast <- Messages; Forecast/Messages/Persistence <- Core <- feature libraries
(OpenMeteo, Budget, Polling, Pipeline, Weather, AirQuality, Flood, Marine, Grpc) <- Host. Feature
libraries reference only `Njord.Core` (and below), never each other, never the Host; they reach each
other's actors through the markers in `ActorKeys.cs`. Registration stays central in the Host.

## Architecture guardrails

- **Feature-driven config.** The operator sets locations and feature toggles; the rest is derived.
  Fetched domains = union of the `Requirements` of the enabled features (`FeatureResolution`).
- **Every feature is a `FeatureKind`** with a `FeatureDescriptor` (`Kind`, `Category`, `Description`,
  `DefaultEnabled`, `Requirements`: a `DomainRequirements` of per-domain `XxxParam` flags). An `IDomainHandler`
  exposes its `Feature` and handles `Handle(current, previous, time)`; required flags gate dispatch (missing
  data skips the handler), `Optional*` flags are requested from the API but never gate. Each `IAlertRule`
  declares its own feature; `WeatherAlertHandler` (an `IMultiFeatureHandler`) runs only the enabled rules.
  The config key is `FeatureKind.ToConfigKey()` (explicit switch); `FeatureConfigKeySpec` pins one
  descriptor per kind.
- **Domain projects are plugins.** Weather/AirQuality/Flood/Marine register handlers via
  `Add<Domain>Domain()`; they never reference each other or the adapters (Grpc).
- **One `PollWorker` per location, per-domain independent timers.** No rounds, no waiting for all
  domains; each domain learns its own update cycle (Discovery -> learned cycle with miss tolerance).
- **Streams for data flow, actors for lifecycle; the stable side owns the hubs.** Domain data and
  pipeline events travel as Akka.Streams (StreamRefs); requests, outcomes and scheduling as messages.
- **Gateway is request/response.** `FetchDomain` gets exactly one `FetchDomainCompleted` (hash only),
  `FetchDomainFailed` or `FetchDomainRejected`; data goes to the pipeline through a SinkRef branch.
- **Pipeline owns both hubs** (`MergeHub` in, `BroadcastHub` out). All analysis state lives in the
  `ProjectionEngine` closure, so a gateway restart loses nothing.
- **Never `Zip` domain sub-streams.** Domains arrive independently; handlers see the current
  `LocationForecast` and `Previous` for trends.
- **Consumers are output adapters via SourceRef** (`GrpcConsumer`); no relay
  actors. No consumer slows the hub: bounded drop-oldest buffers.
- **gRPC is an adapter.** Resolve a marker, `ActorAsk` with a `Njord.Messages` message, map with `ToProto()`.
- **`Njord.Messages` is Akka-free** (refs Forecast only); stream handshakes live in `Njord.Core/Actors`.
- **Lifestyle events share one wire shape**: `ToLifestyleUpdate()` flattens them to `LifestyleUpdate`
  (`LifestyleKind` + string map) for gRPC (`type_name = "lifestyle"`).
- **`TimeProvider` everywhere**, never `DateTime.Now`/`UtcNow`.
- **Zone rules are tests** (`ArchitectureSpec`): Forecast/Messages/Persistence reference no other Njord
  project (Messages only Forecast), no Akka in Messages/Persistence, domain projects independent, all classes sealed.

## Actor topology

```
SINGLETONS (Cluster Singleton)
├─ PollManager (non-persistent, BackoffOnFailure, IWithTimers)    [Polling]
│  └─ one target per location; StartPolling/StopPolling, poll states + health from reports
├─ OpenMeteoGateway (non-persistent)                              [OpenMeteo]
│  └─ GatewayBudgetGate -> typed clients: Weather, AirQuality, Flood, Marine, Seasonal
├─ ForecastPipeline (non-persistent)                              [Pipeline]
│  └─ MergeHub<DomainDataReceived> -> ProjectionEngine -> IDomainHandler[] -> BroadcastHub<PipelineEvent>
├─ BudgetTracker (persistent)                                     [Budget]
└─ GrpcConsumer (SourceRef consumer of the pipeline)              [Grpc]

SHARD REGIONS (Sharded Entity)
└─ PollWorker (persistent, entity = location, "poll-worker", no passivation)  [Polling]
   └─ per-domain timers -> FetchDomain -> OpenMeteoGateway; persists DomainPolled; reports to PollManager
```

## Decisions

- **Ensemble default.** The weather request goes to `v1/ensemble`; there are no model, horizon or
  parameter settings. The requested variables follow the enabled handlers: the `Requirements` of all
  enabled handlers are OR-ed (`FeatureResolution.RequiredParams`) and mapped to API names by
  `ParamMappings.ToApiNames()`; the clients only build the URL. `ProjectionEngine` skips a handler whose
  required fields are missing from the stream (`LocationForecast.HasFields`). `BudgetCalculator` uses the same
  resolved requirements: Weather costs `ceil(variables / 10) * ceil(forecastDays / 14)` calls per cycle,
  every other required domain 1.
- **Per-domain forecast days.** `Njord:AirQuality` (5, max 7), `Njord:Marine` (8, max 8), `Njord:Flood` (92,
  max 210) next to `Njord:Weather`.
- **Feature-driven configuration.** One toggle per `FeatureKind` in `Njord:Features:<Name>` (34 features in the
  categories WeatherAlerts, AirQuality, Lifestyle, Insight, Energy, Flood, Marine). On by default: the 14 weather
  alerts (`FrostAlert`, `HeatAlert`, `StormAlert`, `RainAlert`, `UvAlert`, `FogAlert`, `SnowAlert`,
  `PressureDropAlert`, `ThunderstormAlert`, `IceAlert`, `WindChillAlert`, `VisibilityAlert`,
  `TropicalNightAlert`, `HumidityAlert`), `CommuteSafety`, `PollenAlert`, `Pm25Alert`; everything else is off.
  The old `Njord:Enrichments` section is not read. The entity set follows locations x enabled features.
- **Per-domain discovery.** Every domain of a location learns its own cycle and polls independently.
- **Domains:** `Weather`, `AirQuality`, `Flood`, `Solar`, `Marine` (`DomainKind`). The gateway fetches
  Weather, AirQuality, Flood, Marine, Seasonal; `Solar` exists in the model and event vocabulary only.
- **Explicit coordinates.** A location is `Name`, `Latitude` and `Longitude`, all required (`NjordOptionsValidator`).
- **Free tier assumed** (non-commercial): budget defaults to 300k/month, 600/min, optional
  `Polling:BudgetOverride`. Startup validates projected usage (`BudgetCalculator`: locations x required
  domains x poll cycles x call weight) against 80 % of the monthly budget.
- **Any change to polling needs a budget estimate** against 300k/month, 10k/day, and politeness toward
  a free service. Weather weighs `ceil(variables / 10)` calls (4 standard and with solar); others 1.
- **Seasonal is a normal domain** (`DomainKind.Seasonal`, `SeasonalPoint`, `TimeResolution.Monthly`): the gateway fetches
  `seasonal-api.open-meteo.com` for `SeasonalOutlook`, the PollWorker learns its roughly daily cycle, `BudgetCalculator`
  counts it as 30 calls/month/location instead of once per poll cycle. The API delivers ensemble-mean
  `temperature_2m_anomaly` (K) and `precipitation_anomaly` (mm; `SeasonalOutlookHandler` converts to % of the normal
  month with `precipitation_mean`); confidence follows the lead time. `Njord:Seasonal:ForecastMonths` (3) limits the months kept.
- Poll interval 60 min and discovery interval 20 min are config values.
- Wire text for closed sets comes from explicit mappings, never `ToString()`.
- Feels-like is `apparent_temperature` from the API, no own computation.
- Alert thresholds are `Njord:Enrichment:Alerts:*` (three-step severity arrays, `AlertOptionsValidator`).
- Options are read at startup (`IOptions<T>`); no hot reload, no runtime config writer.

## Build & test

All commands run from `src/` (where `global.json` lives):

```powershell
dotnet build Njord.slnx
dotnet format Njord.slnx --verify-no-changes
```

Tests are xUnit v3 on Microsoft.Testing.Platform - `dotnet run`, **not** `dotnet test`:

```powershell
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj -- -class "<FullyQualifiedName>"
```

All projects sequentially (each is its own process; parallel runs slow the actor specs):

```bash
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
```

Current total: 919 tests (Forecast 68, Messages 69, Persistence 14, Core 155, OpenMeteo 134, Budget 18,
Polling 103, Pipeline 51, Weather 122, AirQuality 20, Flood 28, Marine 63, Grpc 41, Architecture 16,
IntegrationTests 17). One test per `[Fact]` and per `[Theory]` data row.

Run the service from `src/Njord/` (`dotnet run`). Layers: `appsettings.json` (logging, Kestrel only),
`appsettings.Development.json`, environment variables (`Njord__Persistence__DataPath`), `appsettings.Example.json`
(template), `docker-compose.example.yml` (repo root).

Other checks (repo root, local only): `openspec validate --all --no-interactive`,
`bash scripts/check-openspec-root.sh`, mutation testing `bash scripts/run-mutation.sh <Project>`.
Full-stack verification is the `e2e-test` skill (`.claude/skills/e2e-test/SKILL.md`, `e2e/E2E-TEST-PLAN.md`).

## Conventions

- OpenSpec lives only in `<repo root>/openspec`. Never delete `openspec/changes/archive`.
- **Git: NEVER `git push`**, the user pushes. Conventional Commits (commitlint), single-line message.
- Versioning is release-please; never edit `<Version>` in `src/Directory.Build.props`.
- Central package management: versions only in `src/Directory.Packages.props`; use `dotnet add package`.
- While 0.x, breaking changes are fine (`refactor!:`), including persistence records.

### C#

- `sealed` by default, records for messages/DTOs, nullable enabled. Style is enforced by `.editorconfig`.
- No XML docs. Never `async void`, `.Result`, `.Wait()`. Always pass `CancellationToken` through.
- `dotnet format` is enforced by CI; run it after editing `.cs` files.
- Enum to wire text or to another enum: extension method with an explicit `switch`, one arm per member,
  default throws `ArgumentOutOfRangeException` (`ToProto()`, `ToWireName()`). No casts between enums,
  no `Enum.Parse`/`ToString().ToLower()`. Each conversion has a spec pinning every member.
- Mappings are extension methods in static classes named `<Domain><Target>Mappings`: no state, never in
  actors; names `ToProto`, `ToDomain`, `ToMessage`, never `Map*`. Protobuf timestamps via `ToTimestamp()`.

### Akka

Rules apply to production code.

- **No `ContinueWith`.** Use `PipeTo` with `success:` and `failure:`.
- **No `Status.Failure`/`Status.Success`.** Use `XxxFailed(Exception Cause)` (plus request id when
  correlated); the requester must handle it.
  ```csharp
  gateway.Ask<FetchDomainResponse>(new FetchDomain(location, domain), timeout).PipeTo(Self,
      success: reply => new DomainFetchReplied(domain, reply),
      failure: ex => new DomainFetchReplied(domain, AskFailure(location, domain, timeout, ex)));
  ```
- **No `EventStream`.** Use explicit actor references. TestKit probes may subscribe in tests.
- **No `IActorRef` constructor parameters.** Resolve by marker: `Context.TryGetActor<IPollManager>(out var m)`.
- **Naming:** domain-role names, sealed; `*State` is reserved for immutable state records.
- **Logging:** `private readonly ILoggingAdapter _log = Context.GetLogger();` (inline, readonly, `_log`).
  `ActorCategoryLogger` writes the class's full type name as category; `akka.loglevel` follows the
  lowest `Serilog:MinimumLevel`.
- **Persistence:** records in `Njord.Persistence` are positional, `DateTimeOffset`/`TimeSpan` (never ticks),
  past-tense events (`DomainPolled`), `Persisted<Name>State` snapshots, new members nullable or defaulted,
  closed sets as `Persisted*` enums with explicit values. State <-> record mapping in
  `<Owner>PersistenceMappings`, state -> contract in `<Owner>MessageMappings`. "Snapshot" is reserved for
  persisted state. `Apply` takes the event record. Snapshot on `LastSequenceNr` interval; after
  `SaveSnapshotSuccess`: `DeleteMessages(seqNr)` + `DeleteSnapshots(seqNr - 1)`.
- **Health checks ask the actors** (`PipelineHealthCheck` -> `PollManager`, `BudgetHealthCheck` ->
  `BudgetTracker`) with a 3 s timeout, degrade when the
  actor is missing or silent, and keep the rule in an `internal static Evaluate(...)`. The host adds
  `journal`. `/healthz`: 200 Healthy/Degraded, 503 Unhealthy; `/alive` is unconditional.

### Tests

- `Spec` suffix, `sealed` classes, BDD-style names. Actor specs derive from `NjordActorSpec` (Akka.Hosting
  TestKit, in-memory persistence); never create an `ActorSystem` by hand.
- Async and actor tests carry `[Fact(Timeout = ...)]` and pass `TestContext.Current.CancellationToken`
  (xUnit1069 is an error). TestKit waits dilate by `akka.test.timefactor = 3`, so the xUnit timeout must
  exceed every wrapped wait: hosted specs use `TestTimeouts.Hosted`, bounded waits `AwaitAssertMax`,
  undilated waits `RealTimeWaitMax`. Never reuse an actor name after `GracefulStop`.
- Retry-backoff specs pass a short `BackoffPolicy` instead of waiting real seconds.
- `FakeTimeProvider` or a fixed timestamp, never `DateTime.UtcNow`. No `Thread.Sleep`/arbitrary delay:
  use `ExpectMsg`/`AwaitAssert`.
- `Assert.NotNull` before nullable access (no `!.`, except `JsonNode` indexers); `Assert.Single`; count
  before indexing; assert the exact value, not just existence; no vacuous assertion after a proven value.
- `var ex = Assert.Throws<T>(...)` then assert on `ex` fields. `Assert.IsType<T>(obj, exactMatch: false)`
  for interface conformance. `Assert.Multiple` for grouped assertions; `Assert.Equal` over `Assert.True(a == b)`.
- `Theory` for small sweeps, separate named `[Fact]`s for large distinct fixtures. Endpoint tests send a
  request and assert status/body. NSubstitute only when a plain interface needs faking (none yet).

### Metrics

- One `Meter("Njord.<Domain>")` per library (`Core`, `OpenMeteo`, `Budget`, `Polling`, `Pipeline`) in an
  `internal static class Telemetry` with `MeterName` and `internal static readonly` instruments;
  `CoreSetupContainer.NjordMeters` is built from the constants, so a new meter is added there.
- Names `njord_<area>_<name>` in snake_case; counters end `_total`, time histograms `_seconds`. Bounded tag
  cardinality. Exported via prometheus-net.

## Options

One options class per concern with its own `SectionName`, registered with `AddNjordOptions<T>(configuration)`
(binds the section, lets a configured list replace its default, registers `<T>Validator`, `ValidateOnStart`).
Defaults are property initializers.

| Section | Class | Content |
|---------|-------|---------|
| `Njord` | `NjordOptions` | `Locations` (`Name`, optional `Latitude`/`Longitude`) |
| `Njord:Weather` | `WeatherOptions` | `ForecastDays` (4) |
| `Njord:AirQuality` | `AirQualityOptions` | `ForecastDays` (5, 1..7) |
| `Njord:Marine` | `MarineOptions` | `ForecastDays` (8, 1..8) |
| `Njord:Seasonal` | `SeasonalOptions` | `ForecastMonths` (3, 1..6) |
| `Njord:Flood` | `FloodOptions` | `ForecastDays` (92, 1..210) |
| `Njord:Features` | `FeatureOptions` | one toggle per `FeatureKind`, default from its descriptor |
| `Njord:Enrichment:Alerts` | `AlertOptions` | alert thresholds |
| `Njord:SolarYield` | `SolarYieldOptions` | `PeakKw` (5.0), `Azimuth` (180), `Tilt` (30) |
| `Njord:Polling` | `PollingOptions` | `PollInterval` (60 min), `DiscoveryInterval` (20 min), `BudgetOverride` |
| `Njord:Persistence` | `PersistenceOptions` | `Provider` (Sqlite/PostgreSql), `ConnectionString`, `DataPath` |
| `Njord:Health` | `HealthOptions` | poll overdue/stalled factors, budget degraded % |

The Config Builder (`docs/.vitepress/theme/builder`: `types.ts`, `budget.ts`, `serializer.ts`,
`ConfigBuilder.vue`, data in `docs/data/features.json` (generated from the descriptors) and `docs/data/enrichment.json`
(alert thresholds)) mirrors the toggles, their required domains, per-feature marginal cost and the
`BudgetCalculator` formula; change them together with the options and `docker-compose.example.yml`.

## References

- Open-Meteo API: https://open-meteo.com/en/docs
- Weather ensemble: `https://api.open-meteo.com/v1/ensemble`; air quality, flood and
  marine each use their own host (`air-quality-api`, `flood-api`, `marine-api` `.open-meteo.com`). No API key. Free tier non-commercial: 600/min, 5k/h, 10k/day, 300k/month.
- Every URL is built with `OpenMeteoRequestBuilder`: invariant-culture coordinates, `timeformat=unixtime`,
  `timezone=UTC`. Wind in m/s. Values beyond a horizon are `null`; HTTP 400 carries
  `{"error":true,"reason":...}`.
