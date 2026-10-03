## MODIFIED Requirements

### Requirement: ConsensusResult holds both hourly and daily parameter consensus
`ConsensusResult` SHALL be a record with two collections: `Parameters` (hourly, existing) and `DailyParameters` (daily, new). Both are `IReadOnlyList<ParameterConsensus>`. The `Compute` static method SHALL accept the full `ResolvedParameterSet` and iterate both `.Hourly` and `.Daily` lists, producing separate collections in the result.

#### Scenario: Compute produces both hourly and daily results
- **WHEN** `ConsensusResult.Compute` is called with a ResolvedParameterSet containing 30 hourly and 16 daily parameters
- **THEN** the result contains up to 30 entries in `Parameters` and up to 16 entries in `DailyParameters`

#### Scenario: Empty daily parameter set
- **WHEN** the ResolvedParameterSet has an empty `Daily` list
- **THEN** `DailyParameters` is an empty list (no error)

### Requirement: Daily parameter consensus uses DailyForecastSeries for value lookup
For daily parameters, the Compute method SHALL look up values in `ModelForecast.Daily` (the `DailyForecastSeries`) by matching the `DateOnly` corresponding to the day-horizon offset. It SHALL NOT attempt to look up daily parameters in the hourly series.

#### Scenario: Daily value lookup
- **WHEN** computing consensus for `temperature_2m_max` at day-horizon d1 (= 2026-07-20)
- **THEN** the value is retrieved from each model's `DailyForecastSeries` at DateOnly 2026-07-20

#### Scenario: Model missing a daily point
- **WHEN** a model's DailyForecastSeries has no entry for the target date
- **THEN** that model contributes null for that (parameter, day-horizon) and is excluded from AvailableModels
