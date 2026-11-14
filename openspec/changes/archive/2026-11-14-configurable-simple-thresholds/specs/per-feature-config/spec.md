## MODIFIED Requirements

### Requirement: Config validation per feature
Each per-feature config type with range-constrained values SHALL have a validator registered via `AddNjordOptions<T, TValidator>` (or `AddFeature<TRule, TConfig, TValidator>`) with `ValidateOnStart`. Threshold validators SHALL enforce ordering and count. Features with a `WindowHours` property SHALL validate `WindowHours >= 1`. Features with ordered threshold pairs (e.g., moderate vs. dangerous) SHALL validate the ordering relationship.

#### Scenario: Invalid threshold rejected at startup
- **WHEN** `FrostConfig.Thresholds` has fewer than 3 elements after defaults are applied
- **THEN** startup validation SHALL fail with a descriptive error

#### Scenario: Feature config with array defaults validated after ApplyDefaults
- **WHEN** a config class implements `IHasArrayDefaults` and has a validator
- **THEN** validation SHALL run after `ApplyDefaults()` has filled empty arrays

#### Scenario: All multi-parameter configs have validators
- **WHEN** the set of features registered with `AddFeature<TRule, TConfig>()` (2-param) is collected
- **THEN** none SHALL have config properties beyond `Enabled` without a corresponding validator

#### Scenario: WindowHours validated on all features that expose it
- **WHEN** a feature config has a `WindowHours` property
- **THEN** its validator SHALL enforce `WindowHours >= 1`

#### Scenario: Ordered threshold pairs validated
- **WHEN** a feature config has a pair of thresholds with an ordering relationship (e.g., dangerous wave height > moderate wave height)
- **THEN** its validator SHALL enforce the ordering
