# grpc-forecast-service Specification

## Purpose

gRPC service exposing forecast data for configured locations and weather models.
Provides `GetLocations`, `GetModels`, and `GetForecast` RPCs backed by the
`ForecastSnapshotActor` (Akka Persistence). The proto contract is designed for
cross-language consumption (Python, Go, etc.).

## Requirements

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
The `ForecastService.GetForecast` RPC SHALL query the `ForecastSnapshotActor` via Ask to retrieve the latest `ModelForecast`. It SHALL map the `ModelForecast` directly to the proto `GetForecastResponse` without intermediate DTOs. The service SHALL also expose `StreamForecasts` (server-streaming), `GetEnrichments` (unary), and `StreamEnrichments` (server-streaming) RPCs. The existing `GetLocations` and `GetModels` RPCs SHALL remain unchanged. If the actor returns null, the RPC SHALL return gRPC status `NOT_FOUND`.

#### Scenario: Successful forecast query via actor Ask
- **WHEN** a client calls `GetForecast` with location "lucerne" and model "icon_d2"
- **THEN** the service SHALL Ask `ForecastSnapshotActor` for the forecast and map the `ModelForecast` to the proto response

#### Scenario: Hourly forecast points carry core weather fields
- **WHEN** a `GetForecast` response is returned
- **THEN** each hourly point SHALL include: timestamp (unix seconds), temperature, apparent_temperature, precipitation, humidity, wind_speed, wind_bearing, cloud_cover, weather_code, and is_day — with no `condition` field

#### Scenario: Daily forecast points carry aggregate fields
- **WHEN** a `GetForecast` response is returned
- **THEN** each daily point SHALL include: date (ISO 8601), temperature_max, temperature_min, precipitation_sum, sunrise, sunset, and weather_code — with no `condition` field

#### Scenario: Actor timeout returns UNAVAILABLE
- **WHEN** the `ForecastSnapshotActor` does not respond within the timeout
- **THEN** the RPC SHALL return gRPC status `UNAVAILABLE`

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
