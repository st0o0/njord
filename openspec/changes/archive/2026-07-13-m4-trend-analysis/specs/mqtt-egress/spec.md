## ADDED Requirements

### Requirement: TopicScheme provides trend topic helpers
`TopicScheme` SHALL expose `TrendDeviceId(string location)` returning `njord_{slug(location)}_trends` and `TrendTopic(string baseTopic, string location)` returning `{baseTopic}/{slug(location)}/trends`.

#### Scenario: Trend device id
- **WHEN** location is "lucerne"
- **THEN** `TrendDeviceId` returns "njord_lucerne_trends"

#### Scenario: Trend topic
- **WHEN** baseTopic is "njord", location is "lucerne"
- **THEN** `TrendTopic` returns "njord/lucerne/trends"

### Requirement: DiscoveryPayloadBuilder builds a trend device
`DiscoveryPayloadBuilder.BuildTrends` SHALL produce a device-based discovery payload for location with device id `njord_{location}_trends`, model `trends`, and sensor components for: trend direction sensors per primary parameter (temperature, wind_speed, precipitation, cloud_cover) as text sensors, weather_change as a text sensor, precip_starts and precip_ends as numeric sensors (unit "h"), temp_max_in and temp_min_in as numeric sensors (unit "h"), stability as a text sensor, decay_rate as a numeric sensor (unit "°C/h"), and reliable_hours as a numeric sensor (unit "h").

#### Scenario: Trend device payload structure
- **WHEN** `BuildTrends` is called for location "lucerne"
- **THEN** the payload contains device id "njord_lucerne_trends", model "trends"

#### Scenario: Trend direction sensors
- **WHEN** the trend device is built
- **THEN** sensor components exist for trend_temperature, trend_wind_speed, trend_precipitation, trend_cloud_cover — each a text sensor with no unit

#### Scenario: Timing sensors are numeric
- **WHEN** the trend device is built
- **THEN** precip_starts, precip_ends, temp_max_in, temp_min_in have unit "h"

#### Scenario: Decay rate sensor
- **WHEN** the trend device is built
- **THEN** decay_rate has unit "°C/h" and reliable_hours has unit "h"
