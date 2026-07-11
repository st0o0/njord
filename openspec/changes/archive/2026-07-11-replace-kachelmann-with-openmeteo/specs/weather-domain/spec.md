# weather-domain Specification (Delta)

## MODIFIED Requirements

### Requirement: The v1 weather parameter set is closed and typed
The domain SHALL define a closed parameter set consisting of exactly:
temperature (°C), apparentTemperature (°C), precipitation (mm),
windSpeed (m/s), windGust (m/s), dewpoint (°C), relativeHumidity (%),
cloudCover (%), and pressureMsl (hPa). Each parameter SHALL carry its unit as
metadata.

#### Scenario: Parameter metadata is available
- **WHEN** code asks for the unit of `WindSpeed`
- **THEN** the domain returns `m/s`

#### Scenario: Apparent temperature is part of the closed set
- **WHEN** code enumerates the v1 parameter set
- **THEN** `ApparentTemperature` is included with unit `°C`

### Requirement: Weather models are value-wrapped free-form ids
The domain SHALL represent a weather model as a value object wrapping the raw
Open-Meteo model id string (e.g. `icon_d2`, `icon_eu`, `ecmwf_ifs025`,
`gfs_seamless`). Construction SHALL reject null, empty, or whitespace ids. The
domain MUST NOT constrain ids to a hardcoded list.

#### Scenario: Blank model id is rejected
- **WHEN** a `WeatherModel` is constructed from an empty string
- **THEN** construction fails with a validation error
