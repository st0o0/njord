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

#### Scenario: No Name property
- **WHEN** a feature is inspected
- **THEN** it SHALL NOT expose a `Name` property or any string-returning identity method

### Requirement: FeatureDescriptor without Name dependency
`FeatureDescriptor` SHALL contain `OutputKind`, `FeatureCategory`, `bool DefaultEnabled`, `DomainRequirements Requirements`, and `int PollDepth`. It SHALL NOT reference a name or string identifier. The previous `IFeature.Name`-based config key derivation (`Category:Name`) SHALL be replaced by per-feature `IOptions<T>` binding.

#### Scenario: Descriptor content
- **WHEN** the FrostAlert feature descriptor is inspected
- **THEN** it reports category Weather, output Alert, default enabled true, its required weather parameters, and PollDepth 0, but no name

### Requirement: Feature resolution from per-feature config
Feature resolution SHALL compute enabled features by checking each feature's own `IOptions<T>.Enabled` property, not by looking up a string key in a `FeatureOptions` dictionary. The union of requirements, required domains, and cost computation SHALL use the same formulas as before.

#### Scenario: Feature disabled via its own config
- **WHEN** `FrostConfig.Enabled` is false
- **THEN** FrostAlert SHALL be excluded from enabled features and its requirements SHALL NOT contribute to the resolved requirements

#### Scenario: Cost matches startup validation
- **WHEN** the total of enabled features' requirements is computed
- **THEN** it SHALL equal the projected usage `BudgetCalculator` validates at startup
