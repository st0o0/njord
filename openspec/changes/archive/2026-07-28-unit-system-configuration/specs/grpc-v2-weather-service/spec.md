## MODIFIED Requirements

### Requirement: GetCatalog returns all locations with models and model info
`WeatherService.GetCatalog` SHALL be a unary RPC accepting `GetCatalogRequest` (empty) and returning `GetCatalogResponse` containing `repeated LocationInfo locations`, `repeated ModelInfo models`, and `repeated ParameterMeta parameters`. The locations SHALL include resolved model lists. The models SHALL include deduplicated ModelInfo for all models across all locations. The parameters SHALL include one `ParameterMeta` entry per parameter in the active resolved parameter set, with the `unit` field reflecting the configured `UnitSystem`.

#### Scenario: Single call replaces GetLocations + GetModels
- **WHEN** a client calls `GetCatalog` with 2 locations and 5 unique models total
- **THEN** the response SHALL contain 2 `LocationInfo` entries and 5 `ModelInfo` entries

#### Scenario: ModelInfo is deduplicated across locations
- **WHEN** two locations both use "icon_d2"
- **THEN** `models` SHALL contain exactly one `ModelInfo` entry for "icon_d2"

#### Scenario: Parameters reflect active unit system
- **WHEN** a client calls `GetCatalog` with unit system `Imperial`
- **THEN** the `parameters` list contains entries with imperial units (e.g. `{name: "temperature_2m", unit: "°F"}`)

#### Scenario: Parameters match resolved parameter set
- **WHEN** the parameter configuration excludes `cloud_cover`
- **THEN** the `parameters` list does NOT contain an entry for `cloud_cover`
