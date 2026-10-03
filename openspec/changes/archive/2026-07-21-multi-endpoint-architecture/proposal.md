## Why

njord is hardcoded to the Open-Meteo Weather Forecast endpoint (`/v1/forecast`). Open-Meteo offers multiple valuable endpoints (Air Quality, Marine, Satellite Radiation, Flood) that follow the same location-based polling pattern but return different data with different domain semantics. Adding each as a one-off integration would duplicate infrastructure and bloat the pipeline actor. A modular endpoint architecture allows new data sources to be added by registering a self-contained module — no pipeline rewiring needed.

## What Changes

- **Abstract `WeightedTarget` base**: Extract the current `WeightedTarget` into an abstract base with `EndpointType`, `Location`, `CycleId`, and abstract `Weight`. Concrete subclasses (`WeatherTarget`, `AirQualityTarget`, etc.) carry endpoint-specific fields (e.g., `WeatherModel` on `WeatherTarget` only).
- **`IEndpointModule` interface**: Each endpoint registers a module that provides target creation, a self-contained Akka.Streams sub-graph (`Flow<WeightedTarget, EgressEvent>`), and HA device definitions. Modules are composed from smaller parts (`IClient`, snapshot builder, enrichments) via DI.
- **Partition-based pipeline**: `PipelineActor` builds a `Partition<WeightedTarget>` stage keyed by `EndpointType` after the shared `BudgetThrottleStage`. Each outlet feeds into the corresponding module's sub-graph. Streams are fully isolated — own types, own error handling, own enrichments — and merge only at egress via `MergeHub<EgressEvent>`.
- **Namespace-per-endpoint structure**: `Njord.Endpoints.Weather/`, `Njord.Endpoints.AirQuality/`, etc. Each namespace contains its module, target, client, outcome, snapshot, and enrichment types.
- **Per-endpoint poll intervals**: `SchedulerActor` manages timers per `(Location, EndpointType)` instead of a single global tick, allowing Weather at 60min and AirQuality at 2h.
- **Weather endpoint extraction**: Refactor existing Weather Forecast code into the first `IEndpointModule` implementation as proof that the abstraction works before adding new endpoints.

## API Budget Impact

Weather (current): Locations × Models × 24 polls/day ≈ existing baseline (unchanged).
Air Quality (new, 2h interval): Locations × 12 polls/day × 1.0 weight = +12 requests/location/day. For 3 locations: +36/day, +1,080/month — well within the 300k free-tier limit.
Each additional endpoint adds linearly per location. The shared `BudgetThrottleStage` enforces the global ceiling regardless of how many endpoints are enabled.

## Non-goals

- **Adding new endpoints in this change**: This change builds the architecture and extracts Weather. Air Quality and Marine are follow-up changes that prove the architecture works.
- **Cross-endpoint correlation**: Weather and AirQuality streams are fully isolated. Correlating data across endpoints (e.g., weather-adjusted AQI) is a future enrichment concern, not an architecture concern.
- **Per-endpoint sub-budgets**: All endpoints share one budget pool. Reserving capacity per endpoint type is deferred until there's evidence it's needed.
- **Multiple Open-Meteo base URLs**: All endpoints hit the same `api.open-meteo.com` host. Supporting alternative providers is out of scope.

## Capabilities

### New Capabilities

- `endpoint-module`: The `IEndpointModule` contract, module registration via DI, and the composition pattern (client + snapshot builder + enrichments). Defines how a new endpoint plugs into the system.
- `endpoint-partition-pipeline`: The `Partition<WeightedTarget>` stage in `PipelineActor`, dynamic outlet wiring from registered modules, and the `MergeHub<EgressEvent>` convergence at egress.
- `weighted-target-hierarchy`: Abstract `WeightedTarget` base record with polymorphic subclasses per endpoint type. Weight calculation contract.
- `per-endpoint-scheduling`: `SchedulerActor` changes to manage independent timers per `(Location, EndpointType)` with per-endpoint poll intervals from configuration.

### Modified Capabilities

- `poll-pipeline`: Pipeline now builds a partition graph from registered modules instead of a single hardcoded fetch flow.
- `poll-scheduler`: Scheduler emits `WeightedTarget` subclasses per endpoint type with independent poll intervals, not a single global tick for weather-only targets.
- `openmeteo-client`: Existing `IOpenMeteoClient`/`OpenMeteoClient` is refactored into `Njord.Endpoints.Weather` as `IWeatherClient`/`WeatherClient`. The generic interface is removed.
- `enrichment-feature-registry`: Enrichments are registered per endpoint module, not globally. Each module brings its own enrichment set.

## Impact

- **`Njord.Pipeline`**: `PipelineActor` refactored to build partition graph. `WeightedTarget` becomes abstract base. `BudgetThrottleStage` unchanged (operates on base `Weight`).
- **`Njord.Ingest`**: `IOpenMeteoClient`/`OpenMeteoClient` move to `Njord.Endpoints.Weather`. Namespace removed or reduced to shared HTTP utilities.
- **`Njord.Enrichment`**: `EnrichmentActor` wiring changes — enrichments are per-module, not a flat global list. `IEnrichmentFeature` interfaces unchanged.
- **`Njord.Configuration`**: `NjordOptions` gains `Endpoints` section with per-endpoint `Enabled`/`PollInterval`. Existing weather config maps to `Endpoints.Weather`.
- **`Njord.Egress`**: Unchanged. `EgressActor` already consumes `EgressEvent` generically.
- **`Njord.Tests`**: Existing weather pipeline tests adapt to new namespace. `FakeOpenMeteoClient` becomes `FakeWeatherClient`.
- **No breaking changes to MQTT topics or HA entities** — device/entity naming is unchanged for weather.
