## 1. Move domain options validators to Core

- [x] 1.1 Move `ForecastOptionsValidator` from `src/Njord.OpenMeteo/Configuration/` to `src/Njord.Core/Configuration/`, update namespace, remove from `OpenMeteoSetupContainer`, register in `CoreSetupContainer` via `AddNjordOptions<ForecastOptions, ForecastOptionsValidator>()`
- [x] 1.2 Move `EnsembleOptionsValidator` from `src/Njord.OpenMeteo/Configuration/` to `src/Njord.Core/Configuration/`, update namespace, remove from `OpenMeteoSetupContainer`, register in `CoreSetupContainer`
- [x] 1.3 Move `FloodOptionsValidator` from `src/Njord.OpenMeteo/Configuration/` to `src/Njord.Core/Configuration/`, update namespace, remove from `OpenMeteoSetupContainer`, register in `CoreSetupContainer`
- [x] 1.4 Move existing validator tests from `src/Njord.OpenMeteo.Tests/` to `src/Njord.Core.Tests/Configuration/`, update namespaces, verify they pass with `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj`

## 2. Tier-4 alert validators

- [x] 2.1 Create `CoastalFloodConfigValidator` in `src/Njord.Features/Alerts/CoastalFlood/`: SeaLevelThresholdM > 0, WaveHeightThresholdM > 0, WindSpeedThreshold > 0. Change registration to 3-param `AddFeature`. Add tests in `src/Njord.Features.Tests/Alerts/CoastalFlood/CoastalFloodConfigValidatorTests.cs`: valid defaults pass, each threshold <= 0 fails
- [x] 2.2 Create `HeavyRainConfigValidator` in `src/Njord.Features/Alerts/HeavyRain/`: WindowHours >= 1, HourlyThreshold > 0, DailyThreshold > HourlyThreshold. Change registration to 3-param. Add tests: valid defaults pass, WindowHours < 1 fails, DailyThreshold <= HourlyThreshold fails
- [x] 2.3 Create `MigraineRiskConfigValidator` in `src/Njord.Features/Alerts/MigraineRisk/`: PressureThreshold6h > 0, PressureThreshold24h > 0, TempSwingThreshold > 0, HumiditySwingThreshold > 0. Change registration. Add tests: valid defaults pass, each threshold <= 0 fails
- [x] 2.4 Create `PollenBlastConfigValidator` in `src/Njord.Features/Alerts/PollenBlast/`: TemperatureThreshold > 0, WindSpeedThreshold > 0, PollenDelta > 0, PrecipitationThreshold > 0. Change registration. Add tests: valid defaults pass, each threshold <= 0 fails

## 3. Tier-4 score validators

- [x] 3.1 Create `AsthmaRiskConfigValidator` in `src/Njord.Features/Scores/AsthmaRisk/`: all weights >= 0, Pm25Weight + OzoneWeight + PollenWeight + HumidityWeight + BoundaryLayerWeight == 100, MinWindowScore >= 1, MinWindowHours >= 1, MaxWindows >= 1. Change registration. Add tests: valid defaults pass, weights sum != 100 fails, negative weight fails, each window param < 1 fails
- [x] 3.2 Create `SprayWindowConfigValidator` in `src/Njord.Features/Scores/SprayWindow/`: MaxWindSpeed > 0, RainFreeHours >= 1, MinTemp < MaxTemp, MinWindowScore >= 1, MinWindowHours >= 1, MaxWindows >= 1. Change registration. Add tests: valid defaults pass, MinTemp >= MaxTemp fails, each window param < 1 fails
- [x] 3.3 Create `SleepQualityConfigValidator` in `src/Njord.Features/Scores/SleepQuality/`: NightStartHour 0..23, NightEndHour 0..23, MinWindowScore >= 1, MinWindowHours >= 1, MaxWindows >= 1. Change registration. Add tests: valid defaults pass, NightStartHour = 25 fails, each window param < 1 fails

## 4. Tier-4 condition validator

- [x] 4.1 Create `SnowDepthWatchConfigValidator` in `src/Njord.Features/Conditions/SnowDepthWatch/`: all thresholds > 0, LightThreshold < ModerateThreshold < HeavyThreshold < ExtremeThreshold. Change registration. Add tests: valid defaults pass, non-ascending fails, zero threshold fails

## 5. Tier-2 alert validators

- [x] 5.1 Create `IceConfigValidator` in `src/Njord.Features/Alerts/Ice/`: WindowHours >= 1. Change registration. Add tests: valid defaults pass, WindowHours < 1 fails
- [x] 5.2 Create `FogConfigValidator` in `src/Njord.Features/Alerts/Fog/`: WindowHours >= 1, PersistentHours >= 1, PersistentHours <= WindowHours. Change registration. Add tests: valid defaults pass, PersistentHours > WindowHours fails
- [x] 5.3 Create `SmogRiskConfigValidator` in `src/Njord.Features/Alerts/SmogRisk/`: BoundaryLayerThresholdM > 0, Pm25ThresholdUgm3 > 0. Change registration. Add tests: valid defaults pass, each <= 0 fails

## 6. Tier-2 condition validators

- [x] 6.1 Create `EvapotranspirationConfigValidator` in `src/Njord.Features/Conditions/Evapotranspiration/`: LowMm > 0, HighMm > 0, LowMm < HighMm. Change registration. Add tests: valid defaults pass, LowMm >= HighMm fails
- [x] 6.2 Create `IrrigationNeedConfigValidator` in `src/Njord.Features/Conditions/IrrigationNeed/`: both 0..100, DryThresholdPercent < WetThresholdPercent. Change registration. Add tests: valid defaults pass, inverted thresholds fail, out of range fails
- [x] 6.3 Create `SoilMoistureConfigValidator` in `src/Njord.Features/Conditions/SoilMoisture/`: DryThreshold > 0, WetThreshold > 0, DryThreshold < WetThreshold. Change registration. Add tests: valid defaults pass, inverted fails
- [x] 6.4 Create `HeatPumpCopConfigValidator` in `src/Njord.Features/Conditions/HeatPumpCop/`: ForecastHours >= 1. Change registration. Add tests: valid defaults pass, ForecastHours < 1 fails
- [x] 6.5 Create `OceanCurrentsConfigValidator` in `src/Njord.Features/Conditions/OceanCurrents/`: StrongCurrentThresholdKmh > 0. Change registration. Add tests: valid defaults pass, <= 0 fails
- [x] 6.6 Create `FrostDepthConfigValidator` in `src/Njord.Features/Conditions/FrostDepth/`: PipeDepthCm > 0. Change registration. Add tests: valid defaults pass, <= 0 fails
- [x] 6.7 Create `SnowDepthWatchConfigValidator` — already covered in 4.1

## 7. Tier-2 score validators

- [x] 7.1 Create `AllergySeverityConfigValidator` in `src/Njord.Features/Scores/AllergySeverity/`: MinWindowScore >= 1, MinWindowHours >= 1, MaxWindows >= 1. Change registration. Add tests: valid defaults pass, each < 1 fails
- [x] 7.2 Create `LaundryConfigValidator` in `src/Njord.Features/Scores/Laundry/`: MinWindowScore >= 1, MinWindowHours >= 1, MaxWindows >= 1. Change registration. Add tests: valid defaults pass, each < 1 fails

## 8. Tier-2 forecast validator

- [x] 8.1 Create `GrowingDegreeConfigValidator` in `src/Njord.Features/Forecasts/GrowingDegree/`: BaseTemperature >= -10 and <= 30. Change registration. Add tests: valid defaults pass, out of range fails

## 9. Validation

- [x] 9.1 Run `dotnet build Njord.slnx` from `src/`
- [x] 9.2 Run `dotnet format Njord.slnx --verify-no-changes` from `src/`
- [x] 9.3 Run all test projects sequentially from `src/`
