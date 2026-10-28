# Tasks: agriculture-garden-features

## 1. Add FeatureCategory.Agriculture and shared enums

- [x] Add `Agriculture` to `FeatureCategory` enum in `src/Njord.Core/Domain/FeatureDescriptor.cs` (or wherever `FeatureCategory` is defined)
- [x] Add `SoilType` (Sandy, Loam, Clay) and `PlantType` (Lawn, Garden, Vegetables, Trees) enums in `src/Njord.Messages/Pipeline/NonAlertResults.cs` (alongside other feature enums) or a new file `src/Njord.Messages/Config/AgricultureEnums.cs` if the convention separates config-only enums
- [x] Add `IrrigationAdvice` (Water, Skip, Reduce, SoilWet), `SkipReason` (RainExpected, SoilWet, TemperatureLow, Adequate), `FrostDepthRisk` (None, Surface, Shallow, Moderate, Deep) enums in `src/Njord.Messages/Pipeline/NonAlertResults.cs`
- [x] Add result records: `IrrigationNeedResult(Location, IrrigationAdvice, SkipReason?, double SoilMoisture, double RainExpectedMm, double EtMm, double Vpd) : ConditionFeatureResult`, `FrostDepthResult(Location, FrostDepthRisk, double DepthCm, double SnowInsulationCm, double SoilTemperatureC, bool PipeRisk) : ConditionFeatureResult`, `SprayWindowResult(Location, int Score, DateTimeOffset BestStart, int BestHours) : ScoreFeatureResult`

**Validation:** `dotnet build src/Njord.slnx`

## 2. IrrigationNeed feature

- [x] Create `src/Njord.Features/Conditions/IrrigationNeed/IrrigationNeedConfig.cs` — `IConfigSection` at `Njord:Features:Agriculture:IrrigationNeed` with Enabled (false), SoilType (Loam), PlantType (Garden), WetThreshold (80.0), DryThreshold (30.0)
- [x] Create `src/Njord.Features/Conditions/IrrigationNeed/IrrigationNeed.cs` — `FeatureRule<ForecastInput>` with `DomainRequirements(Forecast: ForecastParams.Evapotranspiration | ForecastParams.SoilMoisture0To1cm | ForecastParams.VapourPressureDeficit | ForecastParams.Rain | ForecastParams.PrecipitationProbability | ForecastParams.Temperature)`. Implement decision matrix from spec.
- [x] Register in `src/Njord.Features/ServiceCollectionExtensions.cs`: `AddNjordOptions<IrrigationNeedConfig>()` + `AddSingleton<IFeatureRule, IrrigationNeed>()`

**Validation:** `dotnet build src/Njord.slnx`

## 3. IrrigationNeed tests

- [x] Create `src/Njord.Features.Tests/Conditions/IrrigationNeed/IrrigationNeedTests.cs` — test decision matrix: soil wet → SoilWet, cold → Skip/TemperatureLow, rain expected → Skip/RainExpected, ET > rain + dry soil → Water, moderate rain → Reduce, adequate → Skip/Adequate
- [x] Test soil type threshold variations (Sandy vs Clay)

**Validation:** `dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj`

## 4. FrostDepth feature

- [x] Create `src/Njord.Features/Conditions/FrostDepth/FrostDepthConfig.cs` — `IConfigSection` at `Njord:Features:Agriculture:FrostDepth` with Enabled (false), SoilType (Loam), PipeDepthCm (60)
- [x] Create `src/Njord.Features/Conditions/FrostDepth/FrostDepth.cs` — `FeatureRule<ForecastInput>` with `DomainRequirements(Forecast: ForecastParams.Temperature | ForecastParams.SoilTemperature | ForecastParams.SnowDepth | ForecastParams.WindSpeed)`. Implement Stefan model from spec.
- [x] Register in `ServiceCollectionExtensions.cs`

**Validation:** `dotnet build src/Njord.slnx`

## 5. FrostDepth tests

- [x] Create `src/Njord.Features.Tests/Conditions/FrostDepth/FrostDepthTests.cs` — test risk classification: no frost (warm), surface frost, deep frost with wind, snow insulation reducing depth, pipe risk threshold, soil type factor differences
- [x] Test edge case: soil temp > 0 returns None regardless of air temp

**Validation:** `dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj`

## 6. SprayWindow feature

- [x] Create `src/Njord.Features/Scores/SprayWindow/SprayWindowConfig.cs` — `IConfigSection` at `Njord:Features:Agriculture:SprayWindow` with Enabled (false), MaxWindSpeed (3.0), RainFreeHours (4), MinTemp (10.0), MaxTemp (25.0), MinScore (50)
- [x] Create `src/Njord.Features/Scores/SprayWindow/SprayWindow.cs` — `FeatureRule<ForecastInput>` with `DomainRequirements(Forecast: ForecastParams.WindSpeed | ForecastParams.Rain | ForecastParams.PrecipitationProbability | ForecastParams.Temperature | ForecastParams.Humidity)`. Implement per-hour scoring + contiguous window extraction from spec.
- [x] Register in `ServiceCollectionExtensions.cs`

**Validation:** `dotnet build src/Njord.slnx`

## 7. SprayWindow tests

- [x] Create `src/Njord.Features.Tests/Scores/SprayWindow/SprayWindowTests.cs` — test: high wind → score 0, rain expected → score 0, perfect conditions → high score, window extraction finds contiguous block, BestStart/BestHours correct, temperature falloff
- [x] Test edge: all hours bad → null result

**Validation:** `dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj`

## 8. Proto messages and gRPC mapping

- [x] Add to `protos/njord/v2/weather.proto`: enums `IrrigationAdvice`, `SkipReason`, `FrostDepthRisk` (all with UNSPECIFIED=0 and prefixed members); messages `IrrigationNeedCondition`, `FrostDepthCondition`, `SprayWindowScore`; add oneof cases in `EnrichmentEvent` (pick next available field numbers in the Conditions 60-79 range and Scores 50-59 range)
- [x] Add gRPC mapping in `src/Njord.Grpc/Mapping/FeatureResultMappingExtensions.cs`: `ToProto()` for `IrrigationNeedResult`, `FrostDepthResult`, `SprayWindowResult` (follow SoilMoisture/Evapotranspiration pattern)
- [x] Add enum mapping in `src/Njord.Grpc/Mapping/FeatureEnumMappingExtensions.cs` if needed
- [x] Add dispatch in `src/Njord.Grpc/Mapping/EnrichmentEventMappingExtensions.cs`: pattern match for the three new result types

**Validation:** `dotnet build src/Njord.slnx`

## 9. Proto mapping tests

- [x] Add mapping tests in `src/Njord.Grpc.Tests/` for the three new feature results — verify round-trip enum mapping completeness (one Theory per enum direction)

**Validation:** `dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj`

## 10. ha-njord entity descriptions and translations

- [x] Add entity descriptions for IrrigationNeed (enum sensor), FrostDepth (enum sensor), SprayWindow (numeric sensor + binary sensor) in ha-njord `custom_components/njord/lifestyle.py` (or appropriate file)
- [x] Add translation entries to `strings.json`, `translations/en.json`, `translations/de.json`:
  - sensor.irrigation: name + state (water/skip/reduce/soil_wet) + state_attributes.skip_reason
  - sensor.frost_depth: name + state (none/surface/shallow/moderate/deep)
  - sensor.spray_score: name
  - binary_sensor.spray_window: name

**Validation:** JSON syntax check on all three translation files

## 11. Update docs/data/features.json

- [x] Add entries for IrrigationNeed, FrostDepth, SprayWindow with category Agriculture, options, defaults, required domains, and descriptions

## 12. Full validation

- [x] Run all test projects:
  ```bash
  cd src && for p in Njord.*Tests; do
    [ "$p" = Njord.Tests.Shared ] && continue
    dotnet run --project "$p/$p.csproj" --no-build
  done
  ```
- [x] Run `dotnet format src/Njord.slnx --verify-no-changes`
