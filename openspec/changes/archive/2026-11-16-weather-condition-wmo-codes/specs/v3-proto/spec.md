## MODIFIED Requirements

### Requirement: Proto WeatherValue includes extended current fields
The gRPC `WeatherValue` message SHALL include optional fields for cloud_cover, dew_point, visibility, precipitation, wind_gusts, **weather_code** (int32), and **is_day** (bool). The weather_code and is_day fields SHALL be mapped from the corresponding WeatherResult fields. The existing `condition` string field SHALL remain for human-readable diagnostics.

#### Scenario: WeatherValue carries WMO code over gRPC
- **WHEN** a WeatherResult with WeatherCode = 45, IsDay = true is mapped to proto
- **THEN** the gRPC WeatherValue SHALL include weather_code = 45 and is_day = true alongside condition = "fog"

#### Scenario: WeatherForecastPoint carries WMO code over gRPC
- **WHEN** a WeatherForecastPoint with WeatherCode = 80, IsDay = false is mapped to proto
- **THEN** the gRPC WeatherForecastPoint SHALL include weather_code = 80 and is_day = false alongside condition = "showers"

#### Scenario: New fields use next available field numbers
- **WHEN** the proto is updated
- **THEN** WeatherValue SHALL use field numbers 14 (weather_code) and 15 (is_day); WeatherForecastPoint SHALL use field numbers 12 (weather_code) and 13 (is_day)

#### Scenario: Wire compatibility with older ha-njord clients
- **WHEN** an older ha-njord client that does not know about weather_code/is_day connects
- **THEN** it SHALL continue to function using the condition string field (graceful degradation)
