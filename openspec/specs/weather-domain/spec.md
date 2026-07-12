# weather-domain Specification

## Purpose

Core weather domain model: registry-driven open parameter set, value-wrapped weather model ids, ordered forecast series tolerant of missing values, daily forecast series, and fully identified model forecasts.

## Requirements

### Requirement: The weather parameter set is registry-driven and open
The domain SHALL represent weather parameters via string-keyed `ParameterDef` references from the parameter registry. The active parameter set SHALL be determined at startup from configuration and remain fixed for the process lifetime. Code MUST NOT assume a specific closed set of parameters.

#### Scenario: Parameter identity is its API name
- **WHEN** code resolves a parameter definition
- **THEN** equality is based on the `ApiName` string (e.g. `temperature_2m`)

#### Scenario: Active set is immutable after startup
- **WHEN** the service has started with a resolved parameter set
- **THEN** the set does not change until the process restarts

### Requirement: Weather models are value-wrapped free-form ids
The domain SHALL represent a weather model as a value object wrapping the raw
Open-Meteo model id string (e.g. `icon_d2`, `icon_eu`, `ecmwf_ifs025`,
`gfs_seamless`). Construction SHALL reject null, empty, or whitespace ids. The
domain MUST NOT constrain ids to a hardcoded list.

#### Scenario: Blank model id is rejected
- **WHEN** a `WeatherModel` is constructed from an empty string
- **THEN** construction fails with a validation error

### Requirement: Forecast series are ordered and tolerate missing values
A `ForecastSeries` SHALL hold forecast points ordered ascending by their
valid-at timestamp. Each point SHALL carry a valid-at timestamp
(`DateTimeOffset`) and a dictionary of parameter values keyed by `ParameterDef`. A missing
value for one parameter (null entry or absent key) MUST NOT remove or invalidate the point.
Points whose values are all absent MAY be trimmed from the end of the series.

#### Scenario: Point with a missing parameter survives
- **WHEN** a forecast point has `temperature_2m` but no `dewpoint_2m`
- **THEN** the point is retained in the series with dewpoint absent

#### Scenario: Unordered input is normalized
- **WHEN** a series is constructed from points that are not in ascending
  valid-at order
- **THEN** the resulting series is ascending by valid-at

### Requirement: A model forecast carries both hourly and daily series
A `ModelForecast` SHALL carry the weather model, the location, the poll cycle id, an hourly `ForecastSeries`, and a daily `DailyForecastSeries`. Either series MAY be empty (but not both). The record SHALL NOT carry a separate `RetrievedAt` timestamp — the `CycleId` is the authoritative time reference for when the data was collected.

#### Scenario: Forecast with hourly only
- **WHEN** no daily parameters are active
- **THEN** the `ModelForecast` carries an hourly series and an empty daily series

#### Scenario: Forecast with both
- **WHEN** hourly and daily parameters are active and the model returns both
- **THEN** the `ModelForecast` carries both series populated

#### Scenario: No RetrievedAt field exists
- **WHEN** a `ModelForecast` is constructed
- **THEN** it carries `Model`, `Location`, `Cycle`, `Hourly`, and `Daily` — no `RetrievedAt`
