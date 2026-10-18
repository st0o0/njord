# consensus-snapshot Specification

## Purpose

LocationConsensus is the core domain type produced by the consensus pipeline stage. It wraps hourly and daily consensus facets computed from a ModelForecasts via a pure `Compute` factory method, and serves as the input for all downstream enrichments.
## Requirements
### Requirement: LocationConsensus is the core domain type produced by the consensus pipeline stage
The system SHALL produce a `LocationConsensus` record from a `ModelForecasts` with `ConsensusBuilder.Create(forecasts, location, settings)` (src/Njord.Consensus/ConsensusBuilder.cs), a pure computation over its injected `ResolvedParameterSet` and `TimeProvider`. `LocationConsensus` SHALL contain a `Location` (`LocationId`), an `HourlyConsensus` facet, a `DailyConsensus` facet and `ComputedAt`.

#### Scenario: Compute produces a LocationConsensus with hourly and daily facets
- **WHEN** `ConsensusBuilder.Create` is called with a `ModelForecasts` containing 3 models with hourly and daily data for location "Lucerne"
- **THEN** the result SHALL have `Location` "Lucerne", a non-empty `Hourly.Parameters` list, and a non-empty `Daily.Parameters` list

#### Scenario: Empty consensus for location with no model data
- **WHEN** `ConsensusBuilder.Create` is called for a location with no model entries in the `ModelForecasts`
- **THEN** the result SHALL have empty `Hourly.Parameters` and empty `Daily.Parameters`

### Requirement: HourlyConsensus wraps hourly parameter consensus with cutoff metadata

`HourlyConsensus` SHALL be a record containing `Parameters` (list of `ParameterConsensus`) and `CutoffHour` (int). `CutoffHour` SHALL be the second-to-last model's maximum hour — the last hour where at least 2 models have data.

#### Scenario: CutoffHour from 3 models with different horizons
- **WHEN** 3 models have hourly data extending to h48, h72, and h120
- **THEN** `CutoffHour` SHALL be 72 (second-to-last)

#### Scenario: Fewer than 2 models yields CutoffHour of -1
- **WHEN** only 1 model has hourly data
- **THEN** `CutoffHour` SHALL be -1 and `Parameters` SHALL be empty

### Requirement: DailyConsensus wraps daily parameter consensus with cutoff metadata

`DailyConsensus` SHALL be a record containing `Parameters` (list of `ParameterConsensus`) and `CutoffDay` (int). `CutoffDay` SHALL be the second-to-last model's day count.

#### Scenario: CutoffDay from 3 models with different daily ranges
- **WHEN** 3 models have daily data for 3, 5, and 7 days
- **THEN** `CutoffDay` SHALL be 5 (second-to-last)

#### Scenario: Fewer than 2 models with daily data yields CutoffDay of 0
- **WHEN** only 1 model has daily data
- **THEN** `CutoffDay` SHALL be 0 and `Parameters` SHALL be empty

### Requirement: LocationConsensus records the computation wall-clock time
`LocationConsensus` SHALL include a `ComputedAt` property of type `DateTimeOffset`, set to `timeProvider.GetUtcNow()` inside `ConsensusBuilder.Create`. This value represents the wall-clock time at which the consensus was computed and serves as the anchor for resolving offset-based horizon keys to absolute timestamps.

#### Scenario: ComputedAt matches the TimeProvider value at computation time
- **WHEN** `ConsensusBuilder.Create` is called with a `FakeTimeProvider` returning `2026-07-31T06:00:00Z`
- **THEN** the resulting `LocationConsensus.ComputedAt` SHALL be `2026-07-31T06:00:00Z`

#### Scenario: ComputedAt is propagated to ConsensusResult
- **WHEN** an output mapper (`StatePayloadBuilder.FromConsensus` or the gRPC consensus mapping) builds a `ConsensusResult` from a `LocationConsensus`
- **THEN** `ConsensusResult.ComputedAt` SHALL equal `LocationConsensus.ComputedAt`

### Requirement: Consensus building filters horizons below the quorum
Horizons where fewer than `ConsensusSettings.MinQuorum` (default 2) models have a non-null value SHALL be excluded from the result. This filtering is integral to `ConsensusBuilder.Create`, not a post-processing step.

#### Scenario: Horizon with 1 model excluded
- **WHEN** hour h70 has data from only 1 model and the quorum is 2
- **THEN** h70 does not appear in `Hourly.Parameters[*].ByHorizon`

#### Scenario: Horizon with 2+ models included
- **WHEN** hour h6 has data from 4 models
- **THEN** h6 appears in `Hourly.Parameters[*].ByHorizon`

### Requirement: One consensus is built per received forecast
`ForecastPipeline`'s stateful stage SHALL add each incoming forecast to its `ModelForecasts` and build one `LocationConsensus` for that forecast's location with the current consensus settings. The consensus computation SHALL be pure: no I/O, no actor messages, no state outside the returned record.

#### Scenario: One consensus per forecast
- **WHEN** a forecast for location "Lucerne" arrives while the `ModelForecasts` also holds forecasts for "Zurich"
- **THEN** exactly one `LocationConsensus`, for "Lucerne", is built and, while consensus is enabled, emitted as `ConsensusComputed`

#### Scenario: Pure transformation with no side effects
- **WHEN** `ConsensusBuilder.Create` processes a `ModelForecasts`
- **THEN** it performs no I/O, sends no actor messages and modifies no state

### Requirement: ConsensusBuilder delegates statistics to ConsensusStatistics
`ConsensusBuilder` SHALL delegate all statistical calculations (median, trimmed mean, spread, IQR, agreement, outlier, confidence interval, availability) to the static `ConsensusStatistics` class and SHALL produce `HorizonConsensus` and `ParameterConsensus` records.

#### Scenario: Statistics come from ConsensusStatistics
- **WHEN** `ConsensusBuilder.Create` computes the median of a horizon
- **THEN** the value equals `ConsensusStatistics.ComputeMedian` over the contributing model values

