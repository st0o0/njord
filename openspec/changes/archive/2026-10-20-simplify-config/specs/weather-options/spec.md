## ADDED Requirements

### Requirement: WeatherOptions controls weather-specific settings
The system SHALL provide a `WeatherOptions` class bound to `Njord:Weather` with `IncludeSolar` (bool, default false) and `ForecastDays` (int, default 4). When IncludeSolar is true, the weather client SHALL include solar radiation variables in the Ensemble request.

#### Scenario: Default weather request without solar
- **WHEN** IncludeSolar is false (default)
- **THEN** the Ensemble URL SHALL NOT include shortwave_radiation, direct_radiation, diffuse_radiation or sunshine_duration

#### Scenario: Solar variables included when toggled
- **WHEN** IncludeSolar is true
- **THEN** the Ensemble URL SHALL include shortwave_radiation, direct_radiation, diffuse_radiation, direct_normal_irradiance and sunshine_duration

#### Scenario: ForecastDays controls request range
- **WHEN** ForecastDays is 7
- **THEN** the Ensemble and AirQuality requests SHALL use forecast_days=7

### Requirement: Weather client publishes its variable list
The weather client SHALL expose the list of hourly variable names it requests. MQTT and budget consumers SHALL read this list instead of resolving it from config.

#### Scenario: MQTT weather topics match client variables
- **WHEN** the weather client requests temperature_2m, wind_speed_10m and 6 other variables
- **THEN** MQTT SHALL publish topics for exactly those 8 variables
