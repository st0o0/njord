# parameter-typed-access Specification

## Purpose

Typed parameter access layer: static `ParameterRegistry` properties, O(1) lookup on `ResolvedParameterSet`, time-window filtering and aggregation on `ForecastSeries`, and elimination of string-based parameter lookups from domain Compute methods.
## Requirements
### Requirement: ParameterRegistry provides static typed properties
The `ParameterRegistry` SHALL expose a static, non-nullable `ParameterDef` member for each parameter, declared once as a typed `static readonly` field whose initializer registers the entry in the catalog. A parameter SHALL NOT be looked up by a string at type initialization.

#### Scenario: Typed property returns correct ParameterDef
- **WHEN** `ParameterRegistry.Temperature2m` is accessed
- **THEN** it SHALL return a `ParameterDef` with `ApiName == "temperature_2m"`

#### Scenario: All domain-referenced parameters have typed properties
- **WHEN** all static members are accessed
- **THEN** each SHALL return a non-null `ParameterDef` that is part of the catalog

#### Scenario: Missing parameter fails at startup
- **WHEN** two declarations share the same API name and granularity
- **THEN** the catalog specification fails, and a duplicate cannot reach production because the uniqueness spec runs in CI

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

### Requirement: Mean24h is not duplicated
There SHALL be no private 24-hour mean helper in the analysis libraries. Index scoring SHALL use the period means computed once by `DaySlicer`, and series means SHALL use `ForecastSeries.Mean` or the consensus query vocabulary.

#### Scenario: No private Mean24h methods
- **WHEN** the index library (`src/Njord.Indices`) is inspected
- **THEN** it contains no private `Mean24h` method

### Requirement: No string-based parameter lookups in analysis code
All analysis code — `ConsensusBuilder`, the alert rules, the index scores and `DaySlicer`, `DerivedAnalyzer` and `TrendAnalyzer` — SHALL resolve parameters via `ParameterRegistry` static properties (and `ResolvedParameterSet.Get(ParameterDef)` where the active set matters). No analysis code SHALL contain `FirstOrDefault(p => p.ApiName == ...)` or string-literal parameter names.

#### Scenario: Index slicing uses typed access
- **WHEN** `DaySlicer` resolves the `is_day` parameter
- **THEN** it uses `ParameterRegistry.IsDay`, not a string literal

#### Scenario: Alert rules use typed access
- **WHEN** `FrostRule` resolves the temperature parameter
- **THEN** it uses `ParameterRegistry.Temperature2m`, not `ParameterRegistry.GetByApiName("temperature_2m")`

### Requirement: Trend analysis uses ParameterDef keys, not strings
`TrendAnalyzer` SHALL define its tracked parameters as a `ParameterDef[]` and its per-parameter trend thresholds (`TrendThresholds`) as a dictionary keyed by `ParameterDef`, both using `ParameterRegistry` static properties. It SHALL NOT use `string[]` or `Dictionary<string, double>` for parameter identification.

#### Scenario: TrendParams uses typed array
- **WHEN** `TrendAnalyzer` defines its tracked parameters
- **THEN** it uses a `ParameterDef[]` referencing `ParameterRegistry` properties

