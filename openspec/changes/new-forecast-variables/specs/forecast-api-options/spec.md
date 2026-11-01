## ADDED Requirements

### Requirement: Surface temperature variable
The system SHALL support `surface_temperature` as a requestable Forecast variable. This is the temperature at the ground surface (not 2m air temperature), useful for ice and frost detection. Available on ECMWF.

#### Scenario: surface_temperature in ForecastParams
- **WHEN** a feature declares `ForecastParams.SurfaceTemperature` in its requirements
- **THEN** the Forecast client SHALL include `surface_temperature` in the API request
- **AND** the parsed `ForecastPoint` SHALL contain the `SurfaceTemperature` value

### Requirement: Snowfall water equivalent variable
The system SHALL support `snowfall_water_equivalent` as a requestable Forecast variable. This provides snowfall in mm of water equivalent, more precise than `snowfall` in cm. Available on ECMWF, GFS, and WeatherNext.

#### Scenario: snowfall_water_equivalent in ForecastParams
- **WHEN** a feature declares `ForecastParams.SnowfallWaterEquivalent` in its requirements
- **THEN** the Forecast client SHALL include `snowfall_water_equivalent` in the API request
- **AND** the parsed `ForecastPoint` SHALL contain the `SnowfallWaterEquivalent` value in mm
