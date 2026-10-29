# feature-config-validation-coverage Specification (Delta)

## ADDED Requirements

### Requirement: Every feature config with properties beyond Enabled has a startup validator

Each feature config class that exposes properties beyond `Enabled` (thresholds, window parameters, numeric bounds) SHALL have an `IValidateOptions<T>` validator registered via `AddFeature<TRule, TConfig, TValidator>()`. The validator SHALL reject invalid values at startup with descriptive error messages that include the full section path.

#### Scenario: Tier-4 multi-parameter configs validated
- **WHEN** any of CoastalFloodConfig, HeavyRainConfig, MigraineRiskConfig, PollenBlastConfig, AsthmaRiskConfig, SprayWindowConfig, SleepQualityConfig, or SnowDepthWatchConfig has invalid property values
- **THEN** startup validation SHALL fail with a descriptive error naming the offending property and section

#### Scenario: Tier-2 scalar threshold configs validated
- **WHEN** any of IceConfig, FogConfig, SmogRiskConfig, EvapotranspirationConfig, IrrigationNeedConfig, SoilMoistureConfig, HeatPumpCopConfig, AllergySeverityConfig, LaundryConfig, OceanCurrentsConfig, FrostDepthConfig, or GrowingDegreeConfig has invalid property values
- **THEN** startup validation SHALL fail with a descriptive error naming the offending property and section

#### Scenario: Valid defaults pass validation
- **WHEN** no custom configuration is provided for any of the 20 feature configs
- **THEN** all validators SHALL pass with the default property values

### Requirement: Ordering constraints enforced for threshold pairs

When a config has two thresholds that must be ordered (e.g. DryThreshold < WetThreshold, LowMm < HighMm, MinTemp < MaxTemp), the validator SHALL reject configurations where the lower bound is greater than or equal to the upper bound.

#### Scenario: Inverted thresholds rejected
- **WHEN** SoilMoistureConfig has DryThreshold >= WetThreshold
- **THEN** startup validation SHALL fail with a message naming both properties

#### Scenario: Equal thresholds rejected
- **WHEN** EvapotranspirationConfig has LowMm == HighMm
- **THEN** startup validation SHALL fail

### Requirement: Ascending threshold arrays enforced

When a config has an array of thresholds that must be strictly ascending (e.g. SnowDepthWatchConfig's four depth levels), the validator SHALL reject configurations where the values are not in strictly ascending order.

#### Scenario: Non-ascending snow depth thresholds rejected
- **WHEN** SnowDepthWatchConfig has LightThreshold >= ModerateThreshold
- **THEN** startup validation SHALL fail naming the offending thresholds

### Requirement: Window parameters validated

Configs with WindowScanner parameters (MinWindowScore, MinWindowHours, MaxWindows) SHALL have validators that enforce all three are >= 1.

#### Scenario: Zero MinWindowHours rejected
- **WHEN** LaundryConfig has MinWindowHours = 0
- **THEN** startup validation SHALL fail naming MinWindowHours

### Requirement: Positive-value constraints enforced

Numeric properties representing physical quantities (thresholds in meters, temperatures, concentrations, speeds) SHALL be validated as greater than 0 where physically meaningful.

#### Scenario: Zero threshold rejected
- **WHEN** OceanCurrentsConfig has StrongCurrentThresholdKmh = 0
- **THEN** startup validation SHALL fail

#### Scenario: Negative threshold rejected
- **WHEN** FrostDepthConfig has PipeDepthCm = -10
- **THEN** startup validation SHALL fail

### Requirement: Hour-of-day range validated

Configs with hour-of-day properties (0-23) SHALL have validators that reject values outside this range.

#### Scenario: Invalid night hour rejected
- **WHEN** SleepQualityConfig has NightStartHour = 25
- **THEN** startup validation SHALL fail
