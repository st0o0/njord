## 1. BbqConfig and BbqScore

- [x] 1.1 Add scoring properties to `src/Njord.Features/Scores/Bbq/BbqConfig.cs`: GoodHourScore (70), RainThreshold (0.1), four weights (Temperature 0.35, Wind 0.20, Rain 0.30, Cloud 0.15), ten band/ramp params (TemperatureLow 10, TemperatureIdeal 18, TemperatureWarm 28, TemperatureHot 36, WindModerate 5, WindStrong 10, RainProbabilityLow 0, RainProbabilityHigh 60, CloudLow 40, CloudHigh 100)
- [x] 1.2 Create `src/Njord.Features/Scores/Bbq/BbqConfigValidator.cs`: weight sum check (epsilon 0.001), band ordering (Low < Ideal < Warm < Hot), positive GoodHourScore
- [x] 1.3 Update `src/Njord.Features/Scores/Bbq/BbqScore.cs`: remove constants, read all values from `config.Value`
- [x] 1.4 Update `AddFeatures` registration in `src/Njord.Features/ServiceCollectionExtensions.cs`: change to `AddFeature<BbqScore, BbqConfig, BbqConfigValidator>()`
- [x] 1.5 Add tests in `src/Njord.Features.Tests/Scores/Bbq/`: BbqConfigValidatorTests (weight sum, band ordering, valid defaults), update BbqScoreTests to verify config-driven behavior

## 2. OutdoorConfig and OutdoorScore

- [x] 2.1 Add scoring properties to `src/Njord.Features/Scores/Outdoor/OutdoorConfig.cs`: GoodHourScore (85), RainThreshold (0.1), six weights (Temperature 0.3, Rain 0.2, Wind 0.1, Cloud 0.1, Uv 0.1, AirQuality 0.2), fourteen band/ramp params
- [x] 2.2 Create `src/Njord.Features/Scores/Outdoor/OutdoorConfigValidator.cs`: weight sum check, band ordering, positive GoodHourScore
- [x] 2.3 Update `src/Njord.Features/Scores/Outdoor/OutdoorScore.cs`: remove constants, read all values from `config.Value`
- [x] 2.4 Update `AddFeatures` registration to `AddFeature<OutdoorScore, OutdoorConfig, OutdoorConfigValidator>()`
- [x] 2.5 Add tests in `src/Njord.Features.Tests/Scores/Outdoor/`: OutdoorConfigValidatorTests, update OutdoorScoreTests

## 3. ExerciseAdvisoryConfig and ExerciseAdvisory

- [x] 3.1 Add scoring properties to `src/Njord.Features/Conditions/ExerciseAdvisory/ExerciseAdvisoryConfig.cs`: SafeScore (70), CautionScore (45), ReasonThreshold (0.5), five weights (Pm25 0.4, Ozone 0.2, Temperature 0.2, Humidity 0.1, Uv 0.1), ten ramp/band params
- [x] 3.2 Create `src/Njord.Features/Conditions/ExerciseAdvisory/ExerciseAdvisoryConfigValidator.cs`: weight sum check, SafeScore > CautionScore > 0, ramp ordering
- [x] 3.3 Update `src/Njord.Features/Conditions/ExerciseAdvisory/ExerciseAdvisory.cs`: remove constants, read all values from `config.Value`, pass config to `Components`
- [x] 3.4 Update `AddFeatures` registration to `AddFeature<ExerciseAdvisory, ExerciseAdvisoryConfig, ExerciseAdvisoryConfigValidator>()`
- [x] 3.5 Add tests in `src/Njord.Features.Tests/Conditions/ExerciseAdvisory/`: ExerciseAdvisoryConfigValidatorTests, update ExerciseAdvisoryTests

## 4. SleepQualityConfig extension

- [x] 4.1 Add penalty and threshold properties to `src/Njord.Features/Scores/SleepQuality/SleepQualityConfig.cs`: HighPollen (50), Pm25Limit (25), GustLimit (11.1), CapeLimit (1000), PreSleepHours (6), all penalty values and temperature/humidity thresholds for both night and ventilation scoring
- [x] 4.2 Update `src/Njord.Features/Scores/SleepQuality/SleepQualityConfigValidator.cs`: add validation for positive thresholds, PreSleepHours >= 1, penalties >= 0, temperature ordering (ColdThreshold < WarmThreshold < HotThreshold)
- [x] 4.3 Update `src/Njord.Features/Scores/SleepQuality/SleepQualityScore.cs`: remove constants, read all values from `config.Value`
- [x] 4.4 Update tests in `src/Njord.Features.Tests/Scores/SleepQuality/`: add SleepQualityConfigValidatorTests for new properties, update SleepQualityScoreTests

## 5. AllergySeverityConfig extension

- [x] 5.1 Add reference and modifier properties to `src/Njord.Features/Scores/AllergySeverity/AllergySeverityConfig.cs`: PollenReference (100), GoodHourScore (60), five modifier values, five threshold values
- [x] 5.2 Update `src/Njord.Features/Scores/AllergySeverity/AllergySeverityConfigValidator.cs`: add validation for positive references, modifier ranges
- [x] 5.3 Update `src/Njord.Features/Scores/AllergySeverity/AllergySeverityScore.cs`: remove constants, read all values from `config.Value`
- [x] 5.4 Update tests in `src/Njord.Features.Tests/Scores/AllergySeverity/`: add validator tests for new properties, update scoring tests

## 6. AsthmaRiskConfig extension

- [x] 6.1 Add reference and trigger properties to `src/Njord.Features/Scores/AsthmaRisk/AsthmaRiskConfig.cs`: Pm25Reference (75), OzoneReference (180), PollenReference (100), GoodHourScore (60), ColdAirFactor (1.3), RapidChangeFactor (1.2), RapidChangeDegrees (6), RapidChangeHours (3)
- [x] 6.2 Update `src/Njord.Features/Scores/AsthmaRisk/AsthmaRiskConfigValidator.cs`: add validation for positive references, factors > 1.0, RapidChangeDegrees > 0, RapidChangeHours >= 1
- [x] 6.3 Update `src/Njord.Features/Scores/AsthmaRisk/AsthmaRiskScore.cs`: remove constants, read all values from `config.Value`
- [x] 6.4 Update tests in `src/Njord.Features.Tests/Scores/AsthmaRisk/`: add validator tests for new properties, update scoring tests

## 7. LaundryConfig extension

- [x] 7.1 Add scoring properties to `src/Njord.Features/Scores/Laundry/LaundryConfig.cs`: GoodHourScore (60), RainThreshold (0.1), five weights (Temperature 0.25, Humidity 0.25, Wind 0.15, Rain 0.20, Sun 0.15), nine ramp/band params
- [x] 7.2 Create `src/Njord.Features/Scores/Laundry/LaundryConfigValidator.cs` (replace existing if only Window validation): add weight sum check, band ordering, positive GoodHourScore
- [x] 7.3 Update `src/Njord.Features/Scores/Laundry/LaundryScore.cs`: remove constants, read all values from `config.Value`
- [x] 7.4 Update `AddFeatures` registration if needed
- [x] 7.5 Update tests in `src/Njord.Features.Tests/Scores/Laundry/`: LaundryConfigValidatorTests, update LaundryScoreTests

## 8. SprayWindowConfig extension

- [x] 8.1 Add parameters to `src/Njord.Features/Scores/SprayWindow/SprayWindowConfig.cs`: RainProbabilityLimit (20), TemperatureFalloff (10), HumidityMin (40), HumidityMax (80), HumidityFalloff (20)
- [x] 8.2 Update `src/Njord.Features/Scores/SprayWindow/SprayWindowConfigValidator.cs`: add validation for positive falloffs, HumidityMin < HumidityMax, RainProbabilityLimit in 0..100
- [x] 8.3 Update `src/Njord.Features/Scores/SprayWindow/SprayWindowScore.cs`: remove constants, read all values from `config.Value`
- [x] 8.4 Update tests in `src/Njord.Features.Tests/Scores/SprayWindow/`: add validator tests for new properties, update scoring tests

## 9. SurfConditionsConfig and SurfConditionsRule

- [x] 9.1 Add surf parameters to `src/Njord.Features/Conditions/SurfConditions/SurfConditionsConfig.cs`: WindowHours (24), MediumPeriodSeconds (8), LongPeriodSeconds (12), four height params, OffshoreToleranceDegrees (45), ChoppyShare (0.5), VeryChoppyShare (0.75), PoorScoreCap (39)
- [x] 9.2 Create `src/Njord.Features/Conditions/SurfConditions/SurfConditionsConfigValidator.cs`: MediumPeriod < LongPeriod, height ordering, positive tolerances, WindowHours >= 1, ChoppyShare < VeryChoppyShare
- [x] 9.3 Update `src/Njord.Features/Conditions/Shared/SurfConditionsRule.cs`: add `SurfConditionsConfig` parameter to `Evaluate` and `Assess`, replace public constants with config reads
- [x] 9.4 Update `src/Njord.Features/Conditions/SurfConditions/SurfConditions.cs`: pass `config.Value` to `SurfConditionsRule.Evaluate`
- [x] 9.5 Update `AddFeatures` registration to `AddFeature<SurfConditions, SurfConditionsConfig, SurfConditionsConfigValidator>()`
- [x] 9.6 Update tests in `src/Njord.Features.Tests/Conditions/SurfConditions/`: SurfConditionsConfigValidatorTests, update SurfConditionsTests and SurfConditionsRuleTests to pass config

## 10. features.json and Config Builder

- [x] 10.1 Update `docs/data/features.json`: add `options` arrays to Bbq, Outdoor, Exercise, SleepQuality, AllergySeverity, AsthmaRisk, Laundry, SprayWindow, Surf entries with name, type, default and group fields
- [x] 10.2 Update `docs/.vitepress/theme/builder/types.ts`: add group-aware option rendering types
- [x] 10.3 Update `docs/.vitepress/theme/builder/ConfigBuilder.vue`: render grouped options with collapsible sections, add weight-sum validation with inline error display
- [x] 10.4 Update `docs/.vitepress/theme/builder/serializer.ts`: serialize/deserialize new option properties in JSON and env-var export/import

## 11. Validation

- [x] 11.1 Run `dotnet build Njord.slnx` from `src/`
- [x] 11.2 Run `dotnet format Njord.slnx --verify-no-changes` from `src/`
- [x] 11.3 Run all test projects sequentially from `src/`:
  ```
  dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
  dotnet run --project Njord.Messages.Tests/Njord.Messages.Tests.csproj
  dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj
  dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
  dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj
  dotnet run --project Njord.Polling.Tests/Njord.Polling.Tests.csproj
  dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
  dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
  dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
  dotnet run --project Njord.IntegrationTests/Njord.IntegrationTests.csproj
  dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
  ```
