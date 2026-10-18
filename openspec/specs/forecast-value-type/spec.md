## ADDED Requirements

### Requirement: Unified forecast value type
The system SHALL provide a `ForecastValue` readonly record struct in `Njord.Model` that can hold either a numeric value (`double?`) or a text value (`string?`), replacing the separate numeric and string dictionaries on daily forecast points and the standalone `HorizonValue` record.

#### Scenario: Numeric forecast value
- **WHEN** a forecast data point carries a numeric parameter (e.g., temperature, precipitation)
- **THEN** `ForecastValue.Number` SHALL contain the value and `ForecastValue.Text` SHALL be null

#### Scenario: Text forecast value
- **WHEN** a forecast data point carries a text parameter (e.g., sunrise, sunset as ISO timestamp strings)
- **THEN** `ForecastValue.Text` SHALL contain the value and `ForecastValue.Number` SHALL be null

#### Scenario: Absent forecast value
- **WHEN** a model does not provide a value for a parameter at a given time step
- **THEN** both `ForecastValue.Number` and `ForecastValue.Text` SHALL be null

### Requirement: Single-dictionary forecast points
`ForecastPoint` and `DailyForecastPoint` SHALL each carry a single `IReadOnlyDictionary<ParameterDef, ForecastValue>` named `Values`, replacing the current `IReadOnlyDictionary<ParameterDef, double?>` (hourly) and dual `NumericValues`/`MetaValues` dictionaries (daily).

#### Scenario: Hourly point value access
- **WHEN** a consumer calls `ForecastPoint.Get(parameter)`
- **THEN** it SHALL return `ForecastValue` for that parameter, or a default (both fields null) if absent

#### Scenario: Daily point value access
- **WHEN** a consumer calls `DailyForecastPoint.Get(parameter)`
- **THEN** it SHALL return `ForecastValue` for that parameter, or a default if absent
- **THEN** there SHALL be no separate `GetNumeric` or `GetMeta` methods

#### Scenario: HasAnyValue check
- **WHEN** checking whether a forecast point carries any data
- **THEN** `HasAnyValue` SHALL return true if any entry in `Values` has a non-null `Number` or non-null `Text`

### Requirement: HorizonValue delegates to ForecastValue
`HorizonValue` in `Njord.Forecast` SHALL be a record of `(ParameterDef Parameter, ForecastValue Value)`, replacing the current `(ParameterDef Parameter, double? Number, string? Text)`.

#### Scenario: HorizonProjection produces HorizonValue with ForecastValue
- **WHEN** `HorizonProjection.BuildPerHorizon` projects a forecast point
- **THEN** each `HorizonValue` SHALL wrap the point's `ForecastValue` for that parameter
