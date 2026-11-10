## MODIFIED Requirements

### Requirement: WeatherResult contains current conditions and forecast
`WeatherResult` SHALL contain the current weather conditions (temperature, humidity, pressure, wind speed, wind bearing, weather code/condition, cloud cover, dew point, visibility, precipitation, wind gusts) and an hourly forecast array. Each forecast entry SHALL contain at minimum: timestamp, temperature, humidity, precipitation, wind speed, wind bearing, condition, precipitation probability, and cloud cover.

#### Scenario: WeatherResult maps to HA weather schema
- **WHEN** a WeatherResult is produced
- **THEN** it SHALL contain all fields required by the HA weather platform: temperature, humidity, pressure, wind_speed, wind_bearing, condition, cloud_cover, dew_point, visibility, and wind_gusts as current state, plus forecast_hourly entries

#### Scenario: Forecast entries cover the configured forecast window
- **WHEN** Forecast is configured with ForecastDays = 4
- **THEN** the WeatherResult forecast array SHALL contain up to 96 hourly entries (4 days x 24 hours)

### Requirement: Weather feature requires Forecast domain
The WeatherRule's `FeatureDescriptor` SHALL declare `Requirements` including the Forecast domain with at minimum Temperature, WeatherCode, Humidity, WindSpeed, WindDirection, Pressure, ApparentTemperature, CloudCover, DewPoint, Visibility, WindGusts, and Precipitation parameters.

#### Scenario: Weather feature requests extended parameters
- **WHEN** njord starts with WeatherRule enabled
- **THEN** the resolved DomainRequirements SHALL include Forecast params for CloudCover, DewPoint, Visibility, WindGusts, and Precipitation in addition to the existing params

#### Scenario: Missing optional fields produce null
- **WHEN** the Forecast domain does not include a requested optional field (e.g. visibility is null at the current time)
- **THEN** the corresponding WeatherResult field SHALL be null and the HA entity SHALL show no value for that attribute

## ADDED Requirements

### Requirement: Proto WeatherValue includes extended current fields
The gRPC `WeatherValue` message SHALL include optional fields for cloud_cover, dew_point, visibility, precipitation, and wind_gusts. These fields SHALL be mapped from the corresponding WeatherResult fields.

#### Scenario: Extended fields delivered via gRPC
- **WHEN** a WeatherResult with cloud_cover=75, dew_point=14.2, visibility=39520, precipitation=0.0, wind_gusts=8.1 is produced
- **THEN** the gRPC EntityStateUpdate for that weather entity SHALL include those values in the WeatherValue message

### Requirement: ha-njord exposes all weather attributes
The ha-njord weather entity SHALL expose native_wind_bearing, native_dew_point, cloud_coverage, native_visibility, and wind gust attributes from the gRPC WeatherValue.

#### Scenario: wind_direction visible in HA
- **WHEN** the gRPC WeatherValue contains wind_direction = 263
- **THEN** the HA weather entity SHALL show wind_bearing = 263

#### Scenario: Cloud cover visible in HA
- **WHEN** the gRPC WeatherValue contains cloud_cover = 100
- **THEN** the HA weather entity SHALL show cloud_coverage = 100
