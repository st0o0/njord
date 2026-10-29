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

### Requirement: Severity thresholds configurable for Uv, Pm25 and Pollen
`UvConfig`, `Pm25Config` and `PollenConfig` SHALL expose a `Thresholds` array, implement `IHasArrayDefaults` (array initialized to `[]`, defaults in a static property, filled by `ApplyDefaults()`) and have a validator registered via `AddFeature<TRule, TConfig, TValidator>()`. Defaults SHALL reproduce the previous hardcoded behavior: Uv `[3, 6, 8, 11]` (four levels), Pm25 `[15, 25, 50]`, Pollen `[20, 50, 100]`. Validators SHALL require the exact element count and strictly ascending values.

#### Scenario: Defaults unchanged
- **WHEN** no thresholds are configured
- **THEN** `UvConfig.Thresholds` SHALL be `[3, 6, 8, 11]`, `Pm25Config.Thresholds` `[15, 25, 50]` and `PollenConfig.Thresholds` `[20, 50, 100]`, and alert levels SHALL match the previous behavior

#### Scenario: Custom threshold changes severity
- **WHEN** `Pm25Config.Thresholds` is `[10, 20, 40]` and PM2.5 is 22
- **THEN** `Pm25Alert` SHALL emit the second severity level

#### Scenario: Invalid thresholds rejected
- **WHEN** a Uv, Pm25 or Pollen `Thresholds` array has the wrong element count or is not strictly ascending
- **THEN** startup validation SHALL fail with a descriptive error

### Requirement: Flood alerts expose their constants
`FloodRainConfig` SHALL expose `HeavyRainThreshold` (default 30.0) and `ForecastDays` (default 7). `FloodRiskConfig` SHALL expose `MedianFactor` (default 2.0) and `ForecastDays` (default 7). `FloodRainAlert` and `FloodRiskAlert` SHALL read these from their config instead of constants.

#### Scenario: FloodRain defaults
- **WHEN** no FloodRain options are configured
- **THEN** `HeavyRainThreshold` SHALL be 30.0 and `ForecastDays` 7 and behavior SHALL match the previous constants

#### Scenario: FloodRisk custom factor
- **WHEN** `FloodRiskConfig.MedianFactor` is 3.0 and discharge is 2.5 times the median
- **THEN** `FloodRiskAlert` SHALL NOT emit an alert

#### Scenario: Invalid scalar rejected
- **WHEN** `HeavyRainThreshold` is not greater than 0, `MedianFactor` is not greater than 1, or `ForecastDays` is outside 1..210
- **THEN** startup validation SHALL fail with a descriptive error

### Requirement: Alerts expose WindowHours
Every alert that evaluates `NextHoursInclusive` over the default window SHALL expose `WindowHours` (default 24) in its config and SHALL use `NextHoursInclusive(ctx.Now, config.Value.WindowHours)`. This covers Frost, Heat, Humidity, Storm, Snow, WindChill, Ice, Visibility, TropicalNight, ClearSkyUv, Dust, WetBulbDanger, Uv, Pm25, Pollen, PressureDrop, Fog and HeavyRain. Validation SHALL reject values below 1.

#### Scenario: Default window unchanged
- **WHEN** `WindowHours` is not configured
- **THEN** it SHALL be 24 and alert results SHALL equal the previous behavior

#### Scenario: Shorter window
- **WHEN** `FrostConfig.WindowHours` is 6 and the only frost hour is 10 hours ahead
- **THEN** `FrostAlert` SHALL emit nothing

#### Scenario: Invalid window
- **WHEN** `WindowHours` is 0
- **THEN** startup validation SHALL fail naming `WindowHours`

### Requirement: Thunderstorm validates CAPE thresholds
`ThunderstormConfig` SHALL have a `ThunderstormConfigValidator` registered in `AddFeatures` that enforces the `CapeThresholds` element count and strictly ascending order.

#### Scenario: Valid defaults
- **WHEN** default `CapeThresholds` are applied
- **THEN** validation SHALL succeed

#### Scenario: Unordered thresholds
- **WHEN** `CapeThresholds` is not strictly ascending
- **THEN** startup validation SHALL fail with a descriptive error

### Requirement: SolarYieldConfig holds system and battery settings
`SolarYieldConfig` (`Njord:Features:SolarYield`) SHALL contain `Enabled`, `PeakKw` (default 5.0), `SystemEfficiency` and `BatteryCapacityKwh` (default 0, meaning no battery). `SolarYieldConfigValidator` SHALL reject non-positive `PeakKw`, `SystemEfficiency` outside (0, 1] and negative `BatteryCapacityKwh`. When `BatteryCapacityKwh` is greater than 0, `SolarYield` SHALL also emit the battery strategy result, computed from the same GTI-based irradiance as the yield.

#### Scenario: No battery by default
- **WHEN** `BatteryCapacityKwh` is 0
- **THEN** `SolarYield` SHALL emit only the yield result

#### Scenario: Battery strategy emitted
- **WHEN** `BatteryCapacityKwh` is 10 and forecast yield data is available
- **THEN** `SolarYield` SHALL emit the yield result and the battery strategy result using the existing result types

#### Scenario: Consistent radiation source
- **WHEN** the battery strategy is computed
- **THEN** it SHALL use the blended GTI series, not ShortwaveRadiation

#### Scenario: Invalid values rejected
- **WHEN** `PeakKw` is 0, `SystemEfficiency` is 1.5, or `BatteryCapacityKwh` is -1
- **THEN** startup validation SHALL fail with a descriptive error

### Requirement: SolarBatteryStrategy is not a separate feature
There SHALL be no `SolarBatteryStrategy` feature, config class or `Njord:Features:SolarBatteryStrategy` section. Its behavior is part of `SolarYield`. There SHALL be no `Njord:SolarYield` options section.

#### Scenario: Feature catalog
- **WHEN** all registered feature descriptors are collected
- **THEN** none SHALL be named `SolarBatteryStrategy`

#### Scenario: Old sections ignored
- **WHEN** `Njord:Features:SolarBatteryStrategy:Enabled` or `Njord:SolarYield:PeakKw` is configured
- **THEN** it SHALL have no effect
