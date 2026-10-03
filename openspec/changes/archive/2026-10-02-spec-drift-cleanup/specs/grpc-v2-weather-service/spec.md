## MODIFIED Requirements

### Requirement: GetForecast returns forecast with Timestamp
`WeatherService.GetForecast` SHALL accept `GetForecastRequest` with `string location` and `string model`, returning `GetForecastResponse` with `string location`, `string model`, `google.protobuf.Timestamp updated_at`, `repeated HourlyForecast hourly`, `repeated DailyForecast daily`.

#### Scenario: Forecast returned for valid location/model
- **WHEN** a client calls `GetForecast` with a configured location and model that has data
- **THEN** the response SHALL contain hourly and daily forecast points with Timestamp fields

#### Scenario: Unknown location returns NOT_FOUND
- **WHEN** a client calls `GetForecast` with an unconfigured location
- **THEN** the RPC SHALL throw a gRPC NOT_FOUND error

#### Scenario: No data yet returns NOT_FOUND
- **WHEN** a client calls `GetForecast` before any poll has completed for that model
- **THEN** the RPC SHALL throw a gRPC NOT_FOUND error

#### Scenario: Unknown model returns NOT_FOUND
- **WHEN** a client calls `GetForecast` with a model that is not configured for the location
- **THEN** the RPC SHALL throw a gRPC NOT_FOUND error

### Requirement: StreamForecasts streams per-model updates
`WeatherService.StreamForecasts` SHALL be a server-streaming RPC accepting `StreamForecastsRequest` with optional `string location` (empty = all). Each `ForecastUpdate` SHALL contain `string location`, `string model`, `google.protobuf.Timestamp updated_at`, `repeated HourlyForecast hourly`, `repeated DailyForecast daily`.

#### Scenario: Stream filters by location
- **WHEN** a client subscribes with `location = "Lucerne"`
- **THEN** only forecast updates for "Lucerne" SHALL be streamed

#### Scenario: Empty location streams all updates
- **WHEN** a client subscribes with empty `location`
- **THEN** forecast updates for all locations SHALL be streamed

#### Scenario: Multiple clients receive the same update
- **WHEN** two clients have active `StreamForecasts` streams and a poll cycle completes
- **THEN** both clients SHALL receive the `ForecastUpdate`

#### Scenario: Stream ends on client disconnect
- **WHEN** a client disconnects from the `StreamForecasts` stream
- **THEN** the server-side stream SHALL be shut down and its egress subscription released
