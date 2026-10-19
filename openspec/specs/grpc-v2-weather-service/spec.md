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

### Requirement: GetDomainData returns ensemble data with optional per-model forecasts
`WeatherService.GetDomainData` SHALL return ensemble mean values with standard deviation instead of per-model consensus. Optional per-model forecasts MAY be included if individual models are configured.

#### Scenario: Ensemble forecast response
- **WHEN** a client calls GetDomainData for location "Lucerne"
- **THEN** the response SHALL contain ensemble mean and std dev for each parameter and horizon

### Requirement: StreamDomainData streams per-domain updates
`WeatherService.StreamDomainData` SHALL be a server-streaming RPC accepting optional location and domain filters. Each update delivers domain-specific data messages (WeatherData, AirQualityData, etc.).

#### Scenario: Stream filters by location and domain
- **WHEN** a client subscribes with location "Lucerne" and domain filter [Weather]
- **THEN** only weather data updates for "Lucerne" SHALL be streamed

#### Scenario: Empty filters stream all updates
- **WHEN** a client subscribes with no filters
- **THEN** all domain data updates for all locations SHALL be streamed

### Requirement: GetEnrichments returns domain-aware enrichments
`WeatherService.GetEnrichments` SHALL return alerts from all domains (weather alerts, air quality alerts, flood alerts) instead of only weather-derived enrichments. Old consensus, derived, trend, and index payloads are removed.

#### Scenario: Cross-domain enrichments
- **WHEN** a client calls GetEnrichments for location "Lucerne"
- **THEN** the response SHALL include weather alerts AND air quality alerts if both domains are enabled

### Requirement: StreamEnrichments streams analysis events
`WeatherService.StreamEnrichments` SHALL be a server-streaming RPC accepting optional `string location` (empty = all). Each `EnrichmentEvent` SHALL contain location, alert type, and alert payload. Old consensus, derived, trend, and index event types are removed.

#### Scenario: Stream delivers alert events
- **WHEN** a new alert is raised
- **THEN** all subscribers matching the location filter SHALL receive an EnrichmentEvent

#### Scenario: Enrichment stream filters by location
- **WHEN** a client subscribes with `location = "Lucerne"` and alerts are raised for "lucerne" and "zurich"
- **THEN** only the alerts for "lucerne" SHALL be streamed

