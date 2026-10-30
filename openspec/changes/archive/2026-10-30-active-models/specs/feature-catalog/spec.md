## ADDED Requirements

### Requirement: Catalog includes forecast and ensemble models
The GetCatalog response SHALL list all active models from both ForecastOptions and
EnsembleOptions. Each `CatalogLocation.Models` SHALL contain the primary forecast model
first, followed by secondary forecast models, followed by ensemble models. `CatalogModel`
entries SHALL exist for every model in the list.

#### Scenario: Default configuration (best_match + 1 ensemble)
- **WHEN** ForecastOptions.Models is empty and EnsembleOptions resolves to `["ecmwf_ifs025_ensemble_mean"]`
- **THEN** `CatalogLocation.Models` SHALL be `["best_match", "ecmwf_ifs025_ensemble_mean"]` and two `CatalogModel` entries SHALL exist

#### Scenario: Explicit forecast models + ensemble models
- **WHEN** ForecastOptions.Models is `["icon_d2", "icon_eu"]` and EnsembleOptions.Models is `["ecmwf_ifs025_ensemble_mean"]`
- **THEN** `CatalogLocation.Models` SHALL be `["icon_d2", "icon_eu", "ecmwf_ifs025_ensemble_mean"]` with `icon_d2` as primary

#### Scenario: Catalog sourced from ActiveModels
- **WHEN** WeatherGrpcService builds the catalog
- **THEN** it SHALL use `IOptions<ActiveModels>` and `IOptions<NjordOptions>`, not `IOptions<EnsembleOptions>`
