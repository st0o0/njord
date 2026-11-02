## ADDED Requirements

### Requirement: ModelComparisonHelper has unit test coverage

`ModelComparisonHelper.CompareDays` SHALL be tested for correctness of its day-grouping, model-ordering, and summarization logic.

#### Scenario: Two models with overlapping days produce correct comparison
- **WHEN** two models each have hourly ForecastPoints spanning the same days within the 5-day window
- **THEN** `CompareDays` returns one `ModelComparisonDay` per overlapping day, each containing two `ModelDayValues` ordered by model ID

#### Scenario: Past-only data returns empty
- **WHEN** all ForecastPoints have `ValidFrom` before `now`
- **THEN** `CompareDays` returns an empty `ImmutableArray`

#### Scenario: Null temperature and precipitation are preserved
- **WHEN** a model's points have null `Temperature` or null `Precipitation` for a day
- **THEN** the corresponding `ModelDayValues` fields are null (not zero, not omitted)

#### Scenario: Window is limited to 5 days from today
- **WHEN** models have points spanning 7 days from today
- **THEN** only the first 5 days appear in the result

### Requirement: ForecastModelComparison rule has test coverage

The ForecastModelComparison feature rule SHALL be tested for its projection and evaluation logic.

#### Scenario: Projection skips when fewer than 2 forecast models
- **WHEN** `LocationForecast.ForecastModelMap` contains 0 or 1 model
- **THEN** `Project` returns null (rule is skipped)

#### Scenario: Projection returns input when 2+ forecast models present
- **WHEN** `LocationForecast.ForecastModelMap` contains 2 or more models
- **THEN** `Project` returns a `ForecastModelsInput` with the model map

#### Scenario: Evaluation returns result with valid comparison days
- **WHEN** the projected input has 2 models with future data
- **THEN** `Evaluate` returns a `ForecastModelComparisonResult` with non-empty days

### Requirement: EnsembleModelComparison rule has test coverage

The EnsembleModelComparison feature rule SHALL be tested for its projection and evaluation logic.

#### Scenario: Projection skips when fewer than 2 ensemble models
- **WHEN** `LocationForecast.EnsembleModelMap` contains 0 or 1 model
- **THEN** `Project` returns null (rule is skipped)

#### Scenario: Projection returns input when 2+ ensemble models present
- **WHEN** `LocationForecast.EnsembleModelMap` contains 2 or more models
- **THEN** `Project` returns an `EnsembleModelsInput` with the model map

#### Scenario: Evaluation returns result with valid comparison days
- **WHEN** the projected input has 2 models with future data
- **THEN** `Evaluate` returns an `EnsembleModelComparisonResult` with non-empty days
