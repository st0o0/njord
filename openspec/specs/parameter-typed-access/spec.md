# parameter-typed-access Specification

## Purpose

Typed parameter access layer: static `ParameterRegistry` properties, O(1) lookup on `ResolvedParameterSet`, time-window filtering and aggregation on `ForecastSeries`, and elimination of string-based parameter lookups from domain Compute methods.

## Requirements

### Requirement: ParameterRegistry provides static typed properties
The `ParameterRegistry` SHALL expose static `ParameterDef` properties for
each parameter referenced in domain Compute methods. These properties SHALL
be non-nullable. If a property's `ApiName` is not found in the built
parameter list, the static constructor SHALL throw.

#### Scenario: Typed property returns correct ParameterDef
- **WHEN** `ParameterRegistry.Temperature2m` is accessed
- **THEN** it SHALL return a `ParameterDef` with `ApiName == "temperature_2m"`

#### Scenario: All domain-referenced parameters have typed properties
- **WHEN** all static properties are accessed
- **THEN** each SHALL return a non-null `ParameterDef` matching the
  corresponding API name

#### Scenario: Missing parameter fails at startup
- **WHEN** the static constructor cannot find a parameter's `ApiName` in the
  built list
- **THEN** it SHALL throw an exception, preventing the application from
  starting

### Requirement: ResolvedParameterSet provides O(1) lookup by ParameterDef
The `ResolvedParameterSet` SHALL provide a `Get(ParameterDef param)` method
that returns the matching `ParameterDef` from the resolved set in O(1) time,
or `null` if the parameter is not in the set. It SHALL also provide a
`Contains(ParameterDef param)` method.

#### Scenario: Parameter in resolved set returns non-null
- **WHEN** `Get(ParameterRegistry.Temperature2m)` is called on a resolved
  set that includes `temperature_2m`
- **THEN** it SHALL return the `ParameterDef` for `temperature_2m`

#### Scenario: Parameter not in resolved set returns null
- **WHEN** `Get(ParameterRegistry.Temperature2m)` is called on a resolved
  set that excludes `temperature_2m`
- **THEN** it SHALL return `null`

#### Scenario: Contains returns true for included parameter
- **WHEN** `Contains(ParameterRegistry.WindSpeed10m)` is called on a
  resolved set that includes `wind_speed_10m`
- **THEN** it SHALL return `true`

### Requirement: ForecastSeries provides time-window filtering
The `ForecastSeries` SHALL provide a `Window(DateTimeOffset from,
DateTimeOffset to)` method that returns a new `ForecastSeries` containing
only points where `ValidAt` is within the inclusive range `[from, to]`.

#### Scenario: Window filters points by time range
- **WHEN** `Window(10:00, 14:00)` is called on a series with points at
  09:00, 10:00, 12:00, 14:00, 16:00
- **THEN** the returned series SHALL contain points at 10:00, 12:00, 14:00

#### Scenario: Window with no matching points returns empty series
- **WHEN** `Window(20:00, 22:00)` is called on a series with all points
  before 18:00
- **THEN** the returned series SHALL contain zero points

### Requirement: ForecastSeries provides Mean computation
The `ForecastSeries` SHALL provide a `Mean(ParameterDef param,
DateTimeOffset from, DateTimeOffset to)` method that computes the arithmetic
mean of non-null values for the given parameter within the time window. If
no non-null values exist, it SHALL return `null`.

#### Scenario: Mean of existing values
- **WHEN** `Mean(temperature_2m, 10:00, 14:00)` is called with points at
  10:00 (20.0), 12:00 (22.0), 14:00 (24.0)
- **THEN** it SHALL return `22.0`

#### Scenario: Mean with some null values
- **WHEN** `Mean(temperature_2m, 10:00, 14:00)` is called with points at
  10:00 (20.0), 12:00 (null), 14:00 (24.0)
- **THEN** it SHALL return `22.0` (average of non-null only)

#### Scenario: Mean with no data returns null
- **WHEN** `Mean(temperature_2m, 10:00, 14:00)` is called and all values
  for that parameter are null
- **THEN** it SHALL return `null`

### Requirement: ForecastSeries provides Values extraction
The `ForecastSeries` SHALL provide a `Values(ParameterDef param,
DateTimeOffset from, DateTimeOffset to)` method that returns an enumerable
of non-null `double` values for the parameter within the time window.

#### Scenario: Values returns non-null values only
- **WHEN** `Values(wind_speed_10m, 10:00, 14:00)` is called with points
  having values 5.0, null, 7.0
- **THEN** it SHALL return `[5.0, 7.0]`

### Requirement: No string-based parameter lookups in domain Compute methods
All domain Compute methods (`IndexResult.Compute`, `EnergyResult.Compute`,
`DerivedResult.Compute`, `TrendResult.Compute`, `ConsensusResult.Compute`,
`HistoryResult.Compute`, `AlertEvaluator.EvaluateAll`) SHALL resolve
parameters via `ParameterRegistry` static properties and
`ResolvedParameterSet.Get(ParameterDef)`. No Compute method SHALL contain
`FirstOrDefault(p => p.ApiName == ...)` or string-literal parameter names.

#### Scenario: IndexResult uses typed access
- **WHEN** `IndexResult.Compute` resolves the temperature parameter
- **THEN** it SHALL use `parameters.Get(ParameterRegistry.Temperature2m)`,
  not `parameters.Hourly.FirstOrDefault(p => p.ApiName == "temperature_2m")`

#### Scenario: AlertEvaluator uses typed access
- **WHEN** `AlertEvaluator` resolves the temperature parameter
- **THEN** it SHALL use `ParameterRegistry.Temperature2m`, not
  `ParameterRegistry.GetByApiName("temperature_2m")`

### Requirement: TrendResult uses ParameterDef keys, not strings
The `TrendResult` SHALL define trend parameters as `ParameterDef[]` (using
`ParameterRegistry` static properties) and thresholds as
`Dictionary<ParameterDef, double>`. It SHALL NOT use `string[]` or
`Dictionary<string, double>` for parameter identification.

#### Scenario: TrendParams uses typed array
- **WHEN** `TrendResult` defines its tracked parameters
- **THEN** it SHALL use `ParameterDef[]` referencing `ParameterRegistry`
  properties

### Requirement: HistoryAnalyzer uses ParameterDef, not strings
The `HistoryAnalyzer` API SHALL accept `ParameterDef` parameters instead of
`string paramApiName`. `ForecastHistory` SHALL store data keyed by
`ParameterDef` instead of `string`.

#### Scenario: ComputeBias takes ParameterDef
- **WHEN** `HistoryAnalyzer.ComputeBias` is called
- **THEN** its parameter type SHALL be `ParameterDef`, not `string`

### Requirement: Mean24h is not duplicated
There SHALL be exactly one implementation of 24-hour mean computation.
`IndexResult` and `EnergyResult` SHALL use `ForecastSeries.Mean` or a shared
helper instead of containing their own private `Mean24h` methods.

#### Scenario: No private Mean24h methods
- **WHEN** `IndexResult.cs` and `EnergyResult.cs` are inspected
- **THEN** neither SHALL contain a private `Mean24h` method
