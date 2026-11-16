# feature-catalog Specification

## Purpose
Catalog of configurable features: type identity, descriptors, and resolution of requirements and cost from the enabled features.
## Requirements
### Requirement: Feature resolution reports requirements and cost
Feature resolution SHALL expose all registered features, the enabled features, the union of their required parameters, the required domains, and the marginal monthly API cost of each feature, computed with the same formula as startup budget validation.

#### Scenario: Domain already active
- **WHEN** CommuteSafety is enabled and FrostAlert is evaluated
- **THEN** FrostAlert's cost is 0 calls because Weather is already required

#### Scenario: Feature adds a domain
- **WHEN** no enabled feature needs AirQuality and PollenAlert is evaluated for one location at 60 minute polling
- **THEN** its cost equals the monthly AirQuality call count of that location

#### Scenario: Cost matches startup validation
- **WHEN** the total of enabled features is computed
- **THEN** it equals the projected usage `BudgetGuard` validates at startup

### Requirement: Features are identified by their C# type
Every feature SHALL be identified by its C# type, not by a string name, a feature-kind enum member, or any other textual identifier. The feature type IS the identity.

#### Scenario: Feature type uniqueness
- **WHEN** all registered features are listed
- **THEN** no two features SHALL share the same C# type

### Requirement: FeatureDescriptor carries identity and metadata
`FeatureDescriptor` SHALL contain `OutputKind`, `FeatureCategory`, `Name` (the config section name), `bool DefaultEnabled`, `DomainRequirements Requirements`, `int PollDepth`, and `EntityMetadata` (platform/device_class/icon/unit for HA entity definition). Config binding uses `Njord:Features:<Name>`.

#### Scenario: Descriptor content
- **WHEN** the FrostAlert feature descriptor is inspected
- **THEN** it reports category Weather, output Alert, default enabled true, Name "Frost", its required forecast parameters, PollDepth 0, and EntityMetadata for entity definition

### Requirement: Feature resolution from per-feature config
Feature resolution SHALL compute enabled features by checking each feature's own `IOptions<T>.Enabled` property, not by looking up a string key in a `FeatureOptions` dictionary. The union of requirements, required domains, and cost computation SHALL use the same formulas as before.

#### Scenario: Feature disabled via its own config
- **WHEN** `FrostConfig.Enabled` is false
- **THEN** FrostAlert SHALL be excluded from enabled features and its requirements SHALL NOT contribute to the resolved requirements

#### Scenario: Cost matches startup validation
- **WHEN** the total of enabled features' requirements is computed
- **THEN** it SHALL equal the projected usage `BudgetGuard` validates at startup

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
- **WHEN** NjordGrpcService builds the catalog
- **THEN** it SHALL use `IOptions<ActiveModels>` and `IOptions<NjordOptions>`, not `IOptions<EnsembleOptions>`

