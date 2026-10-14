## MODIFIED Requirements

### Requirement: GetForecast returns forecast with Timestamp
`WeatherService.GetForecast` SHALL accept `GetForecastRequest` with `string location` and `string model`, returning `GetForecastResponse` with `string location`, `string model`, `google.protobuf.Timestamp updated_at`, `repeated HourlyForecast hourly`, `repeated DailyForecast daily`. The location and the model SHALL match the catalog case-insensitively, and the service SHALL query the read model with the configured spelling of both.

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

#### Scenario: Location and model spelled differently return the data
- **WHEN** `lucerne` with model `icon_d2` is configured and has data, and a client calls `GetForecast` with location `LUCERNE` and model `ICON_D2`
- **THEN** the response SHALL contain that forecast with location `lucerne` and model `icon_d2`
