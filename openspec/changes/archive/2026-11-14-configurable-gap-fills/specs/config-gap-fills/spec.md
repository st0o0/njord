## ADDED Requirements

### Requirement: CompoundHeatStress exposes UV and PM2.5 combination thresholds
CompoundHeatStressConfig SHALL include `UvThreshold` (default 8.0), `UvPm25Threshold` (default 15.0), `VeryHighApparentMargin` (default 5.0), and `VeryHighPm25Factor` (default 2.0). The rule SHALL read these from config instead of inline constants.

#### Scenario: UV-trapped-pollution pathway uses config thresholds
- **WHEN** CompoundHeatStressAlert evaluates a point with UV >= `UvThreshold`, boundary layer < `BoundaryLayerLow`, and PM2.5 >= `UvPm25Threshold`
- **THEN** the alert fires with Yellow severity using the configured values

#### Scenario: VeryHigh apparent+PM2.5 escalation uses config margin and factor
- **WHEN** apparent temperature >= `ApparentTempThreshold` + `VeryHighApparentMargin` and PM2.5 >= `Pm25Threshold` * `VeryHighPm25Factor`
- **THEN** the alert escalates to Orange severity

### Requirement: SmogRisk exposes severity-tier thresholds
SmogRiskConfig SHALL include `RedBoundaryLayerM` (default 200), `RedPm25Ugm3` (default 50.0), `OrangeBoundaryLayerM` (default 300), and `OrangePm25Ugm3` (default 35.0). The severity classification SHALL read from config instead of hardcoded switch arms.

#### Scenario: Red severity uses config thresholds
- **WHEN** lowest boundary layer < `RedBoundaryLayerM` and highest PM2.5 >= `RedPm25Ugm3`
- **THEN** the alert fires with Red severity

#### Scenario: Orange severity uses config thresholds
- **WHEN** lowest boundary layer < `OrangeBoundaryLayerM` and highest PM2.5 >= `OrangePm25Ugm3`
- **THEN** the alert fires with Orange severity

### Requirement: CoastalFlood exposes severity-tier sea-level thresholds
CoastalFloodConfig SHALL include `RedSeaLevelM` (default 0.8), `RedMinFactors` (default 1), `OrangeSeaLevelM` (default 0.5), and `OrangeMinFactors` (default 0). The severity classification SHALL read from config.

#### Scenario: Red severity uses config sea-level and factor count
- **WHEN** max sea level >= `RedSeaLevelM` and contributing factors >= `RedMinFactors`
- **THEN** the alert fires with Red severity

#### Scenario: Orange severity uses config sea-level threshold
- **WHEN** max sea level >= `OrangeSeaLevelM`
- **THEN** the alert fires with Orange severity

### Requirement: PollenBlast exposes severity-tier pollen thresholds
PollenBlastConfig SHALL include `RedPollenThreshold` (default 100.0) and `OrangePollenThreshold` (default 50.0). The severity classification SHALL read from config. The validator SHALL ensure Red > Orange > 0.

#### Scenario: Red severity when pollen exceeds red threshold
- **WHEN** current peak pollen >= `RedPollenThreshold`
- **THEN** the alert fires with Red severity

#### Scenario: Orange severity when pollen exceeds orange threshold
- **WHEN** current peak pollen >= `OrangePollenThreshold` and < `RedPollenThreshold`
- **THEN** the alert fires with Orange severity

### Requirement: FireRisk exposes cold and humid gate cutoffs
FireRiskConfig SHALL include `ColdCutoff` (default 15.0) and `HumidCutoff` (default 70.0). The `WeatherScore` method SHALL use these from config instead of inline constants. The validator SHALL ensure `ColdCutoff` < `TemperatureThreshold` and `HumidCutoff` > `HumidityThreshold`.

#### Scenario: Cold gate uses config cutoff
- **WHEN** temperature < `ColdCutoff`
- **THEN** `WeatherScore` returns 0.0 regardless of other parameters

#### Scenario: Humid gate uses config cutoff
- **WHEN** humidity > `HumidCutoff`
- **THEN** `WeatherScore` returns 0.0 regardless of other parameters

### Requirement: MigraineRisk exposes Foehn direction and lookback hours
MigraineRiskConfig SHALL include `FoehnDirectionMin` (default 150.0), `FoehnDirectionMax` (default 210.0), `ShortLookbackHours` (default 6), `LongLookbackHours` (default 24), and `FoehnLookbackHours` (default 3). The rule SHALL read from config. The validator SHALL ensure `FoehnDirectionMin` < `FoehnDirectionMax` and both within 0..360, and all lookback hours >= 1.

#### Scenario: Foehn detection uses config direction range
- **WHEN** wind direction is within `FoehnDirectionMin`..`FoehnDirectionMax` and Foehn temperature/humidity/pressure conditions are met
- **THEN** the Foehn contribution is applied

#### Scenario: Custom lookback hours affect pressure drop detection
- **WHEN** `ShortLookbackHours` is set to 4
- **THEN** the 6h pressure drop check uses a 4-hour window instead

### Requirement: FrostDepth exposes Stefan coefficient, pipe-approach fraction, and risk thresholds
FrostDepthConfig SHALL include `StefanAlpha` (default 2.4), `PipeApproachFraction` (default 0.8), `SurfaceThresholdCm` (default 5.0), `ShallowThresholdCm` (default 15.0), and `ModerateThresholdCm` (default 30.0). The rule SHALL read from config. The validator SHALL ensure thresholds are ascending and `StefanAlpha` > 0 and `PipeApproachFraction` in (0, 1].

#### Scenario: Stefan formula uses config alpha
- **WHEN** `StefanAlpha` is set to 3.0
- **THEN** the frost depth calculation uses 3.0 instead of 2.4

#### Scenario: Risk classification uses config thresholds
- **WHEN** computed depth is 12.0 cm and `ShallowThresholdCm` is 15.0
- **THEN** the risk is classified as Surface (< ShallowThresholdCm but >= SurfaceThresholdCm)

### Requirement: SolarYield exposes battery comparison factors
SolarYieldConfig SHALL include `SunnierFactor` (default 1.2) and `CloudierFactor` (default 0.8). The battery strategy decision SHALL read from config. The validator SHALL ensure `SunnierFactor` > 1.0 and `CloudierFactor` in (0, 1).

#### Scenario: Battery strategy uses config sunnier factor
- **WHEN** `SunnierFactor` is 1.5 and tomorrow yields 1.4x today
- **THEN** the strategy is Normal (not FeedIn/Conserve, since 1.4 < 1.5)

### Requirement: IrrigationNeed exposes rain classification thresholds
IrrigationNeedConfig SHALL include `HeavyRainMm` (default 5.0) and `HighProbabilityPercent` (default 60.0). The advice logic SHALL read from config. The validator SHALL ensure both > 0.

#### Scenario: Skip advice uses config heavy rain threshold
- **WHEN** forecast rain > `HeavyRainMm` and probability > `HighProbabilityPercent`
- **THEN** advice is Skip

### Requirement: HeatPumpCop exposes COP formula coefficients and recommendation thresholds
HeatPumpCopConfig SHALL include `CopIntercept` (default 6.81), `CopSlope` (default 0.121), `IdealThreshold` (default 4.0), `NormalThreshold` (default 2.5), and `InefficientThreshold` (default 1.5). The COP calculation and recommendation SHALL read from config. The validator SHALL ensure `IdealThreshold` > `NormalThreshold` > `InefficientThreshold` > 0 and `CopSlope` > 0.

#### Scenario: COP formula uses config coefficients
- **WHEN** `CopIntercept` is 7.0 and `CopSlope` is 0.15 with delta T = 30
- **THEN** COP = max(1.0, 7.0 - 0.15 * 30) = 2.5

#### Scenario: Recommendation uses config thresholds
- **WHEN** COP is 3.0 and `IdealThreshold` is 4.0 and `NormalThreshold` is 2.5
- **THEN** recommendation is Normal
