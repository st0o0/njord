## ADDED Requirements

### Requirement: FeatureEngine runs WeatherRule against per-model data
When model-specific forecast data arrives (DomainDataReceived with a non-null ModelId), the FeatureEngine SHALL run weather-capable feature rules against that data instead of returning immediately after storing it. The existing behavior of storing model data in LocationForecast SHALL be preserved.

#### Scenario: Ensemble model data triggers WeatherRule
- **WHEN** the FeatureEngine receives a DomainDataReceived for DomainKind.Ensemble with ModelId "ecmwf_ifs025_ensemble_mean"
- **THEN** it SHALL store the model data in LocationForecast AND run WeatherRule against it, producing a WeatherResult with ModelId "ecmwf_ifs025_ensemble_mean"

#### Scenario: Primary forecast model data triggers WeatherRule
- **WHEN** the FeatureEngine receives a DomainDataReceived for DomainKind.Forecast with a non-null ModelId
- **THEN** it SHALL store the model data AND run WeatherRule against it, producing a WeatherResult with the corresponding ModelId

#### Scenario: Only weather-capable rules run on model data
- **WHEN** model-specific data arrives
- **THEN** only rules whose requirements are satisfied by the model's data type SHALL be evaluated; alert, score, condition, and observation rules SHALL NOT run against model data

### Requirement: WeatherResult carries optional ModelId
WeatherResult SHALL have an optional ModelId field (string, nullable). When null, the result represents the primary weather entity. When set, it identifies the model that produced the data.

#### Scenario: Primary weather result has no ModelId
- **WHEN** WeatherRule evaluates primary Forecast data (no model context)
- **THEN** the resulting WeatherResult SHALL have ModelId = null

#### Scenario: Per-model weather result carries ModelId
- **WHEN** WeatherRule evaluates model-specific data for "ukmo_global_ensemble_mean_20km"
- **THEN** the resulting WeatherResult SHALL have ModelId = "ukmo_global_ensemble_mean_20km"

### Requirement: Per-model weather entities receive state updates
The GrpcConsumer SHALL deliver EntityStateUpdate messages for per-model weather entities. Each per-model entity SHALL show the same attributes as the primary weather entity (temperature, humidity, wind, pressure, condition, hourly forecast) but sourced from that model's data.

#### Scenario: HA shows per-model weather entity with data
- **WHEN** ensemble model "ecmwf_ifs025_ensemble_mean" has been polled and WeatherRule has produced a result
- **THEN** the HA entity `weather.barcelona_2` (mapped from `barcelona_ecmwf_ifs025_ensemble_mean_weather`) SHALL show temperature, humidity, wind, pressure, condition, and hourly forecast from that model

#### Scenario: Per-model entity is unavailable before model data arrives
- **WHEN** the Ensemble domain has not yet been polled
- **THEN** per-model weather entities SHALL remain unavailable
