## ADDED Requirements

### Requirement: BbqConfig exposes scoring parameters
`BbqConfig` SHALL expose `GoodHourScore` (default 70), `RainThreshold` (default 0.1), four component weights `TemperatureWeight` (0.35), `WindWeight` (0.20), `RainWeight` (0.30), `CloudWeight` (0.15), and band/ramp parameters `TemperatureLow` (10), `TemperatureIdeal` (18), `TemperatureWarm` (28), `TemperatureHot` (36), `WindModerate` (5), `WindStrong` (10), `RainProbabilityLow` (0), `RainProbabilityHigh` (60), `CloudLow` (40), `CloudHigh` (100). `BbqScore` SHALL read all values from config instead of constants. Defaults SHALL reproduce current behavior.

#### Scenario: Default config unchanged behavior
- **WHEN** no Bbq options are configured
- **THEN** `BbqScore` SHALL produce identical results to the previous hardcoded constants

#### Scenario: Custom temperature band
- **WHEN** `BbqConfig.TemperatureLow` is 15 and `TemperatureIdeal` is 22
- **THEN** the temperature component scores 0.0 at 15 and 1.0 at 22

#### Scenario: Custom weights change score
- **WHEN** `BbqConfig.TemperatureWeight` is 0.5 and `CloudWeight` is 0.0 (other weights adjusted)
- **THEN** the score weights temperature at 50% and ignores cloud cover

### Requirement: OutdoorConfig exposes scoring parameters
`OutdoorConfig` SHALL expose `GoodHourScore` (default 85), `RainThreshold` (default 0.1), six component weights `TemperatureWeight` (0.3), `RainWeight` (0.2), `WindWeight` (0.1), `CloudWeight` (0.1), `UvWeight` (0.1), `AirQualityWeight` (0.2), and band/ramp parameters `TemperatureLow` (0), `TemperatureIdeal` (15), `TemperatureWarm` (26), `TemperatureHot` (36), `WindLow` (-1), `WindIdeal` (0), `WindModerate` (8), `WindStrong` (16), `CloudLow` (50), `CloudHigh` (100), `UvModerate` (5), `UvHigh` (11), `Pm25Low` (15), `Pm25High` (55), `PollenLow` (20), `PollenHigh` (100). `OutdoorScore` SHALL read all values from config. Defaults SHALL reproduce current behavior.

#### Scenario: Default config unchanged behavior
- **WHEN** no Outdoor options are configured
- **THEN** `OutdoorScore` SHALL produce identical results to the previous hardcoded constants

#### Scenario: Custom air quality weight
- **WHEN** `OutdoorConfig.AirQualityWeight` is 0.4 and other weights adjusted to sum 1.0
- **THEN** the air quality component has double the influence on the score

### Requirement: ExerciseAdvisoryConfig exposes scoring parameters
`ExerciseAdvisoryConfig` SHALL expose `SafeScore` (default 70), `CautionScore` (default 45), `ReasonThreshold` (default 0.5), five component weights `Pm25Weight` (0.4), `OzoneWeight` (0.2), `TemperatureWeight` (0.2), `HumidityWeight` (0.1), `UvWeight` (0.1), and ramp/band parameters `Pm25Low` (15), `Pm25High` (55), `OzoneLow` (100), `OzoneHigh` (180), `TemperatureCold` (-5), `TemperatureCool` (8), `TemperatureWarm` (22), `TemperatureHot` (35), `HumidityLow` (70), `HumidityHigh` (100), `UvModerate` (5), `UvHigh` (11). `ExerciseAdvisory` SHALL read all values from config. Defaults SHALL reproduce current behavior.

#### Scenario: Default config unchanged behavior
- **WHEN** no Exercise options are configured
- **THEN** `ExerciseAdvisory` SHALL produce identical results to the previous hardcoded constants

#### Scenario: Custom score cutoff
- **WHEN** `ExerciseAdvisoryConfig.SafeScore` is 80
- **THEN** hours scoring between 70 and 80 SHALL classify as Caution instead of Safe

### Requirement: SleepQualityConfig exposes penalty and threshold parameters
`SleepQualityConfig` SHALL additionally expose `HighPollen` (default 50), `Pm25Limit` (default 25), `GustLimit` (default 11.1), `CapeLimit` (default 1000), `PreSleepHours` (default 6), `HotPenalty` (default 30), `WarmPenalty` (default 15), `ColdPenalty` (default 10), `HumidityPenalty` (default 10), `Pm25Penalty` (default 15), `PollenPenalty` (default 5), `PollenSensitivePenalty` (default 20), `GustPenalty` (default 10), `ThunderstormPenalty` (default 15), `HotThreshold` (default 25), `WarmThreshold` (default 22), `ColdThreshold` (default 5), `HumidityThreshold` (default 80), `VentilationHotPenalty` (default 30), `VentilationHumidityPenalty` (default 20), `VentilationGustPenalty` (default 15), `VentilationRainPenalty` (default 30), `VentilationPm25Penalty` (default 40), `VentilationPollenPenalty` (default 15), `VentilationPollenSensitivePenalty` (default 40). `SleepQualityScore` SHALL read all values from config. Defaults SHALL reproduce current behavior.

#### Scenario: Default config unchanged behavior
- **WHEN** no SleepQuality penalty options are configured
- **THEN** `SleepQualityScore` SHALL produce identical results to the previous hardcoded constants

#### Scenario: Custom hot threshold
- **WHEN** `SleepQualityConfig.HotThreshold` is 28 and temperature is 26
- **THEN** the hot penalty SHALL NOT apply (previously it would at default 25)

#### Scenario: Custom pollen penalty
- **WHEN** `SleepQualityConfig.PollenSensitivePenalty` is 30 and `PollenSensitive` is true and pollen is high
- **THEN** the pollen penalty SHALL be 30 instead of the default 20

### Requirement: AllergySeverityConfig exposes reference and modifier values
`AllergySeverityConfig` SHALL additionally expose `PollenReference` (default 100), `GoodHourScore` (default 60), `WindPollenModifier` (default 15), `LowBoundaryLayerModifier` (default 15), `DryAirModifier` (default 10), `RainModifier` (default -30), `HighHumidityModifier` (default -15), `WindThreshold` (default 4), `DryHumidityThreshold` (default 60), `BoundaryLayerThreshold` (default 800), `DryAirHumidityThreshold` (default 40), `HighHumidityThreshold` (default 80). `AllergySeverityScore` SHALL read all values from config. Defaults SHALL reproduce current behavior.

#### Scenario: Default config unchanged behavior
- **WHEN** no AllergySeverity modifier options are configured
- **THEN** `AllergySeverityScore` SHALL produce identical results to the previous hardcoded constants

#### Scenario: Custom pollen reference
- **WHEN** `AllergySeverityConfig.PollenReference` is 50 and pollen is 50
- **THEN** the base severity SHALL be 100 instead of 50

### Requirement: AsthmaRiskConfig exposes reference and trigger values
`AsthmaRiskConfig` SHALL additionally expose `Pm25Reference` (default 75), `OzoneReference` (default 180), `PollenReference` (default 100), `GoodHourScore` (default 60), `ColdAirFactor` (default 1.3), `RapidChangeFactor` (default 1.2), `RapidChangeDegrees` (default 6), `RapidChangeHours` (default 3). `AsthmaRiskScore` SHALL read all values from config. Defaults SHALL reproduce current behavior.

#### Scenario: Default config unchanged behavior
- **WHEN** no AsthmaRisk reference options are configured
- **THEN** `AsthmaRiskScore` SHALL produce identical results to the previous hardcoded constants

#### Scenario: Custom rapid change sensitivity
- **WHEN** `AsthmaRiskConfig.RapidChangeDegrees` is 3
- **THEN** a 4-degree temperature change within the lookback period SHALL trigger the rapid change amplifier

### Requirement: LaundryConfig exposes scoring parameters
`LaundryConfig` SHALL additionally expose `GoodHourScore` (default 60), `RainThreshold` (default 0.1), five component weights `TemperatureWeight` (0.25), `HumidityWeight` (0.25), `WindWeight` (0.15), `RainWeight` (0.20), `SunWeight` (0.15), and ramp/band parameters `TemperatureLow` (5), `TemperatureHigh` (25), `HumidityLow` (40), `HumidityHigh` (90), `WindLow` (0), `WindIdeal` (2), `WindModerate` (8), `WindStrong` (15), `SunshineFull` (3600). `LaundryScore` SHALL read all values from config. Defaults SHALL reproduce current behavior.

#### Scenario: Default config unchanged behavior
- **WHEN** no Laundry scoring options are configured
- **THEN** `LaundryScore` SHALL produce identical results to the previous hardcoded constants

#### Scenario: Custom humidity range
- **WHEN** `LaundryConfig.HumidityLow` is 30 and `HumidityHigh` is 70
- **THEN** the humidity penalty starts at 30% and reaches maximum at 70%

### Requirement: SprayWindowConfig exposes additional parameters
`SprayWindowConfig` SHALL additionally expose `RainProbabilityLimit` (default 20), `TemperatureFalloff` (default 10), `HumidityMin` (default 40), `HumidityMax` (default 80), `HumidityFalloff` (default 20). `SprayWindowScore` SHALL read all values from config. Defaults SHALL reproduce current behavior.

#### Scenario: Default config unchanged behavior
- **WHEN** no SprayWindow additional options are configured
- **THEN** `SprayWindowScore` SHALL produce identical results to the previous hardcoded constants

#### Scenario: Custom humidity range
- **WHEN** `SprayWindowConfig.HumidityMin` is 50 and `HumidityMax` is 70
- **THEN** the optimal humidity band shifts to 50-70%

### Requirement: SurfConditionsConfig exposes surf assessment parameters
`SurfConditionsConfig` SHALL expose `WindowHours` (default 24), `MediumPeriodSeconds` (default 8), `LongPeriodSeconds` (default 12), `UsableHeightMinMeters` (default 0.5), `IdealHeightMinMeters` (default 1.0), `IdealHeightMaxMeters` (default 2.5), `UsableHeightMaxMeters` (default 3.5), `OffshoreToleranceDegrees` (default 45), `ChoppyShare` (default 0.5), `VeryChoppyShare` (default 0.75), `PoorScoreCap` (default 39). `SurfConditionsRule` SHALL accept the config and read all values from it. Defaults SHALL reproduce current behavior.

#### Scenario: Default config unchanged behavior
- **WHEN** no Surf assessment options are configured
- **THEN** `SurfConditions` SHALL produce identical results to the previous hardcoded constants

#### Scenario: Custom swell height range
- **WHEN** `SurfConditionsConfig.IdealHeightMinMeters` is 0.8 and `IdealHeightMaxMeters` is 2.0
- **THEN** swell heights between 0.8 and 2.0 SHALL score maximum height points

#### Scenario: Custom period threshold
- **WHEN** `SurfConditionsConfig.MediumPeriodSeconds` is 10
- **THEN** a 9-second period SHALL score as short instead of medium

### Requirement: Weight properties use named doubles summing to 1.0
For Bbq, Outdoor, Exercise and Laundry, the component weights SHALL be individual `double` properties (not arrays). Their names SHALL reflect the scored component (e.g. `TemperatureWeight`, `WindWeight`). The sum of all weight properties for a feature SHALL equal 1.0 within an epsilon of 0.001.

#### Scenario: Bbq weights default to 1.0
- **WHEN** default `BbqConfig` weights are summed
- **THEN** the total SHALL be 1.0

#### Scenario: Custom weights must sum to 1.0
- **WHEN** `BbqConfig.TemperatureWeight` is 0.5 and other weights still total 0.5
- **THEN** validation SHALL pass

#### Scenario: Weights not summing to 1.0 rejected
- **WHEN** `BbqConfig` weights sum to 0.8
- **THEN** startup validation SHALL fail with a descriptive error naming the section and the actual sum
