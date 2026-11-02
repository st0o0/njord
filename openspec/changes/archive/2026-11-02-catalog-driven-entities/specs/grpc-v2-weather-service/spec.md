## MODIFIED Requirements

### Requirement: GetCatalog returns all locations with models and model info
`WeatherService.GetCatalog` SHALL be a unary RPC accepting `GetCatalogRequest` (empty) and returning `GetCatalogResponse` containing `repeated LocationInfo locations` and `repeated ModelInfo models`. The locations SHALL include resolved model lists. The models SHALL include deduplicated ModelInfo for all models across all locations. Each `LocationInfo` SHALL additionally include `repeated DomainSchema resolved_domains` listing the active domains and their fetched field names.

#### Scenario: Single call replaces GetLocations + GetModels
- **WHEN** a client calls `GetCatalog` with 2 locations and 5 unique models total
- **THEN** the response SHALL contain 2 `LocationInfo` entries and 5 `ModelInfo` entries

#### Scenario: ModelInfo is deduplicated across locations
- **WHEN** two locations both use "icon_d2"
- **THEN** `models` SHALL contain exactly one `ModelInfo` entry for "icon_d2"

#### Scenario: LocationInfo includes resolved domain schemas
- **WHEN** a client calls `GetCatalog` for a location with Forecast, AirQuality, and Marine domains active
- **THEN** the `LocationInfo` SHALL contain 3 `DomainSchema` entries, each with a `Domain` enum and `repeated string fields` listing the proto field names of fetched variables
