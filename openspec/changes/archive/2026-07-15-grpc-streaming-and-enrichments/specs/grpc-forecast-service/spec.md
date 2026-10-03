## MODIFIED Requirements

### Requirement: ForecastService returns forecast data for a model
The `ForecastService` SHALL expose `GetForecast` (unary), `StreamForecasts` (server-streaming), `GetEnrichments` (unary), and `StreamEnrichments` (server-streaming) RPCs. The existing `GetLocations` and `GetModels` RPCs SHALL remain unchanged. The service SHALL serve both unary queries for initial state and streaming subscriptions for real-time updates.

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

### Requirement: Proto files define the service contract
The service contract SHALL be defined in `protos/njord/v1/forecast_service.proto` using proto3 syntax. The file SHALL include all forecast and enrichment RPC definitions, forecast messages, and enrichment messages. The `Njord.csproj` SHALL generate C# server stubs from all proto files via `Grpc.Tools`.

#### Scenario: Proto file compiles for C# server
- **WHEN** `dotnet build` runs
- **THEN** gRPC server stubs SHALL be generated from all `protos/njord/v1/*.proto` files without errors

#### Scenario: Proto file compiles for Python client
- **WHEN** `python -m grpc_tools.protoc` runs against the proto files
- **THEN** Python client stubs SHALL be generated without errors
