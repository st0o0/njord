## MODIFIED Requirements

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

## REMOVED Requirements

### Requirement: Features are identified by a closed kind
**Reason**: Replaced by C# type-based identity. The closed feature-kind enum is deleted. Features are identified by their concrete type, not by an enum member.
**Migration**: Replace all `FeatureKind.X` references with `typeof(XFeature)` or pattern matching on the feature type.

### Requirement: Feature kinds map to configuration keys explicitly
**Reason**: Replaced by per-feature `IOptions<T>` binding. Each feature has its own config section, no explicit kind-to-key mapping needed.
**Migration**: Config keys are now section paths bound at registration time via `AddNjordOptions<FrostConfig>(config, "Njord:Features:Weather:Frost")`.

### Requirement: Features are toggled individually
**Reason**: Superseded by per-feature config. The `Enabled` flag is on each feature's own config type, not in a shared `FeatureOptions` dictionary.
**Migration**: `Njord:Features:Weather:Frost` changes from `true` (flat boolean) to `{ "Enabled": true, "Moderate": 2.0, ... }` (object).

### Requirement: Legacy enrichment keys migrate
**Reason**: The old `Njord:Enrichments` section was already removed in a prior change. No migration code needed.
**Migration**: None required.
