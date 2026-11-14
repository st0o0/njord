## 1. Alert Config Extractions

- [x] 1.1 CompoundHeatStress: add `UvThreshold`, `UvPm25Threshold`, `VeryHighApparentMargin`, `VeryHighPm25Factor` to `src/Njord.Features/Alerts/CompoundHeatStress/CompoundHeatStressConfig.cs`; remove constants from `CompoundHeatStressAlert.cs`; extend `CompoundHeatStressConfigValidator.cs`; update tests in `src/Njord.Features.Tests/`
- [x] 1.2 SmogRisk: add `RedBoundaryLayerM`, `RedPm25Ugm3`, `OrangeBoundaryLayerM`, `OrangePm25Ugm3` to `src/Njord.Features/Alerts/SmogRisk/SmogRiskConfig.cs`; replace hardcoded switch in `SmogRiskAlert.cs`; extend `SmogRiskConfigValidator.cs`; update tests
- [x] 1.3 CoastalFlood: add `RedSeaLevelM`, `RedMinFactors`, `OrangeSeaLevelM`, `OrangeMinFactors` to `src/Njord.Features/Alerts/CoastalFlood/CoastalFloodConfig.cs`; replace hardcoded switch in `CoastalFloodAlert.cs`; extend `CoastalFloodConfigValidator.cs`; update tests
- [x] 1.4 PollenBlast: add `RedPollenThreshold`, `OrangePollenThreshold` to `src/Njord.Features/Alerts/PollenBlast/PollenBlastConfig.cs`; replace hardcoded switch in `PollenBlastAlert.cs`; add ordering validation to `PollenBlastConfigValidator.cs`; update tests
- [x] 1.5 FireRisk: add `ColdCutoff`, `HumidCutoff` to `src/Njord.Features/Alerts/FireRisk/FireRiskConfig.cs`; remove constants from `FireRiskAlert.cs`; extend `FireRiskConfigValidator.cs` (ColdCutoff < TemperatureThreshold, HumidCutoff > HumidityThreshold); update tests
- [x] 1.6 MigraineRisk: add `FoehnDirectionMin`, `FoehnDirectionMax`, `ShortLookbackHours`, `LongLookbackHours`, `FoehnLookbackHours` to `src/Njord.Features/Alerts/MigraineRisk/MigraineRiskConfig.cs`; remove constants from `MigraineRiskAlert.cs`; extend `MigraineRiskConfigValidator.cs` (direction range 0..360, lookback >= 1); update tests

## 2. Condition Config Extractions

- [x] 2.1 FrostDepth: add `StefanAlpha`, `PipeApproachFraction`, `SurfaceThresholdCm`, `ShallowThresholdCm`, `ModerateThresholdCm` to `src/Njord.Features/Conditions/FrostDepth/FrostDepthConfig.cs`; update `DepthCm` and `RiskFor` static methods in `FrostDepthCondition.cs` to accept parameters; extend `FrostDepthConfigValidator.cs` (alpha > 0, fraction in (0,1], thresholds ascending); update tests in `src/Njord.Features.Tests/`
- [x] 2.2 SolarYield: add `SunnierFactor`, `CloudierFactor` to `src/Njord.Features/Conditions/SolarYield/SolarYieldConfig.cs`; remove constants from `SolarYield.cs`; extend `SolarYieldConfigValidator.cs` (SunnierFactor > 1.0, CloudierFactor in (0,1)); update tests
- [x] 2.3 IrrigationNeed: add `HeavyRainMm`, `HighProbabilityPercent` to `src/Njord.Features/Conditions/IrrigationNeed/IrrigationNeedConfig.cs`; remove constants from `IrrigationNeedCondition.cs`; extend `IrrigationNeedConfigValidator.cs` (both > 0); update tests
- [x] 2.4 HeatPumpCop: add `CopIntercept`, `CopSlope`, `IdealThreshold`, `NormalThreshold`, `InefficientThreshold` to `src/Njord.Features/Conditions/HeatPumpCop/HeatPumpCopConfig.cs`; update `CopAt` and `Recommend` static methods in `HeatPumpCopCondition.cs` to accept parameters; extend `HeatPumpCopConfigValidator.cs` (Ideal > Normal > Inefficient > 0, Slope > 0); update tests

## 3. Documentation and Config Builder

- [x] 3.1 Update `docs/data/features.json`: add option entries for all new config properties across the 10 features, including name, type, default, description, and validation constraints
- [x] 3.2 Update Config Builder components in `docs/.vitepress/theme/builder/` to render new options; add grouping for features with many options (MigraineRisk, FrostDepth, HeatPumpCop)

## 4. Validation

- [x] 4.1 Run all test projects: `dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj`, `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj`, and remaining test projects to verify no regressions
- [x] 4.2 Run `dotnet format Njord.slnx --verify-no-changes` from `src/`
- [x] 4.3 Run `dotnet build Njord.slnx` from `src/` to verify clean compilation
