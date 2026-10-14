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

#### Scenario: Unknown model returns NOT_FOUND
- **WHEN** a client calls `GetForecast` with a model that is not configured for the location
- **THEN** the RPC SHALL throw a gRPC NOT_FOUND error

### Requirement: StreamForecasts streams per-model updates
`WeatherService.StreamForecasts` SHALL be a server-streaming RPC accepting `StreamForecastsRequest` with optional `string location` (empty = all). A non-empty `location` SHALL match a configured location case-insensitively; an unconfigured location SHALL fail the call with gRPC `NOT_FOUND`. Each `ForecastUpdate` SHALL contain `string location`, `string model`, `google.protobuf.Timestamp updated_at`, `repeated HourlyForecast hourly`, `repeated DailyForecast daily`.

#### Scenario: Stream filters by location
- **WHEN** a client subscribes with `location = "Lucerne"`
- **THEN** only forecast updates for "Lucerne" SHALL be streamed

#### Scenario: Empty location streams all updates
- **WHEN** a client subscribes with empty `location`
- **THEN** forecast updates for all locations SHALL be streamed

#### Scenario: Unknown location returns NOT_FOUND
- **WHEN** a client subscribes with a `location` that is not configured
- **THEN** the RPC SHALL throw a gRPC NOT_FOUND error instead of opening a stream that never delivers

#### Scenario: Multiple clients receive the same update
- **WHEN** two clients have active `StreamForecasts` streams and a poll cycle completes
- **THEN** both clients SHALL receive the `ForecastUpdate`

#### Scenario: Stream ends on client disconnect
- **WHEN** a client disconnects from the `StreamForecasts` stream
- **THEN** the server-side stream SHALL be shut down and its egress subscription released

### Requirement: GetEnrichments returns the enrichment payloads
`WeatherService.GetEnrichments` SHALL accept `GetEnrichmentsRequest` with `string location` and return `GetEnrichmentsResponse` with `string location = 1` and optional payload fields `AlertUpdate alerts = 2`, `IndexUpdate indices = 3`, `TrendUpdate trends = 4`, `DerivedUpdate derived = 5`, `ConsensusUpdate consensus = 7` and `google.protobuf.Timestamp consensus_updated_at = 8`. Field number 6 (the removed history payload) SHALL be `reserved`; there SHALL be no energy-related field.

#### Scenario: All enabled enrichments returned
- **WHEN** a client calls `GetEnrichments` for a location with consensus, alerts, and trends enabled
- **THEN** the response SHALL contain non-null `consensus`, `alerts`, and `trends` payloads

#### Scenario: Removed history field stays reserved
- **WHEN** `GetEnrichmentsResponse` is inspected in the proto
- **THEN** field number 6 is `reserved` and no history payload exists

### Requirement: StreamEnrichments streams analysis events
`WeatherService.StreamEnrichments` SHALL be a server-streaming RPC accepting `StreamEnrichmentsRequest` with optional `string location` (empty = all). A non-empty `location` SHALL match a configured location case-insensitively and restrict the stream to that location's events; an unconfigured location SHALL fail the call with gRPC `NOT_FOUND`. Each `EnrichmentEvent` SHALL contain `string location`, `string type_name`, `google.protobuf.Timestamp updated_at`, and a `oneof payload` with the cases `AlertUpdate alerts = 10`, `IndexUpdate indices = 11`, `TrendUpdate trends = 12`, `DerivedUpdate derived = 13` and `ConsensusUpdate consensus = 15` (number 14, the removed history payload, is unused). It SHALL NOT contain energy-related entries.

#### Scenario: Stream delivers enrichment events
- **WHEN** a new analysis result is computed
- **THEN** all subscribers matching the location filter SHALL receive an `EnrichmentEvent`

#### Scenario: Enrichment stream filters by location
- **WHEN** a client subscribes with `location = "Lucerne"` and results are computed for "lucerne" and "zurich"
- **THEN** only the `EnrichmentEvent`s of "lucerne" SHALL be streamed

#### Scenario: Empty location streams all enrichment events
- **WHEN** a client subscribes with empty `location`
- **THEN** enrichment events for all locations SHALL be streamed

#### Scenario: Unknown enrichment location returns NOT_FOUND
- **WHEN** a client subscribes with a `location` that is not configured
- **THEN** the RPC SHALL throw a gRPC NOT_FOUND error

#### Scenario: Oneof without history case
- **WHEN** `EnrichmentEvent` is inspected in the proto
- **THEN** the oneof has the five analysis cases and no history or energy case

