## ADDED Requirements

### Requirement: Elevated wind sensor entities
The system SHALL provide HA sensor entities for 80 m and 120 m wind speed and direction per location when the ElevatedWind feature is enabled. The feature SHALL be off by default.

#### Scenario: Feature enabled
- **WHEN** `Njord:Features:Energy:ElevatedWind:Enabled=true`
- **THEN** the system SHALL create sensors `{location}_wind_speed_80m`, `{location}_wind_direction_80m`, `{location}_wind_speed_120m`, `{location}_wind_direction_120m`

#### Scenario: Feature disabled
- **WHEN** the ElevatedWind feature is not enabled
- **THEN** no elevated wind sensors SHALL be created and the 80m/120m variables SHALL NOT be fetched

### Requirement: ElevatedWind declares weather requirements
The ElevatedWind feature SHALL declare `WeatherParams.WindSpeed80m | WeatherParams.WindDirection80m | WeatherParams.WindSpeed120m | WeatherParams.WindDirection120m` in its `DomainRequirements`.

#### Scenario: Variables fetched only when enabled
- **WHEN** only ElevatedWind requires 80 m wind data
- **THEN** disabling the feature SHALL remove `wind_speed_80m` and `wind_direction_80m` from the weather API request
