## MODIFIED Requirements

### Requirement: Every feature config with properties beyond Enabled has a startup validator

Each feature config class that exposes properties beyond `Enabled` (thresholds, window parameters, numeric bounds, component weights, reference values) SHALL have an `IValidateOptions<T>` validator registered via `AddFeature<TRule, TConfig, TValidator>()`. The validator SHALL reject invalid values at startup with descriptive error messages that include the full section path.

#### Scenario: Tier-4 multi-parameter configs validated
- **WHEN** any of CoastalFloodConfig, HeavyRainConfig, MigraineRiskConfig, PollenBlastConfig, AsthmaRiskConfig, SprayWindowConfig, SleepQualityConfig, SnowDepthWatchConfig, BbqConfig, OutdoorConfig, ExerciseAdvisoryConfig, LaundryConfig, AllergySeverityConfig, or SurfConditionsConfig has invalid property values
- **THEN** startup validation SHALL fail with a descriptive error naming the offending property and section

#### Scenario: Tier-2 scalar threshold configs validated
- **WHEN** any of IceConfig, FogConfig, SmogRiskConfig, EvapotranspirationConfig, IrrigationNeedConfig, SoilMoistureConfig, HeatPumpCopConfig, AllergySeverityConfig, LaundryConfig, OceanCurrentsConfig, FrostDepthConfig, or GrowingDegreeConfig has invalid property values
- **THEN** startup validation SHALL fail with a descriptive error naming the offending property and section

#### Scenario: Valid defaults pass validation
- **WHEN** no custom configuration is provided for any of the feature configs
- **THEN** all validators SHALL pass with the default property values

### Requirement: Weight sum constraint enforced

Validators for scoring features with component weights SHALL check that all weight properties sum to 1.0 within an epsilon of 0.001. The error message SHALL include the section name and the actual sum.

#### Scenario: Bbq weights sum validated
- **WHEN** `BbqConfig` weights sum to 0.9
- **THEN** startup validation SHALL fail with "Njord:Features:Bbq weights must sum to 1.0 (actual: 0.9)"

#### Scenario: Outdoor weights sum validated
- **WHEN** `OutdoorConfig` weights sum to 1.0
- **THEN** validation SHALL pass

#### Scenario: Exercise weights sum validated
- **WHEN** `ExerciseAdvisoryConfig` weights sum to 1.1
- **THEN** startup validation SHALL fail naming the section and actual sum

#### Scenario: Laundry weights sum validated
- **WHEN** `LaundryConfig` weights sum to 1.0
- **THEN** validation SHALL pass

### Requirement: Band parameter ordering enforced

Validators for scoring features with band parameters (four-value bands like temperature) SHALL enforce strict ordering: low < ideal/oneLow < oneHigh/warm < high. Ramp parameters (two-value) SHALL have zeroAt != oneAt.

#### Scenario: Valid temperature band
- **WHEN** `BbqConfig.TemperatureLow` is 10, `TemperatureIdeal` is 18, `TemperatureWarm` is 28, `TemperatureHot` is 36
- **THEN** validation SHALL pass

#### Scenario: Inverted temperature band rejected
- **WHEN** `BbqConfig.TemperatureIdeal` is 30 and `TemperatureWarm` is 20
- **THEN** startup validation SHALL fail naming the offending properties

### Requirement: Positive reference values enforced

Validators SHALL reject zero or negative values for reference parameters (PollenReference, Pm25Reference, OzoneReference) and score cutoffs (GoodHourScore, SafeScore, CautionScore).

#### Scenario: Zero pollen reference rejected
- **WHEN** `AllergySeverityConfig.PollenReference` is 0
- **THEN** startup validation SHALL fail

#### Scenario: Negative safe score rejected
- **WHEN** `ExerciseAdvisoryConfig.SafeScore` is -1
- **THEN** startup validation SHALL fail
