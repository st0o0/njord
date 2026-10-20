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

