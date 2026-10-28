# Tasks: health-wellness-features

## 1. MigraineRisk feature rule and config

Files:
- `src/Njord.Features/Alerts/Migraine/MigraineAlert.cs`
- `src/Njord.Features/Alerts/Migraine/MigraineConfig.cs`

Implementation:
- [x] Create `MigraineConfig : IConfigSection` at `Njord:Features:Health:Migraine` with `Enabled` (false), `PressureThreshold6h` (5.0), `PressureThreshold24h` (10.0), `TempSwingThreshold` (8.0), `HumiditySwingThreshold` (30.0)
- [x] Create `MigraineAlert : FeatureRule<ForecastInput>` with:
  - Descriptor: `OutputKind.Alert`, `FeatureCategory.Health`, Name `"Migraine"`, DefaultEnabled false, PollDepth 6
  - Requirements: `Forecast: ForecastParams.Pressure | ForecastParams.Temperature | ForecastParams.Humidity | ForecastParams.WindDirection | ForecastParams.DewPoint`
  - Project: `forecast.Forecast is { } w ? new(w) : null`
  - Evaluate: compute pressure drop from ring buffer history (compare Current vs first History entry for 6h), forecast-ahead for 24h, secondary triggers from forecast window, Föhn detection, map to severity

## 2. MigraineRisk result, proto, and gRPC mapping

Files:
- `src/Njord.Messages/Pipeline/AlertResults.cs` (add `MigraineRiskAlertResult`)
- `src/Njord.Messages/Alerts/MigraineTrigger.cs` (new enum)
- `protos/njord/v2/weather.proto` (add `MigraineTrigger` enum, `MigraineRisk` message, oneof case)
- `src/Njord.Grpc/Mapping/AlertResultMappingExtensions.cs` (add `ToProto()`)
- `src/Njord.Grpc/Mapping/EnrichmentEventMappingExtensions.cs` (add case)
- `src/Njord.Grpc/Mapping/MigraineTriggerMappingExtensions.cs` (new: enum mapping)
- `src/Njord.Features/ServiceCollectionExtensions.cs` (register)

Implementation:
- [x] Add `MigraineTrigger` enum in `Njord.Messages/Alerts/`: `PressureDrop`, `TemperatureSwing`, `Foehn`, `HumiditySwing`
- [x] Add `MigraineRiskAlertResult` record extending `AlertFeatureResult` with `MigraineTrigger DominantTrigger`, `double PressureChangeHpa`, `double TemperatureChangeC`
- [x] Add proto `MigraineTrigger` enum and `MigraineRisk` message to `weather.proto`. Use next available alert field number (48) in `EnrichmentEvent.oneof feature`
- [x] Add `ToProto()` extension for `MigraineRiskAlertResult` and `MigraineTrigger`
- [x] Add `MigraineRiskAlertResult` case in `EnrichmentEventMappingExtensions`
- [x] Register `MigraineConfig` and `MigraineAlert` in `ServiceCollectionExtensions.AddFeatures()`

## 3. MigraineRisk tests

Files:
- `src/Njord.Features.Tests/Alerts/Migraine/MigraineAlertTests.cs`
- `src/Njord.Grpc.Tests/Mapping/MigraineRiskMappingTests.cs`

Implementation:
- [x] Test MigraineAlert evaluation with ring buffer history: build FeatureContext with 6 history entries showing pressure decline, verify alert fires with correct severity and trigger
- [x] Test no alert when pressure is stable
- [x] Test Föhn detection: south wind + rising temp + falling humidity + rising pressure
- [x] Test secondary trigger accumulation (temp swing + humidity swing → higher severity)
- [x] Test MigraineTrigger mapping (one Theory per direction)
- [x] Test MigraineRiskAlertResult → proto mapping

## 4. SleepQuality feature rule and config

Files:
- `src/Njord.Features/Scores/Sleep/SleepScore.cs`
- `src/Njord.Features/Scores/Sleep/SleepConfig.cs`

Implementation:
- [x] Create `SleepConfig : IConfigSection` at `Njord:Features:Lifestyle:Sleep` with `Enabled` (false), `PollenSensitive` (false), `NightStartHour` (22), `NightEndHour` (6)
- [x] Create `SleepScore : FeatureRule<ForecastAqInput>` with:
  - Descriptor: `OutputKind.Score`, `FeatureCategory.Lifestyle`, Name `"Sleep"`, DefaultEnabled false
  - Requirements: Forecast (Temperature, ApparentTemperature, Humidity, WindGusts, Cape, Precipitation, DewPoint) + AirQuality (Pm25), Optional AirQuality (pollen species)
  - Project: `forecast is { Forecast: { } w, AirQuality: { } a } ? new(w, a) : null`
  - Evaluate: find night window hours, apply penalty model, compute ventilation recommendation, find best pre-sleep ventilation window for BestStart/BestHours

## 5. SleepQuality result, proto, and gRPC mapping

Files:
- `src/Njord.Messages/Pipeline/NonAlertResults.cs` (add `SleepQualityResult`)
- `src/Njord.Messages/Alerts/SleepRecommendation.cs` (new enum)
- `src/Njord.Messages/Alerts/SleepFactor.cs` (new enum)
- `protos/njord/v2/weather.proto` (add enums + message + oneof case)
- `src/Njord.Grpc/Mapping/ScoreResultMappingExtensions.cs` or similar (add ToProto)
- `src/Njord.Grpc/Mapping/EnrichmentEventMappingExtensions.cs` (add case)
- `src/Njord.Features/ServiceCollectionExtensions.cs` (register)

Implementation:
- [x] Add `SleepRecommendation` enum: `WindowOpen`, `WindowClosed`, `AcRecommended`
- [x] Add `SleepFactor` enum: `Heat`, `Humidity`, `Pollen`, `Wind`, `Storm`, `AirQuality`, `Cold`
- [x] Add `SleepQualityResult` record extending `ScoreFeatureResult` with `SleepRecommendation Recommendation`, `SleepFactor DominantFactor`, `double MinNightTemperature`
- [x] Add proto enums and `SleepQuality` message. Use next available score field number (54) in oneof
- [x] Add ToProto extensions and EnrichmentEvent case
- [x] Register `SleepConfig` and `SleepScore` in AddFeatures

## 6. SleepQuality tests

Files:
- `src/Njord.Features.Tests/Scores/Sleep/SleepScoreTests.cs`
- `src/Njord.Grpc.Tests/Mapping/SleepQualityMappingTests.cs`

Implementation:
- [x] Test tropical night penalty (temp > 25°C → score ≤ 70)
- [x] Test cold night penalty
- [x] Test PM2.5 penalty
- [x] Test pollen sensitive vs non-sensitive
- [x] Test thunderstorm penalty (CAPE + precip)
- [x] Test ventilation recommendation logic (window open vs closed vs AC)
- [x] Test BestStart/BestHours identifies pre-sleep ventilation window
- [x] Test enum mapping and proto round-trip

## 7. AsthmaRisk feature rule, config, and validator

Files:
- `src/Njord.Features/Scores/Asthma/AsthmaScore.cs`
- `src/Njord.Features/Scores/Asthma/AsthmaConfig.cs`
- `src/Njord.Features/Scores/Asthma/AsthmaConfigValidator.cs`

Implementation:
- [x] Create `AsthmaConfig : IConfigSection` at `Njord:Features:Health:Asthma` with `Enabled` (false), five weight properties (summing to 1.0), `ColdAirAmplifier` (true), `TempChangeAmplifier` (true)
- [x] Create `AsthmaConfigValidator : IValidateOptions<AsthmaConfig>` checking weights sum to 1.0 ± 0.01
- [x] Create `AsthmaScore : FeatureRule<ForecastAqInput>` with:
  - Descriptor: `OutputKind.Score`, `FeatureCategory.Health`, Name `"Asthma"`, DefaultEnabled false
  - Requirements: Forecast (Temperature, Humidity, BoundaryLayerHeight) + AirQuality (Pm25, Ozone), Optional AirQuality (pollen species)
  - Evaluate: normalize each component 0-100, apply weights, apply amplifiers (cold air, temp change from ring buffer), determine dominant factor, compute score

## 8. AsthmaRisk result, proto, and gRPC mapping

Files:
- `src/Njord.Messages/Pipeline/NonAlertResults.cs` (add `AsthmaRiskResult`)
- `src/Njord.Messages/Alerts/AsthmaDominantFactor.cs` (new enum)
- `protos/njord/v2/weather.proto` (add enum + message + oneof case)
- `src/Njord.Grpc/Mapping/*` (add ToProto, EnrichmentEvent case)
- `src/Njord.Features/ServiceCollectionExtensions.cs` (register)

Implementation:
- [x] Add `AsthmaDominantFactor` enum: `Pm25`, `Ozone`, `Pollen`, `Humidity`, `BoundaryLayer`, `ColdAir`, `TempChange`
- [x] Add `AsthmaRiskResult` record extending `ScoreFeatureResult` with `AsthmaDominantFactor DominantFactor`, `int Pm25Component`, `int OzoneComponent`, `int PollenComponent`
- [x] Add proto enum and `AsthmaRisk` message. Use field number 55 in oneof
- [x] Add ToProto extensions and EnrichmentEvent case
- [x] Register `AsthmaConfig`, `AsthmaConfigValidator`, and `AsthmaScore` in AddFeatures

## 9. AsthmaRisk tests

Files:
- `src/Njord.Features.Tests/Scores/Asthma/AsthmaScoreTests.cs`
- `src/Njord.Features.Tests/Scores/Asthma/AsthmaConfigValidatorTests.cs`
- `src/Njord.Grpc.Tests/Mapping/AsthmaRiskMappingTests.cs`

Implementation:
- [x] Test high PM2.5 → high score with PM25 as dominant
- [x] Test high ozone → dominant ozone
- [x] Test cold air amplifier (temp < 5 + humid > 80 → ×1.3)
- [x] Test weight validation: valid (sum = 1.0), invalid (sum ≠ 1.0)
- [x] Test enum mapping and proto round-trip

## 10. Update docs/data/features.json

- [x] Add MigraineRisk entry: category Health, output Alert, default off, config options
- [x] Add SleepQuality entry: category Lifestyle, output Score, default off, config options
- [x] Add AsthmaRisk entry: category Health, output Score, default off, config options

## 11. ha-njord entity descriptions and translations

Files in `D:\GIT\ha-njord\custom_components\njord\`:
- Add entity descriptions for the three features in the appropriate platform files
- Add translation_keys to `strings.json`, `translations/en.json`, `translations/de.json`

Implementation:
- [x] Add MigraineRisk alert binary sensor description (translation_key: `migraine_risk_alert`)
- [x] Add SleepQuality sensor description (translation_key: `sleep_quality`) with enum states for recommendation and dominant factor
- [x] Add AsthmaRisk sensor description (translation_key: `asthma_risk`) with enum states for dominant factor
- [x] Add EN translations: Migraine Risk Alert / Sleep Quality / Asthma Risk + all enum states
- [x] Add DE translations: Migräne-Risiko / Schlafqualität / Asthma-Risiko + all enum states

## Validation

```powershell
# From src/
dotnet build Njord.slnx
dotnet format Njord.slnx --verify-no-changes

# Run all test projects
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
dotnet run --project Njord.Messages.Tests/Njord.Messages.Tests.csproj
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
```
