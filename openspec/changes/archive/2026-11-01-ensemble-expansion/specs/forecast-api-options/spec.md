## ADDED Requirements

### Requirement: Wind speed and direction at 100m height
The system SHALL support `wind_speed_100m` and `wind_direction_100m` as requestable Forecast variables. These are the standard wind turbine hub height measurements, available on ECMWF and WeatherNext models.

#### Scenario: wind_speed_100m in ForecastParams
- **WHEN** a feature declares `ForecastParams.WindSpeed100m` in its requirements
- **THEN** the Forecast client SHALL include `wind_speed_100m` in the API request
- **AND** the parsed `ForecastPoint` SHALL contain the `WindSpeed100m` value

#### Scenario: wind_direction_100m in ForecastParams
- **WHEN** a feature declares `ForecastParams.WindDirection100m` in its requirements
- **THEN** the Forecast client SHALL include `wind_direction_100m` in the API request
- **AND** the parsed `ForecastPoint` SHALL contain the `WindDirection100m` value
