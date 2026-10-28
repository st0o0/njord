# Tasks: cross-domain-intelligence

## 1. C# enums and result types

- [x] Add `DominantStressor` enum to `src/Njord.Messages/Alerts/` (`WetBulb`, `UvPollution`, `HeatInversion`)
- [x] Add `DominantAllergen` enum to `src/Njord.Messages/Alerts/` (`Birch`, `Grass`, `Alder`, `Mugwort`, `Ragweed`)
- [x] Add `AllergyModifier` enum to `src/Njord.Messages/Alerts/` (`WindDispersal`, `RainWashout`, `InversionTrap`, `LowHumidity`, `HighHumidity`)
- [x] Add `CompoundHeatStressAlertResult` to `src/Njord.Messages/Pipeline/AlertResults.cs` extending `AlertFeatureResult` with `DominantStressor`, `BoundaryLayerHeight` (double), `Pm25` (double)
- [x] Add `AllergySeverityScoreResult` to `src/Njord.Messages/Pipeline/NonAlertResults.cs` extending `ScoreFeatureResult` with `DominantAllergen`, `ImmutableArray<AllergyModifier> ActiveModifiers`, `int BaseScore`, per-species doubles

## 2. CompoundHeatStress feature

- [x] Create `src/Njord.Features/Alerts/CompoundHeat/CompoundHeatConfig.cs` implementing `IConfigSection` at `Njord:Features:Health:CompoundHeat` with `Enabled (false)`, `WetBulbWarningThreshold (28.0)`, `WetBulbSevereThreshold (32.0)`, `WetBulbExtremeThreshold (35.0)`, `BoundaryLayerAmplifierThreshold (500.0)`
- [x] Create `src/Njord.Features/Alerts/CompoundHeat/CompoundHeatAlert.cs` as `FeatureRule<ForecastAqInput>`. Descriptor: `OutputKind.Alert`, `FeatureCategory.Health`, Name `"CompoundHeat"`, `DefaultEnabled: false`, Requirements: Forecast `ApparentTemperature | WetBulbTemperature | UvIndex | BoundaryLayerHeight`, AirQuality `Pm25`. Evaluate: three trigger paths per spec, BoundaryLayer amplifier lowers thresholds by 2°C when < 500m. Return `CompoundHeatStressAlertResult`.
- [x] Register in `src/Njord.Features/ServiceCollectionExtensions.cs`: `AddNjordOptions<CompoundHeatConfig>()` and `AddSingleton<IFeatureRule, CompoundHeatAlert>()`

## 3. CompoundHeatStress tests

- [x] Create `src/Njord.Features.Tests/Alerts/CompoundHeat/CompoundHeatAlertTests.cs`: test each trigger path (WetBulb, UvPollution, HeatInversion), severity levels, BoundaryLayer amplifier effect, null when below thresholds, null when AirQuality missing
- [x] Create `src/Njord.Features.Tests/Alerts/CompoundHeat/CompoundHeatConfigTests.cs`: default values verification

## 4. AllergySeverity feature

- [x] Create `src/Njord.Features/Scores/AllergySeverity/AllergySeverityConfig.cs` implementing `IConfigSection` at `Njord:Features:AirQuality:AllergySeverity` with `Enabled (false)`, `SensitivityMultiplier (1.0)`, `BirchWeight (1.0)`, `GrassWeight (1.0)`, `RagweedWeight (1.0)`
- [x] Create `src/Njord.Features/Scores/AllergySeverity/AllergySeverityScore.cs` as `FeatureRule<ForecastAqInput>`. Descriptor: `OutputKind.Score`, `FeatureCategory.AirQuality`, Name `"AllergySeverity"`, `DefaultEnabled: false`, Requirements: AirQuality `BirchPollen | GrassPollen | Alder | Mugwort | RagweedPollen`, Forecast `WindSpeed | Humidity | Rain | BoundaryLayerHeight`. Evaluate: normalize max species to 0-100, apply atmospheric modifiers, find best (lowest) window in 24h. Return `AllergySeverityScoreResult`.
- [x] Register in `src/Njord.Features/ServiceCollectionExtensions.cs`: `AddNjordOptions<AllergySeverityConfig>()` and `AddSingleton<IFeatureRule, AllergySeverityScore>()`

## 5. AllergySeverity tests

- [x] Create `src/Njord.Features.Tests/Scores/AllergySeverity/AllergySeverityScoreTests.cs`: base score from max species, modifiers (wind +15, rain -30, inversion +15, low humidity +10, high humidity -15), clamping to 0-100, sensitivity multiplier, species weights, BestStart/BestHours for lowest-pollen window, null when no pollen data
- [x] Create `src/Njord.Features.Tests/Scores/AllergySeverity/AllergySeverityConfigTests.cs`: default values

## 6. Proto messages and gRPC mapping

- [x] Add `DominantStressor`, `DominantAllergen`, `AllergyModifier` enums to `protos/njord/v2/weather.proto` with `PREFIX_VALUE` naming
- [x] Add `CompoundHeatStress` message to `weather.proto` (AlertData alert, DominantStressor, double boundary_layer_height, double pm25)
- [x] Add `AllergySeverity` message to `weather.proto` (ScoreData score, DominantAllergen, repeated AllergyModifier, int32 base_score, per-species doubles)
- [x] Add oneof cases in `EnrichmentEvent`: `CompoundHeatStress compound_heat_stress = 48`, `AllergySeverity allergy_severity = 54`
- [x] Add `ToProto()` for `CompoundHeatStressAlertResult` in `src/Njord.Grpc/Mapping/AlertResultMappingExtensions.cs`
- [x] Add `ToProto()` for `AllergySeverityScoreResult` in `src/Njord.Grpc/Mapping/FeatureResultMappingExtensions.cs` (includes DominantAllergen and ActiveModifiers enum mappings)
- [x] Add enrichment event case for both in `src/Njord.Grpc/Mapping/EnrichmentEventMappingExtensions.cs`

## 7. gRPC mapping tests

- [x] Add mapping tests for `CompoundHeatStressAlertResult.ToProto()` in `src/Njord.Grpc.Tests/Mapping/`
- [x] Add mapping tests for `AllergySeverityScoreResult.ToProto()` in `src/Njord.Grpc.Tests/Mapping/`
- [x] Add enum mapping tests for `DominantStressor`, `DominantAllergen`, `AllergyModifier` (one Theory per direction)

## 8. ha-njord entity descriptions and translations

- [x] Add alert meta entry for `compound_heat_stress` in ha-njord `const.py` ALERT_META
- [x] Add lifestyle sensor description for `allergy_severity` score in ha-njord `lifestyle.py`
- [x] Add `compound_heat_stress_alert` and `allergy_severity` to `strings.json`, `translations/en.json`, `translations/de.json` with entity names and enum states for DominantStressor, DominantAllergen, AllergyModifier

## 9. docs/data/features.json

- [x] Add CompoundHeatStress entry (category Health, alert, off by default, config options)
- [x] Add AllergySeverity entry (category AirQuality, score, off by default, config options)

## Validation

```powershell
# From src/
dotnet build Njord.slnx
dotnet format Njord.slnx --verify-no-changes
```

```bash
# Run all test projects
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
```
