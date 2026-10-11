# egress-event Delta

## MODIFIED Requirements

### Requirement: PipelineEvent uses typed variants for analysis results
`PipelineEvent` SHALL have typed record variants for each analysis output
instead of a generic `AnalysisResult(string TypeName, object Result)`. The
variants are: `ForecastReceived`, `ConsensusComputed`, `AlertComputed`,
`DerivedComputed`, `TrendComputed`, `IndexComputed`. Each carries its typed
result record directly. `ModelCapability` stays unchanged.

#### Scenario: Consumers pattern-match on typed events
- **WHEN** a consumer receives a `PipelineEvent`
- **THEN** it matches on typed variants like `PipelineEvent.AlertComputed` without casting `object`

#### Scenario: No string-based type discrimination
- **WHEN** a `PipelineEvent` is emitted
- **THEN** it carries no `TypeName` string — the C# type IS the discriminator

### Requirement: Forecast replaces ModelForecast as the domain type
`ModelForecast` SHALL be renamed to `Forecast` in `Njord.Weather`. The type
retains its `WeatherModel Model` property for model identification.

#### Scenario: Forecast used throughout the pipeline
- **WHEN** a poll returns forecast data
- **THEN** it is represented as `Forecast` (not `ModelForecast`)

### Requirement: PollOutcome replaces FetchOutcome
`FetchOutcome` SHALL be renamed to `PollOutcome` in `Njord.Messages.Pipeline`.
Variants remain `Success` and `Failure`.

#### Scenario: PollOutcome carries the poll result
- **WHEN** a poll completes
- **THEN** the result is `PollOutcome.Success` or `PollOutcome.Failure`

## REMOVED Requirements

### Requirement: AnalysisResult wraps heterogeneous results
Removed. `AnalysisResult(string TypeName, object Result)` is replaced by
typed variants. No `object` wrapping.

#### Scenario: removed
- **WHEN** removed
- **THEN** removed
