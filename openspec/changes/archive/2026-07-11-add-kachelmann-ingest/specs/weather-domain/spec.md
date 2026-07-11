# weather-domain — Delta Spec

## ADDED Requirements

### Requirement: The v1 weather parameter set is closed and typed
The domain SHALL define a closed parameter set consisting of exactly:
temperature (°C), precipitation (mm), windSpeed (m/s), windGust (m/s),
dewpoint (°C), relativeHumidity (%), cloudCover (%), and pressureMsl (hPa).
Each parameter SHALL carry its unit as metadata.

#### Scenario: Parameter metadata is available
- **WHEN** code asks for the unit of `WindSpeed`
- **THEN** the domain returns `m/s`

### Requirement: Weather models are value-wrapped free-form ids
The domain SHALL represent a weather model as a value object wrapping the raw
Kachelmann model id string (e.g. `ICON-D2`, `ECMWF`, `GFS`, `SWISS1X1`).
Construction SHALL reject null, empty, or whitespace ids. The domain MUST NOT
constrain ids to a hardcoded list.

#### Scenario: Blank model id is rejected
- **WHEN** a `WeatherModel` is constructed from an empty string
- **THEN** construction fails with a validation error

### Requirement: Forecast series are ordered and tolerate missing values
A `ForecastSeries` SHALL hold forecast points ordered ascending by their
valid-at timestamp. Each point SHALL carry a valid-at timestamp
(`DateTimeOffset`) and one nullable value per weather parameter; a missing
value for one parameter MUST NOT remove or invalidate the point.

#### Scenario: Point with a missing parameter survives
- **WHEN** a forecast point has temperature but no dewpoint
- **THEN** the point is retained in the series with dewpoint absent

#### Scenario: Unordered input is rejected or normalized
- **WHEN** a series is constructed from points that are not in ascending
  valid-at order
- **THEN** the resulting series is ascending by valid-at

### Requirement: A model forecast is fully identified
A `ModelForecast` SHALL carry the weather model, the location it was requested
for, the poll cycle id it belongs to, the retrieval timestamp, and its forecast
series.

#### Scenario: Forecast is attributable
- **WHEN** a `ModelForecast` is produced for model `ECMWF`, location `home`,
  cycle `2026-07-11T12:00Z`
- **THEN** all three identifiers plus the retrieval timestamp are readable from
  the value
