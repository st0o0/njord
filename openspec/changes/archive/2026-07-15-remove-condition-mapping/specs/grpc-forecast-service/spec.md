## MODIFIED Requirements

### Requirement: ForecastService returns forecast data for a model
The `ForecastService` SHALL expose a `GetForecast` RPC accepting a location and model ID. It SHALL return the latest forecast snapshot including hourly forecast points and daily forecast points. The response SHALL NOT include pre-mapped condition strings — consumers derive conditions from the raw `weather_code` and `is_day` fields.

#### Scenario: Successful forecast query
- **WHEN** a client calls `GetForecast` with location `"lucerne"` and model `"icon_d2"` and a snapshot exists
- **THEN** the response SHALL contain the location, model, update timestamp, a list of hourly forecast points, and a list of daily forecast points — with no `condition` field

#### Scenario: Hourly forecast points carry core weather fields
- **WHEN** a `GetForecast` response is returned
- **THEN** each hourly point SHALL include: timestamp (unix seconds), temperature, apparent_temperature, precipitation, humidity, wind_speed, wind_bearing, cloud_cover, weather_code, and is_day — with no `condition` field

#### Scenario: Daily forecast points carry aggregate fields
- **WHEN** a `GetForecast` response is returned
- **THEN** each daily point SHALL include: date (ISO 8601), temperature_max, temperature_min, precipitation_sum, sunrise, sunset, and weather_code — with no `condition` field

#### Scenario: No snapshot available returns NOT_FOUND
- **WHEN** a client calls `GetForecast` for a valid (location, model) but no forecast data has been received yet
- **THEN** the RPC SHALL return gRPC status `NOT_FOUND`

#### Scenario: Unknown model returns NOT_FOUND
- **WHEN** a client calls `GetForecast` with a model not configured for the location
- **THEN** the RPC SHALL return gRPC status `NOT_FOUND`
