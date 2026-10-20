# per-feature-config Specification

## Purpose
Each feature owns its configuration type with an Enabled flag and its own validated settings.

## Requirements
### Requirement: Per-feature config type
Each feature SHALL have its own configuration class containing at minimum a `bool Enabled` property with a default matching the feature's `FeatureDescriptor.DefaultEnabled`. Alert features SHALL include their specific thresholds. Config classes SHALL be bound to their own config section via standard `IOptions<T>` DI binding.

#### Scenario: Feature config bound to specific section
- **WHEN** `FrostConfig` is registered with section `Njord:Features:Weather:Frost`
- **THEN** the feature SHALL read its `Enabled` flag and `Moderate`, `Warning`, `Severe` thresholds from that section

#### Scenario: Unset config uses descriptor default
- **WHEN** no config section exists for a feature
- **THEN** the feature's `Enabled` property SHALL default to the value of `FeatureDescriptor.DefaultEnabled`

#### Scenario: Feature-specific thresholds in own config
- **WHEN** `FrostConfig` defines `Moderate`, `Warning`, `Severe` threshold properties
- **THEN** `FrostAlert` SHALL receive those thresholds via `IOptions<FrostConfig>`, not from a shared `AlertOptions`

### Requirement: No monolithic options classes
The system SHALL NOT have a single `AlertOptions` class bundling thresholds for all alerts, nor a `FeatureOptions` dictionary for toggle lookup. Each feature's enabled state and settings SHALL be in its own config type.

#### Scenario: AlertOptions removed
- **WHEN** the system is built
- **THEN** there SHALL be no `AlertOptions` class; each alert feature SHALL own its thresholds

#### Scenario: FeatureOptions removed
- **WHEN** the system is built
- **THEN** there SHALL be no `FeatureOptions` dictionary; enabled state SHALL be per-feature config

### Requirement: Config validation per feature
Each per-feature config type SHALL have a validator registered via `AddNjordOptions<T>` with `ValidateOnStart`. Alert threshold validators SHALL enforce ordering (e.g. moderate < severe).

#### Scenario: Invalid threshold rejected at startup
- **WHEN** `FrostConfig.Severe` is set higher than `FrostConfig.Moderate`
- **THEN** startup validation SHALL fail with a descriptive error
