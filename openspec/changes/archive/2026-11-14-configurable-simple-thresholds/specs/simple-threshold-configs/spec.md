## ADDED Requirements

### Requirement: CommuteSafety thresholds are configurable
The CommuteSafety feature SHALL read its hazard detection thresholds from `CommuteSafetyConfig` instead of hardcoded constants. Properties: `IceTemperatureCelsius` (default 1.0), `DewPointSpreadCelsius` (default 2.0), `FogHumidityPercent` (default 95.0), `HeavyRainMmH` (default 5.0), `StrongWindKmh` (default 16.67). All defaults SHALL preserve current behavior.

#### Scenario: Default config produces identical output
- **WHEN** CommuteSafety evaluates with default config values
- **THEN** the results SHALL be identical to the previous hardcoded behavior

#### Scenario: Custom ice temperature threshold
- **WHEN** `IceTemperatureCelsius` is set to 3.0
- **THEN** the feature SHALL flag icy conditions when temperature is at or below 3.0

### Requirement: VentilationWindow thresholds are configurable
The VentilationWindow feature SHALL read its air quality and comfort thresholds from `VentilationConfig` instead of hardcoded constants. Properties: `MaxPm25` (default 15.0), `MaxPollen` (default 20.0), `MinTemperatureCelsius` (default 15.0), `MaxTemperatureCelsius` (default 28.0), `MaxPrecipitationMmH` (default 0.1). All defaults SHALL preserve current behavior.

#### Scenario: Default config produces identical output
- **WHEN** VentilationWindow evaluates with default config values
- **THEN** the results SHALL be identical to the previous hardcoded behavior

#### Scenario: Custom temperature range
- **WHEN** `MinTemperatureCelsius` is set to 18.0 and `MaxTemperatureCelsius` is set to 25.0
- **THEN** the feature SHALL only recommend ventilation when outdoor temperature is between 18.0 and 25.0

### Requirement: NightVentilation thresholds are configurable
The NightVentilation feature SHALL read its temperature thresholds from `NightVentilationConfig` instead of hardcoded constants. Properties: `HotDayPeakCelsius` (default 28.0), `CoolNightLimitCelsius` (default 20.0). All defaults SHALL preserve current behavior.

#### Scenario: Default config produces identical output
- **WHEN** NightVentilation evaluates with default config values
- **THEN** the results SHALL be identical to the previous hardcoded behavior

#### Scenario: Custom night cooling limit
- **WHEN** `CoolNightLimitCelsius` is set to 18.0
- **THEN** the feature SHALL require night temperatures at or below 18.0 for a ventilation recommendation

### Requirement: Gardening thresholds are configurable
The Gardening feature SHALL read its safety thresholds from `GardeningConfig` instead of hardcoded constants. Properties: `FrostTemperatureCelsius` (default 2.0), `RainThresholdMmH` (default 2.0), `WindLimitMs` (default 10.0). All defaults SHALL preserve current behavior.

#### Scenario: Default config produces identical output
- **WHEN** Gardening evaluates with default config values
- **THEN** the results SHALL be identical to the previous hardcoded behavior

#### Scenario: Custom frost temperature
- **WHEN** `FrostTemperatureCelsius` is set to 0.0
- **THEN** the feature SHALL only flag frost risk when temperature reaches 0.0 or below

### Requirement: ApproachingFront thresholds are configurable with validation
The ApproachingFront feature SHALL read its detection thresholds from `ApproachingFrontConfig` instead of hardcoded constants. Properties: `PressureDropThresholdHpa` (default 3.0), `WindShiftThresholdDegrees` (default 45.0), `WindowHours` (default 6). A validator SHALL enforce `WindowHours >= 1`.

#### Scenario: Default config produces identical output
- **WHEN** ApproachingFront evaluates with default config values
- **THEN** the results SHALL be identical to the previous hardcoded behavior

#### Scenario: WindowHours validation rejects zero
- **WHEN** `WindowHours` is set to 0
- **THEN** startup validation SHALL fail with a descriptive error

#### Scenario: Custom pressure sensitivity
- **WHEN** `PressureDropThresholdHpa` is set to 2.0
- **THEN** the feature SHALL detect fronts with smaller pressure drops

### Requirement: SwimmingConditions thresholds are configurable with validation
The SwimmingConditions feature SHALL read its safety thresholds from `SwimmingConditionsConfig` instead of hardcoded constants. Properties: `WindowHours` (default 24), `ModerateWaveHeightM` (default 1.0), `DangerousWaveHeightM` (default 2.0), `ModerateWaterTempCelsius` (default 16.0), `DangerousWaterTempCelsius` (default 10.0). A validator SHALL enforce `WindowHours >= 1`, `DangerousWaveHeightM > ModerateWaveHeightM`, and `DangerousWaterTempCelsius < ModerateWaterTempCelsius`.

#### Scenario: Default config produces identical output
- **WHEN** SwimmingConditions evaluates with default config values
- **THEN** the results SHALL be identical to the previous hardcoded behavior

#### Scenario: WindowHours validation rejects zero
- **WHEN** `WindowHours` is set to 0
- **THEN** startup validation SHALL fail with a descriptive error

#### Scenario: Wave height ordering validation
- **WHEN** `DangerousWaveHeightM` is set to 0.5 and `ModerateWaveHeightM` is set to 1.0
- **THEN** startup validation SHALL fail because dangerous must exceed moderate

#### Scenario: Water temp ordering validation
- **WHEN** `DangerousWaterTempCelsius` is set to 20.0 and `ModerateWaterTempCelsius` is set to 16.0
- **THEN** startup validation SHALL fail because dangerous must be below moderate

### Requirement: PollenTrend thresholds are configurable
The PollenTrend feature SHALL read its change detection thresholds from `PollenTrendConfig` instead of hardcoded constants. Properties: `ChangeThreshold` (default 0.10), `MinimumChange` (default 1.0). All defaults SHALL preserve current behavior.

#### Scenario: Default config produces identical output
- **WHEN** PollenTrend evaluates with default config values
- **THEN** the results SHALL be identical to the previous hardcoded behavior

#### Scenario: Custom sensitivity
- **WHEN** `ChangeThreshold` is set to 0.05
- **THEN** the feature SHALL detect smaller pollen changes as significant

### Requirement: ConfidenceTrend threshold is configurable
The ConfidenceTrend feature SHALL read its change detection threshold from `ConfidenceTrendConfig` instead of a hardcoded constant. Properties: `ChangeThreshold` (default 0.20). The default SHALL preserve current behavior.

#### Scenario: Default config produces identical output
- **WHEN** ConfidenceTrend evaluates with default config values
- **THEN** the results SHALL be identical to the previous hardcoded behavior

#### Scenario: Custom sensitivity
- **WHEN** `ChangeThreshold` is set to 0.10
- **THEN** the feature SHALL detect smaller confidence shifts as significant
