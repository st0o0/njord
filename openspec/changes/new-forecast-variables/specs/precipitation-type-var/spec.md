## ADDED Requirements

### Requirement: Precipitation type variable
The system SHALL support `precipitation_type` as a requestable Forecast variable. This classifies precipitation as rain, snow, freezing rain, or ice pellets. Available on ECMWF IFS.

#### Scenario: precipitation_type in ForecastParams
- **WHEN** a feature declares `ForecastParams.PrecipitationType` in its requirements
- **THEN** the Forecast client SHALL include `precipitation_type` in the API request
- **AND** the parsed `ForecastPoint` SHALL contain the `PrecipitationType` value

#### Scenario: precipitation_type null when unavailable
- **WHEN** the API returns null values for `precipitation_type`
- **THEN** the parsed `ForecastPoint` SHALL have `PrecipitationType` as null
