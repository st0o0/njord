## ADDED Requirements

### Requirement: ForecastService exposes location and model metadata
The gRPC `ForecastService` SHALL expose a `GetLocations` RPC returning all configured location names, and a `GetModels` RPC returning all configured model IDs for a given location.

#### Scenario: GetLocations returns configured locations
- **WHEN** a client calls `GetLocations`
- **THEN** the response SHALL contain all location names from `NjordOptions.Locations` (e.g. `["lucerne"]`)

#### Scenario: GetModels returns resolved models for a location
- **WHEN** a client calls `GetModels` with location `"lucerne"`
- **THEN** the response SHALL contain all model IDs resolved for that location (e.g. `["icon_d2", "ecmwf_ifs025", ...]`)

#### Scenario: GetModels for unknown location returns NOT_FOUND
- **WHEN** a client calls `GetModels` with a location not in the configuration
- **THEN** the RPC SHALL return gRPC status `NOT_FOUND`

### Requirement: ForecastService returns forecast data for a model
The `ForecastService` SHALL expose a `GetForecast` RPC accepting a location and model ID. It SHALL return the latest forecast snapshot including the HA-compatible weather condition, hourly forecast points, and daily forecast points.

#### Scenario: Successful forecast query
- **WHEN** a client calls `GetForecast` with location `"lucerne"` and model `"icon_d2"` and a snapshot exists
- **THEN** the response SHALL contain the location, model, current condition string, a list of hourly forecast points, and a list of daily forecast points

#### Scenario: Hourly forecast points carry core weather fields
- **WHEN** a `GetForecast` response is returned
- **THEN** each hourly point SHALL include: timestamp (unix seconds), temperature, apparent_temperature, precipitation, humidity, wind_speed, wind_bearing, cloud_cover, and weather_code

#### Scenario: Daily forecast points carry aggregate fields
- **WHEN** a `GetForecast` response is returned
- **THEN** each daily point SHALL include: date (ISO 8601), temperature_max, temperature_min, precipitation_sum, sunrise, and sunset

#### Scenario: No snapshot available returns NOT_FOUND
- **WHEN** a client calls `GetForecast` for a valid (location, model) but no forecast data has been received yet
- **THEN** the RPC SHALL return gRPC status `NOT_FOUND`

#### Scenario: Unknown model returns NOT_FOUND
- **WHEN** a client calls `GetForecast` with a model not configured for the location
- **THEN** the RPC SHALL return gRPC status `NOT_FOUND`

### Requirement: Proto files define the service contract
The service contract SHALL be defined in `protos/njord/v1/forecast_service.proto` using proto3 syntax. The `Njord.csproj` SHALL generate C# server stubs from these proto files via `Grpc.Tools`.

#### Scenario: Proto file compiles for C# server
- **WHEN** `dotnet build` runs
- **THEN** gRPC server stubs SHALL be generated from `protos/njord/v1/forecast_service.proto` without errors

#### Scenario: Proto file compiles for Python client
- **WHEN** `python -m grpc_tools.protoc` runs against the same proto file
- **THEN** Python client stubs SHALL be generated without errors
