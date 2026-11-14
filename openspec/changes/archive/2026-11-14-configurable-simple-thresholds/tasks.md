## 1. Condition features: config extraction

- [x] 1.1 Add properties to `CommuteSafetyConfig` (`IceTemperatureCelsius`, `DewPointSpreadCelsius`, `FogHumidityPercent`, `HeavyRainMmH`, `StrongWindKmh`) in `src/Njord.Features/Conditions/CommuteSafety/CommuteSafetyConfig.cs`; update `CommuteSafetyRule` to read from config; add config tests in `src/Njord.Features.Tests/Conditions/CommuteSafety/`
- [x] 1.2 Add properties to `VentilationConfig` (`MaxPm25`, `MaxPollen`, `MinTemperatureCelsius`, `MaxTemperatureCelsius`, `MaxPrecipitationMmH`) in `src/Njord.Features/Conditions/Ventilation/VentilationConfig.cs`; update `VentilationWindowRule` to read from config; add config tests in `src/Njord.Features.Tests/Conditions/Ventilation/`
- [x] 1.3 Add properties to `NightVentilationConfig` (`HotDayPeakCelsius`, `CoolNightLimitCelsius`) in `src/Njord.Features/Conditions/NightVentilation/NightVentilationConfig.cs`; update `NightVentilationRule` to read from config; add config tests in `src/Njord.Features.Tests/Conditions/NightVentilation/`
- [x] 1.4 Add properties to `GardeningConfig` (`FrostTemperatureCelsius`, `RainThresholdMmH`, `WindLimitMs`) in `src/Njord.Features/Conditions/Gardening/GardeningConfig.cs`; update `GardeningRule` to read from config; add config tests in `src/Njord.Features.Tests/Conditions/Gardening/`
- [x] 1.5 Add properties to `ApproachingFrontConfig` (`PressureDropThresholdHpa`, `WindShiftThresholdDegrees`, `WindowHours`) in `src/Njord.Features/Conditions/ApproachingFront/ApproachingFrontConfig.cs`; create `ApproachingFrontConfigValidator`; update `ApproachingFrontRule` to read from config; switch registration to 3-param `AddFeature` in `src/Njord.Features/ServiceCollectionExtensions.cs`; add config and validator tests in `src/Njord.Features.Tests/Conditions/ApproachingFront/`

## 2. Alert feature: SwimmingConditions config extraction

- [x] 2.1 Add properties to `SwimmingConditionsConfig` (`WindowHours`, `ModerateWaveHeightM`, `DangerousWaveHeightM`, `ModerateWaterTempCelsius`, `DangerousWaterTempCelsius`) in `src/Njord.Features/Alerts/SwimmingConditions/SwimmingConditionsConfig.cs`; create `SwimmingConditionsConfigValidator` (WindowHours >= 1, wave ordering, temp ordering); update `SwimmingConditionsAlert` to read from config; switch registration to 3-param `AddFeature` in `src/Njord.Features/ServiceCollectionExtensions.cs`; add config and validator tests in `src/Njord.Features.Tests/Alerts/SwimmingConditions/`

## 3. Trend features: config extraction

- [x] 3.1 Add properties to `PollenTrendConfig` (`ChangeThreshold`, `MinimumChange`) in `src/Njord.Features/Trends/PollenTrend/PollenTrendConfig.cs`; update `PollenTrendRule` to read from config; add config tests in `src/Njord.Features.Tests/Trends/PollenTrend/`
- [x] 3.2 Add property to `ConfidenceTrendConfig` (`ChangeThreshold`) in `src/Njord.Features/Trends/ConfidenceTrend/ConfidenceTrendConfig.cs`; update `ConfidenceTrendRule` to read from config; add config tests in `src/Njord.Features.Tests/Trends/ConfidenceTrend/`

## 4. Documentation and Config Builder

- [x] 4.1 Update `docs/data/features.json`: add `options` arrays with name, type, default, and description for all 8 affected features
- [x] 4.2 Update Config Builder types (`docs/.vitepress/theme/builder/types.ts`) to support per-feature option rendering with numeric inputs
- [x] 4.3 Update Config Builder UI (`docs/.vitepress/theme/builder/ConfigBuilder.vue`) to render feature options, validate WindowHours >= 1 and ordered pairs, and exclude default-valued options from export
- [x] 4.4 Update Config Builder serializer (`docs/.vitepress/theme/builder/serializer.ts`) to include non-default feature options in generated config

## 5. Validation

- [x] 5.1 Run all test projects: `dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj`, `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj`, `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj` and remaining test projects
- [x] 5.2 Run `dotnet build Njord.slnx` and `dotnet format Njord.slnx --verify-no-changes`
