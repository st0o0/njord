# per-feature-config Specification

## Purpose
Each feature owns its configuration type with an Enabled flag and its own validated settings.

## Requirements
### Requirement: Per-feature config type
Each feature SHALL have its own configuration class containing at minimum a `bool Enabled` property with a default matching the feature's `FeatureDescriptor.DefaultEnabled`. Alert features SHALL include their specific thresholds. Config classes SHALL be bound to their own config section via standard `IOptions<T>` DI binding.

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

### Requirement: No monolithic options classes
The system SHALL NOT have a single `AlertOptions` class bundling thresholds for all alerts, nor a `FeatureOptions` dictionary for toggle lookup. Each feature's enabled state and settings SHALL be in its own config type.

#### Scenario: AlertOptions removed
- **WHEN** the system is built
- **THEN** there SHALL be no `AlertOptions` class; each alert feature SHALL own its thresholds

#### Scenario: FeatureOptions removed
- **WHEN** the system is built
- **THEN** there SHALL be no `FeatureOptions` dictionary; enabled state SHALL be per-feature config

### Requirement: Config validation per feature
Each per-feature config type with range-constrained values SHALL have a validator registered via `AddNjordOptions<T, TValidator>` (or `AddFeature<TRule, TConfig, TValidator>`) with `ValidateOnStart`. Threshold validators SHALL enforce ordering and count.

#### Scenario: Invalid threshold rejected at startup
- **WHEN** `FrostConfig.Thresholds` has fewer than 3 elements after defaults are applied
- **THEN** startup validation SHALL fail with a descriptive error

#### Scenario: Feature config with array defaults validated after ApplyDefaults
- **WHEN** a config class implements `IHasArrayDefaults` and has a validator
- **THEN** validation SHALL run after `ApplyDefaults()` has filled empty arrays
