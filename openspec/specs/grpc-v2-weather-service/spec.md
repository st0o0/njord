# Capability: grpc-v2-weather-service

## Purpose

WeatherService gRPC service for reading domain data, enrichments, and streaming real-time updates. Provides GetCatalog, GetDomainData, GetEnrichments, StreamDomainData, and StreamEnrichments RPCs for multi-domain weather intelligence.
## Requirements
### Requirement: WeatherService definition
`protos/njord/v2/weather.proto` SHALL define a `WeatherService` with 5 RPCs: `GetCatalog`, `GetDomainData`, `GetEnrichments`, `StreamDomainData`, `StreamEnrichments`. The package SHALL be `njord.v2` with `csharp_namespace = "Njord.Grpc.V2"`. It SHALL import `common.proto`. GetForecast and StreamForecasts are removed and their field numbers reserved.

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

### Requirement: StreamDomainData streams per-domain updates
`WeatherService.StreamDomainData` SHALL be a server-streaming RPC accepting optional location and domain filters. Each update delivers domain-specific data messages (WeatherData, AirQualityData, etc.).

#### Scenario: Stream filters by location and domain
- **WHEN** a client subscribes with location "Lucerne" and domain filter [Weather]
- **THEN** only weather data updates for "Lucerne" SHALL be streamed

#### Scenario: Empty filters stream all updates
- **WHEN** a client subscribes with no filters
- **THEN** all domain data updates for all locations SHALL be streamed

### Requirement: StreamEnrichments streams typed feature events
`WeatherService.StreamEnrichments` SHALL be a server-streaming RPC. Each event SHALL carry `string location`, `google.protobuf.Timestamp updated_at`, and a `oneof feature` with one typed message per feature (e.g. `FrostAlert frost`, `BbqScore bbq`). The `type_name` string field and the generic `AlertUpdate`/`LifestyleUpdate` payloads SHALL be removed.

#### Scenario: Stream emits typed frost alert
- **WHEN** the frost feature triggers for location "Zurich"
- **THEN** the event SHALL have `location = "Zurich"`, `updated_at` set, and `frost` field populated with `AlertData` plus `min_temperature` and `dew_point`

#### Scenario: Stream emits typed score
- **WHEN** the BBQ score updates for location "Zurich"
- **THEN** the event SHALL have `bbq` field populated with `ScoreData` containing `score`, `best_start`, `best_hours`

### Requirement: GetEnrichments returns typed feature results
`WeatherService.GetEnrichments` SHALL return enrichment data using the same typed feature messages as the stream. The response SHALL carry `string location` and a `oneof feature` per result (or repeated messages). The generic `AlertUpdate` and `repeated LifestyleUpdate` fields SHALL be removed.

#### Scenario: GetEnrichments returns typed results
- **WHEN** a client calls `GetEnrichments` for a location with frost alert active and BBQ score computed
- **THEN** the response SHALL contain typed `FrostAlert` and `BbqScore` messages

### Requirement: Per-model weather data
The weather service SHALL accept an optional `model` in `GetDomainDataRequest` and SHALL set `model` on every weather `DomainDataUpdate`. An empty request model SHALL select the primary model. `StreamDomainData` SHALL emit one weather update per effective model.

#### Scenario: Default request
- **WHEN** a client calls `GetDomainData` for Weather without a model
- **THEN** the response carries the primary model's data and its model id

#### Scenario: Named model
- **WHEN** a client requests an enabled secondary model
- **THEN** the response carries that model's points and `model` set to its id

#### Scenario: Unknown or disabled model
- **WHEN** a client requests a model that is not effective
- **THEN** the call fails with `NOT_FOUND`

#### Scenario: Non-weather domains
- **WHEN** air quality, flood or marine data is served
- **THEN** the `model` field is empty

### Requirement: Catalog reports enabled models
`GetCatalog` SHALL list the catalog models with an `enabled` flag that is true exactly for the effective models.

#### Scenario: Two enabled models
- **WHEN** two of five catalog models are effective
- **THEN** exactly those two have `enabled = true`
