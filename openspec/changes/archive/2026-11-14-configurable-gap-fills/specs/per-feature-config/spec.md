## MODIFIED Requirements

### Requirement: Per-feature config type
Each feature SHALL have its own configuration class containing at minimum a `bool Enabled` property with a default matching the feature's `FeatureDescriptor.DefaultEnabled`. Features with tunable constants SHALL include those as config properties with defaults matching the current hardcoded values. Config classes SHALL be bound to their own config section via standard `IOptions<T>` DI binding.

#### Scenario: Feature config bound to flat section
- **WHEN** `FrostConfig` is registered
- **THEN** it SHALL bind to section `Njord:Features:Frost` (not `Njord:Features:Forecast:Frost`)

#### Scenario: Feature names are unique across categories
- **WHEN** all feature config `SectionName` constants are collected
- **THEN** each SHALL follow the pattern `Njord:Features:<Name>` with no duplicate `<Name>` values

#### Scenario: Unset config uses descriptor default
- **WHEN** no config section exists for a feature
- **THEN** the feature's `Enabled` property SHALL default to the value of `FeatureDescriptor.DefaultEnabled`

#### Scenario: Feature-specific thresholds in own config
- **WHEN** `FrostConfig` defines threshold properties
- **THEN** `FrostAlert` SHALL receive those thresholds via `IOptions<FrostConfig>`

#### Scenario: No hardcoded tunable constants in rule logic
- **WHEN** a feature rule uses a numeric threshold, coefficient, or classification boundary that an operator might want to adjust
- **THEN** that value SHALL be a property on the feature's Config class with the current value as default
