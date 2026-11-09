# weather-feature Specification

## Purpose
OutputKind.Weather for the HA weather platform entity as a feature with its own FeatureRule.

## Requirements

### Requirement: Weather OutputKind for HA weather platform
The system SHALL support `OutputKind.Weather` for features that produce the data needed by the Home Assistant weather platform entity. There SHALL be exactly one Weather feature (the `WeatherRule`), always enabled, producing a `WeatherResult` that contains current conditions and an hourly forecast series.

#### Scenario: Weather feature is always enabled
- **WHEN** njord starts with any configuration
- **THEN** the WeatherRule SHALL be registered and enabled (it has no user-facing `Enabled` toggle)

### Requirement: WeatherResult contains current conditions and forecast
`WeatherResult` SHALL contain the current weather conditions (temperature, humidity, pressure, wind speed, wind bearing, weather code/condition) and an hourly forecast array. Each forecast entry SHALL contain at minimum: timestamp, temperature, humidity, precipitation, wind speed, wind bearing, and condition.

#### Scenario: WeatherResult maps to HA weather schema
- **WHEN** a WeatherResult is produced
- **THEN** it SHALL contain all fields required by the HA weather platform: temperature, humidity, pressure, wind_speed, wind_bearing, and condition as current state, plus forecast_hourly entries

#### Scenario: Forecast entries cover the configured forecast window
- **WHEN** Forecast is configured with ForecastDays = 4
- **THEN** the WeatherResult forecast array SHALL contain up to 96 hourly entries (4 days x 24 hours)

### Requirement: Weather feature requires Forecast domain
The WeatherRule's `FeatureDescriptor` SHALL declare `Requirements` including the Forecast domain with at minimum Temperature, WeatherCode, Humidity, WindSpeed, and Pressure parameters.

#### Scenario: Weather feature skips without forecast data
- **WHEN** the Forecast domain has not been polled yet for a location
- **THEN** the WeatherRule's `Project` SHALL return null and no WeatherResult is produced

### Requirement: Weather EntityMetadata declares weather platform
The WeatherRule's `FeatureDescriptor` SHALL include `EntityMetadata` with `Platform = Weather`. This is the only feature that uses the `Weather` platform value.

#### Scenario: Weather entity appears in manifest as weather platform
- **WHEN** the manifest is built
- **THEN** each location SHALL have exactly one entity with `platform = weather` from the primary WeatherRule

### Requirement: Secondary model weather entities
For each enabled secondary model, the system SHALL produce a separate weather entity with the same structure but data from the secondary model's forecast. These entities SHALL appear in the manifest with distinct `entity_id` values including the model slug.

#### Scenario: Secondary model weather entity in manifest
- **WHEN** 2 secondary Ensemble models are enabled
- **THEN** each location SHALL have 2 additional weather entities in the manifest, one per model
