## ADDED Requirements

### Requirement: Consensus building filters horizons below the quorum
Horizons where fewer than `ConsensusSettings.MinQuorum` (default 2) models have a non-null value SHALL be excluded from the result. This filtering is integral to `ConsensusBuilder.Create`, not a post-processing step.

#### Scenario: Horizon with 1 model excluded
- **WHEN** hour h70 has data from only 1 model and the quorum is 2
- **THEN** h70 does not appear in `Hourly.Parameters[*].ByHorizon`

#### Scenario: Horizon with 2+ models included
- **WHEN** hour h6 has data from 4 models
- **THEN** h6 appears in `Hourly.Parameters[*].ByHorizon`

### Requirement: One consensus is built per received forecast
`ForecastPipeline`'s stateful stage SHALL add each incoming forecast to its `ModelSnapshot` and build one `ConsensusSnapshot` for that forecast's location with the current consensus settings. The consensus computation SHALL be pure: no I/O, no actor messages, no state outside the returned record.

#### Scenario: One consensus per forecast
- **WHEN** a forecast for location "Lucerne" arrives while the snapshot also holds forecasts for "Zurich"
- **THEN** exactly one `ConsensusSnapshot`, for "Lucerne", is built and emitted as `ConsensusComputed`

#### Scenario: Pure transformation with no side effects
- **WHEN** `ConsensusBuilder.Create` processes a `ModelSnapshot`
- **THEN** it performs no I/O, sends no actor messages and modifies no state

### Requirement: ConsensusBuilder delegates statistics to ConsensusStatistics
`ConsensusBuilder` SHALL delegate all statistical calculations (median, trimmed mean, spread, IQR, agreement, outlier, confidence interval, availability) to the static `ConsensusStatistics` class and SHALL produce `HorizonConsensus` and `ParameterConsensus` records.

#### Scenario: Statistics come from ConsensusStatistics
- **WHEN** `ConsensusBuilder.Create` computes the median of a horizon
- **THEN** the value equals `ConsensusStatistics.ComputeMedian` over the contributing model values

## MODIFIED Requirements

### Requirement: ConsensusSnapshot is the core domain type produced by the consensus pipeline stage
The system SHALL produce a `ConsensusSnapshot` record from a `ModelSnapshot` with `ConsensusBuilder.Create(snapshot, location, settings)` (src/Njord.Compute/Consensus/ConsensusBuilder.cs), a pure computation over its injected `ResolvedParameterSet` and `TimeProvider`. `ConsensusSnapshot` SHALL contain a `Location` (`LocationId`), an `HourlyConsensus` facet, a `DailyConsensus` facet and `ComputedAt`.

#### Scenario: Compute produces a ConsensusSnapshot with hourly and daily facets
- **WHEN** `ConsensusBuilder.Create` is called with a `ModelSnapshot` containing 3 models with hourly and daily data for location "Lucerne"
- **THEN** the result SHALL have `Location` "Lucerne", a non-empty `Hourly.Parameters` list, and a non-empty `Daily.Parameters` list

#### Scenario: Empty snapshot for location with no model data
- **WHEN** `ConsensusBuilder.Create` is called for a location with no model entries in the snapshot
- **THEN** the result SHALL have empty `Hourly.Parameters` and empty `Daily.Parameters`

### Requirement: ConsensusSnapshot records the computation wall-clock time
`ConsensusSnapshot` SHALL include a `ComputedAt` property of type `DateTimeOffset`, set to `timeProvider.GetUtcNow()` inside `ConsensusBuilder.Create`. This value represents the wall-clock time at which the consensus was computed and serves as the anchor for resolving offset-based horizon keys to absolute timestamps.

#### Scenario: ComputedAt matches the TimeProvider value at computation time
- **WHEN** `ConsensusBuilder.Create` is called with a `FakeTimeProvider` returning `2026-07-31T06:00:00Z`
- **THEN** the resulting `ConsensusSnapshot.ComputedAt` SHALL be `2026-07-31T06:00:00Z`

#### Scenario: ComputedAt is propagated to ConsensusResult
- **WHEN** an output mapper (`StatePayloadBuilder.FromConsensus` or the gRPC consensus mapping) builds a `ConsensusResult` from a `ConsensusSnapshot`
- **THEN** `ConsensusResult.ComputedAt` SHALL equal `ConsensusSnapshot.ComputedAt`

## REMOVED Requirements

### Requirement: ConsensusSnapshot.Compute filters horizons with fewer than 2 contributing models
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Consensus building filters horizons below the quorum" in this capability.

### Requirement: ConsensusSnapshot is the input for the consensus stream stage
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "One consensus is built per received forecast" in this capability.

### Requirement: ConsensusSnapshot reuses existing statistical computation
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "ConsensusBuilder delegates statistics to ConsensusStatistics" in this capability.
