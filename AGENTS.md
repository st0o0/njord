# AGENTS.md

## Project

njord — Multi-model weather intelligence for Home Assistant. A .NET service
(Docker container) on Akka.NET + Akka.Streams: polls the Open-Meteo API for
multiple weather models per location, processes forecasts through an enrichment
pipeline (consensus, alerts, derived values, trends, indices),
and publishes Home Assistant entities via MQTT Discovery (Mosquitto broker on
the HA host).

### Architecture guardrails

- **Three zones that only meet in the domain model:** Ingest (Open-Meteo client,
  DTOs, parsing) → Domain (`Forecast`, analysis, consensus) → Consumers
  (MQTT state payloads, gRPC streaming). Ingest and consumers never reference
  each other.
- **Streams for data flow, actors for lifecycle.** The data flow spans two
  actors: `OpenMeteoGateway` (fetch) and `ForecastPipeline` (analysis),
  connected via `SourceRef<Forecast>`.
  **Gateway** owns: `Source.Queue<FetchTarget>` → `GatewayThrottleStage` (token
  bucket) → `SelectAsyncUnordered(2, fetch)` → `Sink.ActorRef(Self)` for
  correlation; successful forecasts feed a second `Source.Queue<Forecast>` →
  `BroadcastHub<Forecast>`. The `PollScheduler` sends `FetchForecast` and
  receives `ForecastFetched`/`ForecastFetchFailed` feedback.
  **Pipeline** receives a `SourceRef<Forecast>` from the Gateway and owns
  the analysis chain: `sourceRef.Source` → `ZipWith(sensors)` →
  `StatefulSelectMany(computeAll)` → `BroadcastHub<PipelineEvent>`. Sensor data
  enters via a `Source.ActorRef` + `ReuseLatest` side channel. All analysis
  state (`ModelSnapshot`, previous `ConsensusSnapshot`) lives inside the
  `StatefulSelectMany` closure, not in actor fields. Consumers attach via
  SourceRef.
- **The entity set is static, derived from config** (locations × models ×
  parameters × horizons) — never from what the API happens to return. A missing
  value is an `unavailable` state, not a missing entity.
- **Never `Zip` model sub-streams for consensus.** Aggregate per poll cycle
  (cycle id = tick timestamp) with timeout and quorum — consensus from whatever
  arrived, plus `models_used`/spread diagnostics.
- **`TimeProvider` everywhere**, never `DateTime.Now`/`UtcNow` directly.
- **SensorHub for external sensor input.** The SensorHub actor receives readings
  via gRPC (`SensorGrpcService`), stores latest values per `SensorKind` (closed enum).
  Analysis features pull sensor data at each poll cycle (latest-value, no
  reactive re-computation).
- **Each analysis feature is a self-contained library.** Feature libraries
  (Alerts, Derived, Trends, Indices) define compute logic and DI extensions.
  Each analyzer takes `ConsensusSnapshot` directly (plus per-feature extras:
  `Previous` for Trends, `SensorSnapshot` for Indices). No input interfaces
  or PollContext god-object. `PipelineEvent` variants are typed records
  (`ForecastReceived`, `ConsensusComputed`, `AlertComputed`, `DerivedComputed`,
  `TrendComputed`, `IndexComputed`) — no `object` wrapping. Pipeline
  orchestrates all feature libraries directly (`Njord.Pipeline` references
  analysis libraries).
- **Consumers are output adapters via SourceRef.** MQTT and gRPC consumers
  request a `SourceRef<PipelineEvent>` from `ForecastPipeline` and run their own
  sinks. No intermediate relay actors, no presenters, no discovery payloads.
- **Zone rules are enforced by tests** in `src/Njord.Architecture.Tests/` (lateral
  independence of the feature libraries, Domain independence,
  sealed/`Spec` conventions,
  `LayerReferenceSpec` for the assembly reference direction: Domain and
  Persistence reference no Njord assembly, Messages only Domain, Core only those
  three, feature libraries (OpenMeteo, Budget, SensorHub, Grpc, Pipeline, Mqtt,
  Alerts, Derived, Trends, Indices) only Core and below; the host references
  all. Feature libraries never reference each other, checked by the lateral
  rule in `ZoneArchitectureSpec`).

### Decisions

- Open-Meteo free tier (non-commercial) is the assumed plan: the request budget
  defaults to its soft limits (300k/month, 600/min) with an optional
  `BudgetOverride` for self-throttling. Startup validates projected usage
  against 80 % of the resolved monthly budget. Commercial use would need the
  paid tier (out of scope).
- Poll interval and request budget are config values (default 60 min). Any change
  that adds or alters polling needs a budget estimate against the free-tier soft
  limits (300k/month, 10k/day) — and politeness toward a free service.
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
- MQTT egress: device-based discovery (one retained config per device,
  `homeassistant/device/<id>/config`), one retained state JSON per device per
  cycle, availability via LWT on `njord/status` + per-component
  `availability_template` + `expire_after` (2× poll interval). MQTTnet sits
  behind the `IMqttTransport` seam; the connection actor owns lifecycle, HA
  birth handling, and tombstoning of stale retained configs.

## Language

All code, specs, docs, and communication in English.

## Solution structure

```
src/
  Njord.slnx

  # /Foundation/ — shared types, messages, persistence, computation, infrastructure
  Njord.Weather/               # Pure weather records (no Njord references)
  Njord.Sensors/               # Pure sensor records (no Njord references)
  Njord.Persistence/          # Persistence DTOs (extend-only; no Njord references)
  Njord.Messages/             # Actor message records (-> Weather, Sensors)
  Njord.Compute/              # Pure analysis computation (-> Weather, Sensors only)
    Analysis/                 # Consensus, alerts, derived, trends, indices computers
    Configuration/            # Compute-parameter options (AlertOptions, IndexOptions)
  Njord.Core/                 # Options validators, actor keys,
                              #   StreamSupervision (-> Weather, Sensors, Messages, Persistence, Compute)
    Configuration/            # Validators, budget calculator

  # /Domain/ — feature libraries (each -> Core only)
  Njord.OpenMeteo/            # OpenMeteoGateway, OpenMeteoClient, DTOs, JSON source generator
  Njord.Budget/               # BudgetTracker persistent actor, usage accounting
  Njord.SensorHub/            # SensorHub
  Njord.Pipeline/             # PollScheduler, ForecastPipeline, PipelineEvent BroadcastHub
  Njord.Mqtt/                 # MQTT connection, state payloads, SourceRef consumer
  Njord.Grpc/                 # gRPC services, GrpcConsumer, protos

  # /Analysis/ — per-feature analysis libraries (each -> Core only)
  Njord.Alerts/               # AlertAnalyzer, AlertEvaluator
  Njord.Derived/              # DerivedAnalyzer
  Njord.Trends/               # TrendAnalyzer
  Njord.Indices/              # IndexAnalyzer

  # Host
  Njord/                      # Service host: Program.cs, DI, actors, streams (-> all above)
    Configuration/            # Host setup (service, actor system, application)
    Health/

  # /Tests/ — one test project per production project
  Njord.Weather.Tests/        # Weather record specs
  Njord.Sensors.Tests/        # Sensor record specs
  Njord.Persistence.Tests/    # DTO wire-format specs (Verify)
  Njord.Compute.Tests/        # Pure analysis computation specs (no Akka)
  Njord.Core.Tests/           # Configuration, StreamConsumerActor
  Njord.OpenMeteo.Tests/      # OpenMeteoClient, OpenMeteoGateway specs
  Njord.Budget.Tests/         # BudgetTracker specs
  Njord.SensorHub.Tests/      # SensorHub specs
  Njord.Pipeline.Tests/       # PollScheduler, ForecastPipeline specs
  Njord.Mqtt.Tests/           # MQTT connection, state payload, consumer specs
  Njord.Grpc.Tests/           # gRPC service, GrpcConsumer specs
  Njord.Alerts.Tests/         # AlertAnalyzer specs
  Njord.Derived.Tests/        # DerivedAnalyzer specs
  Njord.Trends.Tests/         # TrendAnalyzer specs
  Njord.Indices.Tests/        # IndexAnalyzer specs
  Njord.Architecture.Tests/   # ArchUnit zone/layer/convention rules
  Njord.Tests.Shared/         # Shared fakes, fixtures, helpers (not a test project)
```

Reference direction: Weather/Sensors/Persistence <- Messages <- Compute <- Core <- feature libs
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

Current total: 655 tests (Weather 59, Sensors 11, Persistence 2, Compute 165, Core 115,
Grpc 56, Pipeline 64, Mqtt 37, OpenMeteo 16, Budget 22, SensorHub 6, Architecture 34,
Alerts 61, Derived 2, Trends 3, Indices 2).
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
- `appsettings.json` — production logging only (no `Njord:` section).
- `appsettings.Development.json` — dev overrides (locations, models, MQTT
  disabled). Loaded automatically by `dotnet run` (`ASPNETCORE_ENVIRONMENT=Development`).
- `data/njord-config.json` — optional runtime override (Docker volume mount).
- Environment variables — Docker-compose / `docker run` (`Njord__Mqtt__Host`, etc.).

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
  wait it wraps: TestKit waits dilate by `akka.test.timefactor = 3` (3 s
  default becomes 9 s). Specs that start a hosted actor system or host use
  `TestTimeouts.Hosted` (`Njord.Tests.Shared`) and bound `AwaitAssert` /
  `AwaitCondition` explicitly (`TestTimeouts.AwaitAssertMax`); plain
  `Timeout = 5000` is only for specs that make no dilated Akka wait. Never
  reuse an actor name after `GracefulStop` (name release lags `Terminated`):
  use a unique name. Retry-backoff specs set `AddFastRetryBackoff()` instead of
  waiting real seconds. Pure synchronous specs use a plain `[Fact]`, a timeout
  cannot be enforced there.
- C#: records for messages/DTOs, value objects in the domain, `sealed` by
  default, nullable enabled.
- Persistence DTOs (`Njord.Persistence`): extend-only. Never remove or rename
  a `[JsonProperty]` string. New properties must be nullable or have a default.
  Increment `Version` when semantics change. Recovery code must handle all
  versions ≥ 1. While the project is 0.x, moving or renaming persistence DTO
  types is allowed as a breaking change (`refactor!:`); `[JsonProperty]` names
  and `Version` semantics stay extend-only.

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
- **Actor naming:** domain-role names (e.g. `PollScheduler`, `ForecastPipeline`), sealed.
- **Actors are resolved by marker interfaces** (`IPollScheduler`, ... in
  `Njord.Core/Actors/ActorKeys.cs`), registered in the host
  (`AkkaSetupContainer`); never key a registry lookup by the actor class.

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

Known deviations: 21 existing `!.` uses on nullable results in the test projects
(most in
`Njord.Compute.Tests/Analysis/ConsensusComputerSpec.cs` and
`Njord.Core.Tests/Configuration/ModelCoverageRegistrySpec.cs`), not counting the allowed
`JsonNode` indexers; new code follows the rule.

## Metrics conventions

- One `Meter("Njord.<Domain>")` per feature library (Ingest, Pipeline, Mqtt,
  Pipeline), owned by an `internal static class Telemetry` in that project with
  `internal static readonly` instrument fields (`Telemetry.MqttDedup.Add(...)`).
  New meters are registered in `CoreSetupContainer` (`NjordMeters`).
- Names are `njord_<area>_<name>` in snake_case; counters end in `_total`,
  time histograms in `_seconds`. Keep tag cardinality bounded.
- Metrics are exported via prometheus-net.

## References

- Open-Meteo API: https://open-meteo.com/en/docs (endpoint: `https://api.open-meteo.com/v1/forecast`)
- HA MQTT Discovery: https://www.home-assistant.io/integrations/mqtt/#mqtt-discovery
