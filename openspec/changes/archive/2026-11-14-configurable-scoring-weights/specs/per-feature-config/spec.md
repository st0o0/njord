## MODIFIED Requirements

### Requirement: Per-feature config type
Each feature SHALL have its own configuration class containing at minimum a `bool Enabled` property with a default matching the feature's `FeatureDescriptor.DefaultEnabled`. Alert features SHALL include their specific thresholds. Scoring features with component weights SHALL include their weight, band/ramp and reference parameters as named properties. Config classes SHALL be bound to their own config section via standard `IOptions<T>` DI binding.

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

#### Scenario: Scoring feature weights in own config
- **WHEN** `BbqConfig` defines weight properties
- **THEN** `BbqScore` SHALL receive those weights via `IOptions<BbqConfig>` and use them for score computation

### Requirement: Config validation per feature
Each per-feature config type with range-constrained values SHALL have a validator registered via `AddNjordOptions<T, TValidator>` (or `AddFeature<TRule, TConfig, TValidator>`) with `ValidateOnStart`. Threshold validators SHALL enforce ordering and count. Weight validators SHALL enforce the sum-to-1.0 constraint.

#### Scenario: Invalid threshold rejected at startup
- **WHEN** `FrostConfig.Thresholds` has fewer than 3 elements after defaults are applied
- **THEN** startup validation SHALL fail with a descriptive error

#### Scenario: Feature config with array defaults validated after ApplyDefaults
- **WHEN** a config class implements `IHasArrayDefaults` and has a validator
- **THEN** validation SHALL run after `ApplyDefaults()` has filled empty arrays

#### Scenario: All multi-parameter configs have validators
- **WHEN** the set of features registered with `AddFeature<TRule, TConfig>()` (2-param) is collected
- **THEN** none SHALL have config properties beyond `Enabled` without a corresponding validator

#### Scenario: Weight sum validated at startup
- **WHEN** a scoring config has component weights that do not sum to 1.0 within 0.001
- **THEN** startup validation SHALL fail naming the section and the actual sum
