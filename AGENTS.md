# AGENTS.md

## Project

njord - Multi-domain weather intelligence for Home Assistant. A .NET service (Docker container)
on Akka.NET + Akka.Streams: polls the Open-Meteo APIs (weather ensemble, air quality, flood,
marine) per location, evaluates feature rules on the data, and serves it to Home Assistant
over gRPC (the ha-njord integration).

## Solution structure

12 production projects, one test project each (except Host), plus `Njord.Tests.Shared`.

```
src/
  Njord.slnx
  Njord.Forecast/       # Vocabulary, no Njord refs; folders Forecast/ (DomainKind, Location, DomainForecast<T>),
                        #   Weather/ AirQuality/ Flood/ Marine/ Solar/ (XxxPoint + [Flags] XxxParam enums,
                        #   Point.HasFields(param)), Horizon/
  Njord.Messages/       # Akka-free contracts (-> Forecast): PipelineEvent, Fetch*/Poll*/Query* messages,
                        #   Alerts/, Budget/, Config/, Forecast/, Pipeline/ (FeatureResult, OutputKind)
  Njord.Persistence/    # Extend-only records, no Njord refs: Events/Budget, Events/Polling
  Njord.Core/           # (-> Forecast, Messages, Persistence)
    ActorKeys.cs        #   marker interfaces for every resolvable actor
    Actors/             #   IActorRegistration, Forecast/Pipeline stream handshakes shared across libraries
                        #   (actor plumbing used by one library lives there: ShardMessageExtractor in Polling,
                        #   StreamSupervision in Pipeline, GrpcStreamMessages in Grpc)
    Domain/             #   IFeatureRule, FeatureRule<TInput>, FeatureDescriptor, FeatureCategory, FeatureContext,
                        #   FeatureInputs (typed inputs), FeatureResolution, FetchPlan, DomainRequirements,
                        #   LocationForecast, RingBuffer, ForecastQueries, DomainDataReceived
    Configuration/      #   IConfigSection, NjordOptions (with FindLocation), LocationOptions, WeatherOptions, FloodOptions,
                        #   PollingOptions, Health/HealthOptions, AddNjordOptions (+ validators)
    Polling/            #   CallWeight, RequestBudget (free-tier constants, DailyLimit), FetchTimeouts
  Njord.OpenMeteo/      # (-> Core) folders per domain: Weather/, AirQuality/, Flood/, Marine/, Seasonal/, Satellite/
                        #   (client, API variable enum, response DTO, mapping extensions per domain),
                        #   Gateway/ (OpenMeteoGateway, stream-based dispatch), Budget/ (GatewayBudgetGate),
                        #   Http/ (ClientRequest, OpenMeteoEndpoints, OpenMeteoJsonContext),
                        #   Mapping/ (shared: DomainKindMappingExtensions, ForecastTrimmingExtensions, SeriesAccess),
                        #   Configuration/ (AirQuality/Marine/Seasonal/Satellite/SolarYield options)
  Njord.Budget/         # BudgetTracker (persistent), usage accounting, health check
  Njord.Polling/        # PollManager, PollWorker (+ state, mappings), polling Telemetry, Configuration/ (BudgetGuard, PollingOptionsValidator)
  Njord.Pipeline/       # Stream/ (ForecastPipeline, FeatureEngine), Health/ (PipelineHealthCheck), Telemetry
  Njord.Features/       # (-> Core) every feature: <OutputKind>/<Feature>/ with FeatureRule + <Name>Config (+ validator)
                        #   side by side (Alerts/ Scores/ Conditions/ Trends/ Forecasts/), <Kind>/Shared/ and top-level
                        #   Shared/ (Scoring), ServiceCollectionExtensions.AddFeatures; typed inputs stay in Core
  Njord.Grpc/           # WeatherGrpcService, OpsGrpcService, ActorAsk, GrpcConsumer, Mapping/
  Njord/                # Host: Program.cs, NjordHost, Configuration/ (setup containers, PersistenceOptions), Health/

  Njord.Forecast.Tests/ Njord.Messages.Tests/ Njord.Persistence.Tests/ Njord.Core.Tests/
  Njord.OpenMeteo.Tests/ Njord.Budget.Tests/ Njord.Pipeline.Tests/ Njord.Polling.Tests/
  Njord.Features.Tests/ Njord.Grpc.Tests/
  Njord.IntegrationTests/       # whole host via NjordHost + TestServer
  Njord.Architecture.Tests/     # ArchUnitNET rules (ArchitectureTests)
  Njord.Tests.Shared/           # small shared fakes and builders (not a test project)
protos/njord/v2/                # gRPC contracts: weather.proto, ops.proto, common.proto
docs/                           # VitePress site incl. the Config Builder (docs/.vitepress/theme/builder)
```

Reference direction: Forecast <- Messages; Forecast/Messages/Persistence <- Core <- feature libraries
(OpenMeteo, Budget, Polling, Pipeline, Features, Grpc) <- Host. Feature
libraries reference only `Njord.Core` (and below), never each other, never the Host; they reach each
other's actors through the markers in `ActorKeys.cs`. Registration stays central in the Host.

## Architecture guardrails

- **Feature-driven config.** The operator sets locations and feature toggles; the rest is derived.
  Fetched domains = union of the `Requirements` of the enabled features (`FeatureResolution`).
- **Every feature is a `FeatureRule<TInput>`** (an `IFeatureRule`) in `Njord.Features/<OutputKind>/<Feature>/`: a
  `FeatureDescriptor` (`Output` (`OutputKind`), `Category`, `Name` (the config section name), `DefaultEnabled`, `Requirements`: a `DomainRequirements` of
  per-domain `XxxParams` flags, `PollDepth`), `Enabled` from its own config, `Project(LocationForecast)` to a typed
  input (null skips the rule) and `Evaluate(FeatureContext<TInput>)` returning one result event or null. Required
  flags gate dispatch (missing data skips the rule), `Optional*` flags are requested from the API but never gate.
  `FeatureEngine` runs only the enabled rules whose domain got new data and hands them a ring of the last
  `PollDepth + 1` forecasts for trends.
- **Every feature owns its config class** `<Name>Config : IConfigSection` with `SectionName`
  `Njord:Features:<Category>:<Name>`, `Enabled` and its options (thresholds); an optional `<Name>ConfigValidator`.
  Registered in `AddFeatures` next to the rule. Names are unique per category; the description lives only in
  `docs/data/features.json` (hand-maintained, with the options and defaults).
- **Feature libraries are plain libraries.** `Njord.Features` references only Core and below; no feature project
  references another or an adapter (Grpc).
- **One `PollWorker` per location, per-domain independent timers.** No rounds, no waiting for all
  domains; each domain learns its own update cycle (Discovery -> learned cycle with miss tolerance).
- **Streams for data flow, actors for lifecycle; the stable side owns the hubs.** Domain data and
  pipeline events travel as Akka.Streams (StreamRefs); requests, outcomes and scheduling as messages.
- **Gateway is request/response.** `FetchDomain` gets exactly one `FetchDomainCompleted` (hash only),
  `FetchDomainFailed` or `FetchDomainRejected`; data goes to the pipeline through a SinkRef branch.
- **Pipeline owns both hubs** (`MergeHub` in, `BroadcastHub` out). All analysis state lives in the
  `FeatureEngine` closure, so a gateway restart loses nothing.
- **Never `Zip` domain sub-streams.** Domains arrive independently; rules see the current
  `LocationForecast` and, through the ring, earlier ones for trends.
- **Consumers are output adapters via SourceRef** (`GrpcConsumer`); no relay
  actors. No consumer slows the hub: bounded drop-oldest buffers.
- **gRPC is an adapter.** Resolve a marker, `ActorAsk` with a `Njord.Messages` message, map with `ToProto()`.
- **`Njord.Messages` is Akka-free** (refs Forecast only); stream handshakes live in `Njord.Core/Actors`.
- **Rules emit typed `FeatureResult`s** (`AlertResult`, `ScoreResult`, `ConditionResult`, `TrendResult`,
  `ForecastResult` and their feature-specific records); gRPC delivers each as an `EnrichmentEvent` whose `feature`
  oneof case identifies the feature (`weather.proto`).
- **`TimeProvider` everywhere**, never `DateTime.Now`/`UtcNow`.
- **Zone rules are tests** (`ArchitectureTests`): Forecast/Messages/Persistence reference no other Njord
  project (Messages only Forecast), no Akka in Messages/Persistence, domain projects independent, all classes sealed.

## Actor topology

```
SINGLETONS (Cluster Singleton)
├─ PollManager (non-persistent, BackoffOnFailure, IWithTimers)    [Polling]
│  └─ one target per location; StartPolling/StopPolling, poll states + health from reports
├─ OpenMeteoGateway (non-persistent)                              [OpenMeteo]
│  └─ GatewayBudgetGate -> typed clients: Weather, AirQuality, Flood, Marine, Seasonal, SatelliteRadiation
├─ ForecastPipeline (non-persistent)                              [Pipeline]
│  └─ MergeHub<DomainDataReceived> -> FeatureEngine -> IFeatureRule[] -> BroadcastHub<PipelineEvent>
├─ BudgetTracker (persistent)                                     [Budget]
└─ GrpcConsumer (SourceRef consumer of the pipeline)              [Grpc]

SHARD REGIONS (Sharded Entity)
└─ PollWorker (persistent, entity = location, "poll-worker", no passivation)  [Polling]
   └─ per-domain timers -> FetchDomain -> OpenMeteoGateway; persists DomainPolled; reports to PollManager
```

## Decisions

- **Ensemble default.** The weather request goes to `v1/ensemble`; `Njord:Weather:Models` (default
  `ecmwf_ifs025_ensemble_mean`, first = primary; ids from `WeatherModelCatalog`) selects the ensemble-mean models,
  there are no horizon or parameter settings. Models beyond the first only take effect with the
  `Weather:ModelComparison` feature (`EffectiveWeatherModels`); rules run on the primary model only and
  the weather call weight is multiplied by the effective model count. The requested variables follow the enabled features: the `Requirements` of all
  enabled features are OR-ed (`FeatureResolution.RequiredParams`) and mapped to API names by
  `ParamMappings.ToApiNames()`; the clients only build the URL. `FeatureEngine` skips a rule whose
  required fields are missing from the stream (`LocationForecast.HasFields`). `BudgetGuard` uses the same
  resolved requirements: Weather costs `ceil(variables / 10) * ceil(forecastDays / 14) * modelCount` calls per cycle,
  every other required domain 1.
- **Per-domain forecast days.** `Njord:AirQuality` (5, max 7), `Njord:Marine` (8, max 8), `Njord:Flood` (92,
  max 210) next to `Njord:Weather`.
- **Feature-driven configuration.** One toggle per feature in `Njord:Features:<Category>:<Name>` (37 features; categories see
  `docs/data/features.json`; env `Njord__Features__<Category>__<Name>__Enabled`). 18 are on by default: the 14
  weather alerts (`Weather:Frost`, `Heat`, `Storm`, `HeavyRain`, `Uv`, `Fog`, `Snow`, `PressureDrop`, `Thunderstorm`,
  `Ice`, `WindChill`, `Visibility`, `TropicalNight`, `Humidity`), `Lifestyle:Commute`, `AirQuality:Pollen`,
  `AirQuality:Pm25`; everything else is off. The entity set follows locations x enabled features.
- **Per-domain discovery.** Every domain of a location learns its own cycle and polls independently.
- **Domains:** `Weather`, `AirQuality`, `Flood`, `Marine`, `Seasonal`, `Satellite` (`DomainKind`); the gateway fetches all of them.
- **Explicit coordinates.** A location is `Name`, `Latitude` and `Longitude`, all required (`NjordOptionsValidator`).
- **Free tier assumed** (non-commercial): budget defaults to 300k/month, 600/min, optional
  `Polling:BudgetOverride`. Startup validates projected usage (`BudgetGuard`: locations x required
  domains x poll cycles x call weight) against 80 % of the monthly budget.
- **Any change to polling needs a budget estimate** against 300k/month, 10k/day, and politeness toward
  a free service. Weather weighs `ceil(variables / 10)` calls (4 standard and with solar); others 1.
- **Seasonal is a normal domain** (`DomainKind.Seasonal`, `SeasonalPoint`, `TimeResolution.Monthly`): the gateway fetches
  `seasonal-api.open-meteo.com` for `SeasonalOutlook`, the PollWorker learns its roughly daily cycle, `BudgetGuard`
  counts it as 30 calls/month/location instead of once per poll cycle. The API delivers ensemble-mean
  `temperature_2m_anomaly` (K) and `precipitation_anomaly` (mm; `SeasonalOutlook` converts to % of the normal
  month with `precipitation_mean`); confidence follows the lead time. `Njord:Seasonal:ForecastMonths` (3) limits the months kept.
- **Satellite is a normal domain** (`DomainKind.Satellite`, `SatelliteRadiationPoint` in `Njord.Forecast/Solar`): the gateway
  fetches `satellite-api.open-meteo.com/v1/archive` (GHI, direct, diffuse, DNI, GTI; tilt/azimuth from `SolarYieldOptions`,
  `Njord:Satellite:PastHours`) for `SatelliteRadiation` (off by default, about 720 calls/month/location, counted per poll
  cycle by `BudgetGuard`). `SolarIrradianceBlend` takes satellite GTI up to the newest observation (ignored when
  older than 3 h) and ensemble GTI afterwards; `SolarYield` and `SatelliteRadiation` use it.
- **Base parameters.** `FeatureResolution.WithBaseParams` always requests `weather_code` for a required Weather domain and `pm10` for a required AirQuality domain; `BudgetGuard` and the Config Builder count them.
- **Failure backoff.** A failed fetch retries after 60 s, doubling per consecutive failure up to the poll interval (`FailureBackoff`); rate limit and budget rejections wait at least 5 min. The count is persisted in `DomainPolled.ConsecutiveFailures` and reset by a successful fetch.
- Poll interval 60 min and discovery interval 20 min are config values.
- Wire text for closed sets comes from explicit mappings, never `ToString()`.
- Feels-like is `apparent_temperature` from the API, no own computation.
- Thresholds live in the feature's own config (`Njord:Features:<Category>:<Name>`, e.g. three-step severity arrays), validated by its `<Name>ConfigValidator`.
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

All projects sequentially (each is its own process; parallel runs slow the actor tests):

```bash
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
```

After any change run ALL test projects. Fix stale AGENTS.md/CLAUDE.md content as soon as you notice it.

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
  default throws `ArgumentOutOfRangeException` (`ToProto()`, `ToDomain()`, `ToApiVariable()`). No casts
  between enums, no `Enum.Parse`/`ToString().ToLower()`. Hand-written wire mappings get one Theory per
  direction (`<Name>MappingTests`); the exhaustive switch covers completeness at compile time.
  API variable string names use `[JsonStringEnumMemberName]` on per-domain API enums in `Njord.OpenMeteo`
  with a cached `FrozenDictionary` lookup (`ApiVariableNames`), outside the switch convention.
- Every static class with `this` methods ends in `Extensions` and lives in a file of the same name; state
  extensions live next to, not inside, the state record. This is a review convention, not an ArchUnit rule.
- Mappings are extension methods in static classes named `<Domain><Target>MappingExtensions` (file name = class
  name): no state, never in actors; names `ToProto`, `ToDomain`, `ToMessage`, never `Map*`.
  Protobuf timestamps via `ToTimestamp()`.
- No em dashes in code, docs or commit messages.

### Akka

Rules apply to production code.

- **No `ContinueWith`.** Use `PipeTo` with `success:` and `failure:`. The mappers are pure; log in the
  result handler, not in the mapper.
  ```csharp
  gateway.Ask<FetchDomainResponse>(new FetchDomain(location, domain), timeout).PipeTo(Self,
      success: reply => new DomainFetchReplied(domain, reply),
      failure: ex => new DomainFetchReplied(domain, AskFailure(location, domain, timeout, ex)));
  ```
- **No `Status.Failure`/`Status.Success`.** Use `XxxFailed(Exception Cause)` (plus request id when
  correlated); the requester must handle it.
- **No `EventStream`.** Use explicit actor references. TestKit probes may subscribe in tests.
- **No `IActorRef` constructor parameters.** Resolve by marker: `Context.TryGetActor<IPollManager>(out var m)`.
- **Messages:** commands `VerbNoun` with `VerbNounCompleted`/`VerbNounFailed` (and `Rejected` where the
  domain has it) in one file; queries `QueryNoun` with `NounResult`/`NounFailed`; events past tense in
  `Njord.Persistence`. `Failed` always carries `Exception Cause`.
- **Naming:** `*Manager` for a coordinating actor (Cluster Singleton), `*Worker` for an actor per entity or per
  request (sharded entity or child), other actors take a domain-role name (`BudgetTracker`, `OpenMeteoGateway`,
  `ForecastPipeline`). All sealed. `*State` is reserved for immutable state records.
- **State:** a stateful actor has `<Actor>.cs` (plumbing: routing, persistence lifecycle, timers) and
  `<Actor>State.cs` (immutable record, never nested; `Empty` static; `Apply(event)` returns a new state;
  `ProcessCommand` returns `(State, Event)` for persistent actors). Logic lives in state extensions.
  State is never sent or snapshotted raw: callers get a message record, `SaveSnapshot` gets
  `Persisted<Name>State`.
- **Sharding:** one `ShardMessageExtractor` in `Njord.Polling` matching `IWith*Id` interfaces, never concrete
  types. Passivation only through `ShardOptions.PassivateIdleEntityAfter`, never `SetReceiveTimeout` +
  `Passivate`. (PollWorker opts out of passivation.)
- **Logging:** `private readonly ILoggingAdapter _log = Context.GetLogger();` (inline, readonly, `_log`).
  `ActorCategoryLogger` writes the class's full type name as category; `akka.loglevel` follows the
  lowest `Serilog:MinimumLevel`.
- **Persistence:** records in `Njord.Persistence` are positional, `DateTimeOffset`/`TimeSpan` (never ticks),
  past-tense events (`DomainPolled`), `Persisted<Name>State` snapshots, new members nullable or defaulted,
  closed sets as `Persisted*` enums with explicit values, no `Event`/`Dto` suffix, no custom serializer.
  State <-> record mapping in `<Owner>PersistenceMappingExtensions`, state -> contract in
  `<Owner>MessageMappingExtensions`. "Snapshot" is reserved for persisted state. `Apply` takes the event
  record. Snapshot when `LastSequenceNr % SnapshotInterval == 0`, never after every event; after
  `SaveSnapshotSuccess`: `DeleteMessages(seqNr)` + `DeleteSnapshots(seqNr - 1)`. `DeferAsync` only for
  non-trivial side effects and never when the handler persists internally. Bounded-lifecycle entities
  need no snapshots.
- **Health checks ask the actors** (`PipelineHealthCheck` -> `PollManager`, `BudgetHealthCheck` ->
  `BudgetTracker`) with a 3 s timeout, degrade when the
  actor is missing or silent, and keep the rule in an `internal static Evaluate(...)`. The host adds
  `journal`. `/healthz`: 200 Healthy/Degraded, 503 Unhealthy; `/alive` is unconditional.

### Tests

- Class suffix `Tests`, `public sealed`. Layout mirrors production 1:1: `Foo.cs` -> `FooTests.cs`,
  `FooState.cs` -> `FooStateTests.cs`. Topic splits (`FooRetryTests`) only when one file gets large.
  Names are `Snake_case_sentences` describing behavior.
- Actor tests derive from `Akka.TestKit.Xunit.TestKit`. Collaborators are `CreateTestProbe()` probes
  registered in `ActorRegistry.For(Sys).Register<IMarker>(probe)` (register every marker the actor
  resolves); many probes go in a `TestProbes` record. Create the actor with
  `Sys.ActorOf(Props.Create(() => new X(...)))`; never `resolver.Props<T>()`, never an `ActorSystem` by hand.
  Never reuse an actor name after `GracefulStop`.
- State records are tested as plain xUnit `<Name>StateTests` (3-5 facts: Apply, ProcessCommand,
  snapshot round trip); actors test message routing and lifecycle only.
- Waits: `ExpectMsg`/`probe.ExpectMsg`/`AwaitAssert`/`AwaitCondition`; `ExpectNoMsg(200 ms)` for absence.
  No `Thread.Sleep`, no arbitrary delay. Retry-backoff tests pass a short `BackoffPolicy`.
- `FakeTimeProvider` or a fixed timestamp, never `DateTime.UtcNow`.
- NSubstitute only for plain interfaces (never actors, use probes): `Received(1)` with `Arg.Is`.
- Assertions: `Assert.NotNull` before nullable access (no `!.`, except `JsonNode` indexers); `Assert.Single`;
  count before indexing; assert the exact value; no vacuous assertion after a proven value;
  `var ex = Assert.Throws<T>(...)` then assert on `ex`; `Assert.IsType<T>(obj, exactMatch: false)`;
  `Assert.Multiple` for groups; `Assert.Equal` over `Assert.True(a == b)`. Pass
  `TestContext.Current.CancellationToken` in async tests (xUnit1069 is an error).
- `Theory` for small sweeps, separate named `[Fact]`s for large distinct fixtures. Endpoint tests send a
  request and assert status/body. No data-only, constructor, property or DI-wiring tests.
- `Njord.IntegrationTests` use a fixture with probes, like FunkArr. `Njord.Tests.Shared` stays small
  (options monitor, builders, fakes). Verify snapshots only for persistence records.
- After any change run ALL test projects, not only the affected ones (snapshot tests catch breaks elsewhere).

### Metrics

- One `Meter("Njord.<Domain>")` per library (`OpenMeteo`, `Budget`, `Polling`, `Pipeline`) in an
  `internal static class Telemetry` with `MeterName` and `internal static readonly` instruments;
  `CoreSetupContainer.NjordMeters` is built from the constants, so a new meter is added there.
- Names `njord_<area>_<name>` in snake_case; counters end `_total`, time histograms `_seconds`. Bounded tag
  cardinality. Exported via prometheus-net.

## Options

One options class per concern with its own `SectionName`, implementing `IConfigSection` (`static abstract SectionName`), registered with `AddNjordOptions<T>()`
(`AddOptions<T>().BindConfiguration(T.SectionName).ValidateOnStart()`) or `AddNjordOptions<T, TValidator>()` when it has
cross-field rules (explicit `IValidateOptions<T>`, failures collected with context). Options live with their owner
(Njord/Weather/Flood/Polling/Health options in `Njord.Core`, AirQuality/Marine/Seasonal/Satellite/SolarYield options in `Njord.OpenMeteo`, `PollingOptionsValidator` and `BudgetGuard` in
`Njord.Polling`, `PersistenceOptions` in the Host, feature options in each `<Name>Config`).
Defaults are property initializers.

| Section | Class | Content |
|---------|-------|---------|
| `Njord` | `NjordOptions` | `Locations` (`Name`, `Latitude`, `Longitude`, all required) |
| `Njord:Weather` | `WeatherOptions` | `ForecastDays` (4, 1..16), `Models` (`ecmwf_ifs025_ensemble_mean`; known ids in `WeatherModelCatalog`) |
| `Njord:AirQuality` | `AirQualityOptions` | `ForecastDays` (5, 1..7) |
| `Njord:Marine` | `MarineOptions` | `ForecastDays` (8, 1..8) |
| `Njord:Seasonal` | `SeasonalOptions` | `ForecastMonths` (3, 1..6) |
| `Njord:Satellite` | `SatelliteOptions` | `PastHours` (6, 1..48) |
| `Njord:Flood` | `FloodOptions` | `ForecastDays` (92, 1..210), `IncludeEnsemble` (false) |
| `Njord:Features:<Category>:<Name>` | `<Name>Config` | `Enabled` (default from the descriptor) plus the feature's options |
| `Njord:SolarYield` | `SolarYieldOptions` | `PeakKw` (5.0), `Azimuth` (180), `Tilt` (30) |
| `Njord:Polling` | `PollingOptions` | `PollInterval` (60 min), `DiscoveryInterval` (20 min), `BudgetOverride` |
| `Njord:Persistence` | `PersistenceOptions` | `Provider` (Sqlite/PostgreSql), `ConnectionString`, `DataPath` |
| `Njord:Health` | `HealthOptions` | poll overdue/stalled factors, budget degraded % |

The Config Builder (`docs/.vitepress/theme/builder`: `types.ts`, `budget.ts`, `serializer.ts`,
`ConfigBuilder.vue`, data in `docs/data/features.json` (hand-maintained, incl. per-feature options)) mirrors the toggles, their required domains, per-feature marginal cost and the
`BudgetGuard` formula; change them together with the options and `docker-compose.example.yml`.

## References

- Open-Meteo API: https://open-meteo.com/en/docs
- Weather ensemble: `https://api.open-meteo.com/v1/ensemble`; air quality, flood and
  marine each use their own host (`air-quality-api`, `flood-api`, `marine-api` `.open-meteo.com`). No API key. Free tier non-commercial: 600/min, 5k/h, 10k/day, 300k/month.
- Every URL is built by `ClientRequest` (`QueryHelpers`): invariant-culture coordinates, `timeformat=unixtime`,
  `timezone=UTC`. Wind in m/s. Values beyond a horizon are `null`; HTTP 400 carries
  `{"error":true,"reason":...}`.
