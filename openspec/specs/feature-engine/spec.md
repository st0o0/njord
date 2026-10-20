# feature-engine Specification

## Purpose
Single stateful FeatureEngine stage in the ForecastPipeline that keeps a per-location ring of recent forecasts and evaluates the enabled feature rules.

## Requirements
### Requirement: Single stateful evaluation stage
The `FeatureEngine` SHALL be a single class that replaces the handler iteration in `ProjectionEngine`. It SHALL hold a `RingBuffer<LocationForecast>` per location, evaluate all enabled features sequentially, and return `PipelineEvent` results.

#### Scenario: Feature evaluation on domain data arrival
- **WHEN** a `DomainDataReceived` event arrives for a location
- **THEN** the engine SHALL update that location's `LocationForecast`, push it into the ring buffer, project the input for each enabled feature whose requirements are met, call `Evaluate`, and emit the non-null results as `PipelineEvent`s

#### Scenario: Feature skipped when requirements not met
- **WHEN** a feature requires Weather and AirQuality, but only Weather data is available
- **THEN** the engine SHALL skip that feature (its `Project` returns null) and not emit any result for it

### Requirement: Ring buffer per location
The engine SHALL maintain a bounded ring buffer of `LocationForecast` snapshots per location. The ring depth SHALL be the maximum `PollDepth` declared by any enabled feature.

#### Scenario: Ring depth determined by max PollDepth
- **WHEN** enabled features declare PollDepth values of 0, 1, and 8
- **THEN** the ring buffer SHALL store at most 9 entries (8 + 1 for current)

#### Scenario: Ring does not survive restart
- **WHEN** the service restarts
- **THEN** the ring buffer SHALL be empty and features with `PollDepth > 0` SHALL produce no result until enough polls accumulate

### Requirement: FeatureResolution from enabled features
The engine SHALL compute the union of `DomainRequirements` from all features whose config has `Enabled = true`. This union SHALL be used by `BudgetCalculator` and poll resolution, replacing the current `FeatureResolution.RequiredParams` logic.

#### Scenario: Disabled feature does not contribute requirements
- **WHEN** a feature with `Requirements: Weather(Uv)` has `Enabled = false` in its config
- **THEN** the UV parameter SHALL NOT be included in the resolved requirements

#### Scenario: All enabled features contribute requirements
- **WHEN** two enabled features require `Weather(Temperature)` and `AirQuality(Pm25)` respectively
- **THEN** the resolved requirements SHALL include both `Weather(Temperature)` and `AirQuality(Pm25)`

### Requirement: Integration with ForecastPipeline
The `FeatureEngine` SHALL be used by `ForecastPipeline` in place of the current `IDomainHandler[]` iteration. The stream topology (MergeHub → evaluation → BroadcastHub) SHALL remain unchanged.

#### Scenario: Pipeline uses FeatureEngine
- **WHEN** `ForecastPipeline` is constructed
- **THEN** it SHALL receive a `FeatureEngine` instead of `IEnumerable<IDomainHandler>` and use it for the evaluation stage
