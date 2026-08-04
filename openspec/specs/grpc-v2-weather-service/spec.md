# Capability: grpc-v2-weather-service

## Purpose

WeatherService gRPC service for reading forecasts, enrichments, and streaming real-time updates. Replaces v1's ForecastService with unified Timestamp types and a merged GetCatalog RPC.

## Requirements

### Requirement: WeatherService definition
`protos/njord/v2/weather.proto` SHALL define a `WeatherService` with 5 RPCs: `GetCatalog`, `GetForecast`, `GetEnrichments`, `StreamForecasts`, `StreamEnrichments`. The package SHALL be `njord.v2` with `csharp_namespace = "Njord.Grpc.V2"`. It SHALL import `common.proto`.

#### Scenario: Proto compiles with all RPCs
- **WHEN** `dotnet build` runs
- **THEN** gRPC stubs SHALL be generated for all 5 WeatherService RPCs without errors

### Requirement: GetCatalog returns all locations with models and model info
`WeatherService.GetCatalog` SHALL be a unary RPC accepting `GetCatalogRequest` (empty) and returning `GetCatalogResponse` containing `repeated LocationInfo locations` and `repeated ModelInfo models`. The locations SHALL include resolved model lists. The models SHALL include deduplicated ModelInfo for all models across all locations.

#### Scenario: Single call replaces GetLocations + GetModels
- **WHEN** a client calls `GetCatalog` with 2 locations and 5 unique models total
- **THEN** the response SHALL contain 2 `LocationInfo` entries and 5 `ModelInfo` entries

#### Scenario: ModelInfo is deduplicated across locations
- **WHEN** two locations both use "icon_d2"
- **THEN** `models` SHALL contain exactly one `ModelInfo` entry for "icon_d2"

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

### Requirement: GetEnrichments returns all enrichment payloads
`WeatherService.GetEnrichments` SHALL accept `GetEnrichmentsRequest` with `string location` and return `GetEnrichmentsResponse` with `string location` and optional fields for each enrichment type: `AlertUpdate alerts`, `IndexUpdate indices`, `TrendUpdate trends`, `DerivedUpdate derived`, `HistoryUpdate history`, `ConsensusUpdate consensus`.

#### Scenario: All enabled enrichments returned
- **WHEN** a client calls `GetEnrichments` for a location with consensus, alerts, and trends enabled
- **THEN** the response SHALL contain non-null `consensus`, `alerts`, and `trends` payloads

### Requirement: StreamForecasts streams per-model updates
`WeatherService.StreamForecasts` SHALL be a server-streaming RPC accepting `StreamForecastsRequest` with optional `string location` (empty = all). Each `ForecastUpdate` SHALL contain `string location`, `string model`, `google.protobuf.Timestamp updated_at`, `repeated HourlyForecast hourly`, `repeated DailyForecast daily`.

#### Scenario: Stream filters by location
- **WHEN** a client subscribes with `location = "Lucerne"`
- **THEN** only forecast updates for "Lucerne" SHALL be streamed

#### Scenario: Empty location streams all updates
- **WHEN** a client subscribes with empty `location`
- **THEN** forecast updates for all locations SHALL be streamed

### Requirement: StreamEnrichments streams enrichment events
`WeatherService.StreamEnrichments` SHALL be a server-streaming RPC accepting `StreamEnrichmentsRequest` with optional `string location`. Each `EnrichmentEvent` SHALL contain `string location`, `string type_name`, `google.protobuf.Timestamp updated_at`, and a `oneof payload` with all enrichment types.

#### Scenario: Stream delivers enrichment events
- **WHEN** a new enrichment result is computed
- **THEN** all subscribers matching the location filter SHALL receive an `EnrichmentEvent`
