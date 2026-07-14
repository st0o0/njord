# daily-forecast-typing Specification

## Purpose

Typed access for daily forecast data: `DailyForecastPoint` separates numeric values (`double?`) from meta/text values (`string?`) via distinct dictionaries and accessor methods, daily parsing routes values by `ParameterDef.ValueType`, and actor messages are kept out of the Domain layer.

## Requirements

### Requirement: DailyForecastPoint provides typed numeric access
The `DailyForecastPoint` SHALL store numeric daily parameter values in
`IReadOnlyDictionary<ParameterDef, double?> NumericValues` and provide a
`GetNumeric(ParameterDef)` method returning `double?`.

#### Scenario: Numeric daily value accessed via GetNumeric
- **WHEN** `GetNumeric` is called with a parameter for `precipitation_sum`
- **THEN** it SHALL return the `double?` value from `NumericValues`

#### Scenario: Missing numeric parameter returns null
- **WHEN** `GetNumeric` is called with a parameter not in `NumericValues`
- **THEN** it SHALL return `null`

### Requirement: DailyForecastPoint provides typed meta access
The `DailyForecastPoint` SHALL store text/time daily parameter values in
`IReadOnlyDictionary<ParameterDef, string?> MetaValues` and provide a
`GetMeta(ParameterDef)` method returning `string?`.

#### Scenario: Meta daily value accessed via GetMeta
- **WHEN** `GetMeta` is called with a parameter for `sunrise`
- **THEN** it SHALL return the `string?` value from `MetaValues`

#### Scenario: Missing meta parameter returns null
- **WHEN** `GetMeta` is called with a parameter not in `MetaValues`
- **THEN** it SHALL return `null`

### Requirement: DailyForecastPoint does not expose untyped access
The `DailyForecastPoint` SHALL NOT provide a `Get(ParameterDef)` method
returning `object?`. All access SHALL go through `GetNumeric` or `GetMeta`.

#### Scenario: No untyped Get method exists
- **WHEN** the `DailyForecastPoint` type is inspected
- **THEN** it SHALL NOT have a method named `Get` returning `object?`

### Requirement: Daily parsing routes by ValueType
The Open-Meteo response parser SHALL route daily parameter values into
`NumericValues` or `MetaValues` based on the parameter's
`ParameterDef.ValueType`:
- `Numeric` -> `NumericValues` (parsed as `double?`)
- `TimeString` -> `MetaValues` (parsed as `string?`)

#### Scenario: Numeric daily parameter goes to NumericValues
- **WHEN** the parser processes `precipitation_sum` (ValueType = Numeric)
- **THEN** the value SHALL appear in `NumericValues`

#### Scenario: TimeString daily parameter goes to MetaValues
- **WHEN** the parser processes `sunrise` (ValueType = TimeString)
- **THEN** the value SHALL appear in `MetaValues`

### Requirement: Actor messages do not reside in the Domain layer
The types `RecordSnapshot`, `QueryHistory`, and `HistoryResponse` SHALL
reside in the `Njord.Enrichment` namespace, not in
`Njord.Domain.Analysis`. The `ForecastRecorded` type SHALL NOT exist --
`ForecastRecord` SHALL be used directly as the persistence event.

#### Scenario: Domain/Analysis contains no actor messages
- **WHEN** `src/Njord/Domain/Analysis/ForecastHistory.cs` is inspected
- **THEN** it SHALL NOT contain `RecordSnapshot`, `QueryHistory`,
  `HistoryResponse`, or `ForecastRecorded` type definitions

#### Scenario: ForecastHistoryActor uses ForecastRecord as event
- **WHEN** `ForecastHistoryActor` persists a forecast observation
- **THEN** it SHALL persist a `ForecastRecord` instance, not
  `ForecastRecorded`
