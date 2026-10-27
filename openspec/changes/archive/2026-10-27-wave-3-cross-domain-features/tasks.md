## 1. Cross-Domain Input Types (Njord.Core)

- [x] 1.1 Add `WeatherSoilEnsembleInput` (Weather + Ensemble, with SoilMoisture access) or reuse `WeatherEnsembleInput` if soil moisture is already on WeatherPoint -- check if SoilMoisture is sufficient via the existing WeatherPoint field. Add `WeatherAqInput` variant if needed for PollenBlast (Weather + AirQuality). Add `MarineWeatherEnsembleInput` (Marine + Weather + Ensemble) for CoastalFlood in `src/Njord.Core/Domain/FeatureInputs.cs`
- [x] 1.2 Tests: update input type tests in `src/Njord.Core.Tests/Domain/`

### Validation
```
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
```

## 2. FireRisk Alert

- [x] 2.1 Create `FireRiskConfig` in `src/Njord.Features/Alerts/FireRisk/FireRiskConfig.cs`: `Enabled`, `TemperatureThreshold` (30), `HumidityThreshold` (30), `WindSpeedThreshold` (20), `SoilMoistureThreshold` (0.10), `DryHours` (48). Section `Njord:Features:Lifestyle:FireRisk`
- [x] 2.2 Create `FireRiskAlert : FeatureRule<WeatherEnsembleInput>` in `src/Njord.Features/Alerts/FireRisk/FireRiskAlert.cs`: weighted scoring from 5 components (temperature 25%, humidity 25%, wind 20%, soil moisture 20%, dry spell 10%). Severity from total score: >0.7 Red, >0.4 Orange, >0.2 Yellow. Requirements: Weather (Temperature, Humidity, WindSpeed, SoilMoisture, Precipitation), Ensemble (TemperatureSpread). Default disabled
- [x] 2.3 Create `FireRiskAlertResult` in `src/Njord.Messages/Pipeline/` with score, component breakdown
- [x] 2.4 Register in `src/Njord.Features/ServiceCollectionExtensions.cs`
- [x] 2.5 Tests: `FireRiskAlertTests` in `src/Njord.Features.Tests/Alerts/FireRisk/FireRiskAlertTests.cs` -- all-conditions-met Red, partial Yellow, adequate-moisture no alert, confidence from ensemble

### Validation
```
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 3. PollenBlast Alert

- [x] 3.1 Create `PollenBlastConfig` in `src/Njord.Features/Alerts/PollenBlast/PollenBlastConfig.cs`: `Enabled`, `TemperatureThreshold` (15), `WindSpeedThreshold` (5), `PollenDelta` (10). Section `Njord:Features:AirQuality:PollenBlast`
- [x] 3.2 Create `PollenBlastAlert : FeatureRule<WeatherAqInput>` in `src/Njord.Features/Alerts/PollenBlast/PollenBlastAlert.cs`: PollDepth=8, detects rain-to-dry transition in history, checks pollen concentration increase. Cross-domain Weather + AirQuality. No Ensemble. Default disabled
- [x] 3.3 Create `PollenBlastAlertResult` in `src/Njord.Messages/Pipeline/`
- [x] 3.4 Register in `src/Njord.Features/ServiceCollectionExtensions.cs`
- [x] 3.5 Tests: `PollenBlastAlertTests` in `src/Njord.Features.Tests/Alerts/PollenBlast/PollenBlastAlertTests.cs` -- rain-to-dry triggers, continuous-dry no trigger, no-pollen-data no trigger, PollDepth behavior

### Validation
```
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 4. GrowingDegree Forecast

- [x] 4.1 Create `GrowingDegreeConfig` in `src/Njord.Features/Forecasts/GrowingDegree/GrowingDegreeConfig.cs`: `Enabled`, `BaseTemperature` (5.0). Section `Njord:Features:Lifestyle:GrowingDegree`
- [x] 4.2 Create `GrowingDegree : FeatureRule<WeatherInput>` in `src/Njord.Features/Forecasts/GrowingDegree/GrowingDegree.cs`: accumulates max(mean_temp - base, 0) per day. Weather only, no Ensemble. Default disabled
- [x] 4.3 Create `GrowingDegreeResult` in `src/Njord.Messages/Pipeline/` with daily GDD array and cumulative total
- [x] 4.4 Register in `src/Njord.Features/ServiceCollectionExtensions.cs`
- [x] 4.5 Tests: `GrowingDegreeTests` in `src/Njord.Features.Tests/Forecasts/GrowingDegree/GrowingDegreeTests.cs` -- warm day accumulates, cold day zero, multi-day accumulation, custom base

### Validation
```
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 5. CoastalFlood Alert

- [x] 5.1 Create `CoastalFloodConfig` in `src/Njord.Features/Alerts/CoastalFlood/CoastalFloodConfig.cs`: `Enabled`, `SeaLevelThreshold` (0.3), `WaveHeightThreshold` (1.5), `WindSpeedThreshold` (15). Section `Njord:Features:Marine:CoastalFlood`
- [x] 5.2 Create `CoastalFloodAlert : FeatureRule<MarineWeatherEnsembleInput>` in `src/Njord.Features/Alerts/CoastalFlood/CoastalFloodAlert.cs`: evaluates sea level deviation + wave height + onshore wind. Requires Marine (SeaLevelHeight, WaveHeight, WaveDirection) + Weather (WindSpeed, WindDirection) + Ensemble (TemperatureSpread). Default disabled
- [x] 5.3 Create `CoastalFloodAlertResult` in `src/Njord.Messages/Pipeline/`
- [x] 5.4 Register in `src/Njord.Features/ServiceCollectionExtensions.cs`
- [x] 5.5 Tests: `CoastalFloodAlertTests` in `src/Njord.Features.Tests/Alerts/CoastalFlood/CoastalFloodAlertTests.cs` -- high sea level + waves Red, sea level alone Yellow, normal sea level no alert, inland no alert

### Validation
```
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 6. ThunderstormEnhanced

- [x] 6.1 Add `ConvectiveInhibition` and `LiftedIndex` to optional weather params in `ThunderstormAlert` requirements in `src/Njord.Features/Alerts/Thunderstorm/ThunderstormAlert.cs`
- [x] 6.2 Update `Evaluate` to apply severity modifiers: CIN < -50 AND LI < -2 escalates one step; CIN > 0 de-escalates one step (minimum Yellow). No-op when values are null
- [x] 6.3 Add `CinThreshold` (-50) and `LiftedIndexThreshold` (-2) to `ThunderstormConfig` in `src/Njord.Features/Alerts/Thunderstorm/ThunderstormConfig.cs`
- [ ] 6.4 Tests: update `ThunderstormAlertTests` in `src/Njord.Features.Tests/Alerts/Thunderstorm/` -- escalation with unstable atmosphere, de-escalation with strong cap, missing CIN/LI no change

### Validation
```
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 7. Messages, Proto, gRPC

- [x] 7.1 Add new result records to `src/Njord.Messages/Pipeline/`: `FireRiskAlertResult`, `PollenBlastAlertResult`, `CoastalFloodAlertResult`, `GrowingDegreeResult`
- [x] 7.2 Update `protos/njord/v2/weather.proto`: add enrichment event oneof cases for fire_risk, pollen_blast, coastal_flood, growing_degree. Update thunderstorm result to include CIN/LI fields
- [x] 7.3 Update `src/Njord.Grpc/Mapping/` proto mapping for new feature results
- [ ] 7.4 Tests: update `src/Njord.Grpc.Tests/` for new mappings

### Validation
```
dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
```

## 8. Documentation and Config

- [x] 8.1 Update `docs/data/features.json` with 4 new features and updated thunderstorm entry
- [ ] 8.2 Update Config Builder (`docs/.vitepress/theme/builder/`) for new features
- [ ] 8.3 Update `AGENTS.md` feature count and category lists
- [ ] 8.4 Update ha-njord enrichment event handling for new feature results

### Validation
```
dotnet build Njord.slnx
dotnet format Njord.slnx --verify-no-changes
```

Full test suite:
```bash
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
```
