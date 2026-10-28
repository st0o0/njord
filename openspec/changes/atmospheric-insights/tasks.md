# Tasks: atmospheric-insights

## 1. Proto: enums and messages

Add to `protos/njord/v2/weather.proto`:

- [ ] Enum `InversionStrength` (UNSPECIFIED=0, NONE=1, MILD=2, STRONG=3, EXTREME=4)
- [ ] Enum `InversionType` (UNSPECIFIED=0, RADIATION=1, SUBSIDENCE=2, FRONTAL=3)
- [ ] Enum `StabilityClass` (UNSPECIFIED=0, STABLE=1, CAPPED=2, MARGINAL=3, UNSTABLE=4, EXPLOSIVE=5)
- [ ] Enum `CeilingCategory` (UNSPECIFIED=0, VERY_LOW=1, LOW=2, MODERATE=3, HIGH=4, VERY_HIGH=5, UNLIMITED=6)
- [ ] Message `InversionDetectionCondition` (condition, strength, inversion_type, temperature_difference_c, boundary_layer_height_m, expected_dissolution_hours)
- [ ] Message `AtmosphericStabilityCondition` (condition, stability_class, cape_j_per_kg, cin_j_per_kg, lifted_index)
- [ ] Message `CloudCeilingCondition` (condition, ceiling_category, estimated_ceiling_m, cloud_cover_low_percent, cloud_cover_mid_percent, cloud_cover_high_percent)
- [ ] Add to `EnrichmentEvent.feature` oneof: `InversionDetectionCondition inversion_detection = 76`, `AtmosphericStabilityCondition atmospheric_stability = 77`, `CloudCeilingCondition cloud_ceiling = 78`

Validation: `dotnet build src/Njord.slnx`

## 2. Domain enums and result records

- [ ] Create `InversionStrength` enum in `src/Njord.Messages/Pipeline/` (None, Mild, Strong, Extreme)
- [ ] Create `InversionType` enum in `src/Njord.Messages/Pipeline/` (Radiation, Subsidence, Frontal)
- [ ] Create `StabilityClass` enum in `src/Njord.Messages/Pipeline/` (Stable, Capped, Marginal, Unstable, Explosive)
- [ ] Create `CeilingCategory` enum in `src/Njord.Messages/Pipeline/` (VeryLow, Low, Moderate, High, VeryHigh, Unlimited)
- [ ] Add `InversionDetectionResult(Location, InversionStrength Strength, InversionType Type, double TemperatureDifference, double BoundaryLayerHeight, int DissolutionHours) : ConditionFeatureResult(Location)` to `src/Njord.Messages/Pipeline/NonAlertResults.cs`
- [ ] Add `AtmosphericStabilityResult(Location, StabilityClass Class, double Cape, double? Cin, double? LiftedIndex) : ConditionFeatureResult(Location)` to `src/Njord.Messages/Pipeline/NonAlertResults.cs`
- [ ] Add `CloudCeilingResult(Location, CeilingCategory Category, int EstimatedCeilingM, int CloudCoverLow, int CloudCoverMid, int CloudCoverHigh) : ConditionFeatureResult(Location)` to `src/Njord.Messages/Pipeline/NonAlertResults.cs`

Validation: `dotnet build src/Njord.slnx`

## 3. InversionDetection feature

- [ ] Create `src/Njord.Features/Conditions/InversionDetection/InversionDetectionConfig.cs` — `IConfigSection`, Section `Njord:Features:Insight:InversionDetection`, `Enabled` (default false)
- [ ] Create `src/Njord.Features/Conditions/InversionDetection/InversionDetection.cs` — `FeatureRule<ForecastInput>`, Descriptor with `OutputKind.Condition`, `FeatureCategory.Insight`, `Name: "InversionDetection"`, `DefaultEnabled: false`, `PollDepth: 3`, DomainRequirements with Required Temperature/Temperature80m/Temperature120m/BoundaryLayerHeight, Optional Visibility/CloudCover/WindSpeed/IsDay/SurfacePressure
- [ ] Register in `src/Njord.Features/ServiceCollectionExtensions.cs`: `AddNjordOptions<InversionDetectionConfig>()` and `AddSingleton<IFeatureRule, InversionDetection>()`

Validation: `dotnet build src/Njord.slnx`

## 4. InversionDetection tests

- [ ] Create `src/Njord.Features.Tests/Conditions/InversionDetection/InversionDetectionTests.cs` — sealed class, Facts:
  - Returns MILD when Temp80m > Temp and BoundaryLayer < 800m
  - Returns STRONG when difference > 2°C and BoundaryLayer < 500m
  - Returns EXTREME when difference > 4°C and BoundaryLayer < 300m
  - Returns null when no inversion (Temp80m < Temp)
  - Returns null when Temperature80m data missing
  - Classifies RADIATION type at night with clear sky and calm wind
  - Reports dissolution hours for RADIATION inversion

Validation: `dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj`

## 5. AtmosphericStability feature

- [ ] Create `src/Njord.Features/Conditions/AtmosphericStability/AtmosphericStabilityConfig.cs` — `IConfigSection`, Section `Njord:Features:Insight:AtmosphericStability`, `Enabled` (default false)
- [ ] Create `src/Njord.Features/Conditions/AtmosphericStability/AtmosphericStability.cs` — `FeatureRule<ForecastInput>`, Descriptor with `OutputKind.Condition`, `FeatureCategory.Insight`, `Name: "AtmosphericStability"`, `DefaultEnabled: false`, DomainRequirements with Required Cape, Optional ConvectiveInhibition/LiftedIndex
- [ ] Register in `src/Njord.Features/ServiceCollectionExtensions.cs`

Validation: `dotnet build src/Njord.slnx`

## 6. AtmosphericStability tests

- [ ] Create `src/Njord.Features.Tests/Conditions/AtmosphericStability/AtmosphericStabilityTests.cs` — sealed class, Facts:
  - Returns STABLE when CAPE < 300
  - Returns CAPPED when CAPE >= 1000 and CIN > 200
  - Returns MARGINAL when CAPE 300-1000
  - Returns UNSTABLE when CAPE >= 1000, CIN < 50, LI < -3
  - Returns EXPLOSIVE when CAPE >= 2500, CIN < 50, LI < -6
  - Falls back to CAPE-only classification when CIN/LI null
  - Returns null when CAPE is null

Validation: `dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj`

## 7. CloudCeiling feature

- [ ] Create `src/Njord.Features/Conditions/CloudCeiling/CloudCeilingConfig.cs` — `IConfigSection`, Section `Njord:Features:Insight:CloudCeiling`, `Enabled` (default false)
- [ ] Create `src/Njord.Features/Conditions/CloudCeiling/CloudCeiling.cs` — `FeatureRule<ForecastInput>`, Descriptor with `OutputKind.Condition`, `FeatureCategory.Insight`, `Name: "CloudCeiling"`, `DefaultEnabled: false`, DomainRequirements with Required CloudCoverLow/CloudCoverMid/CloudCoverHigh, Optional FreezingLevelHeight/BoundaryLayerHeight/Visibility
- [ ] Register in `src/Njord.Features/ServiceCollectionExtensions.cs`

Validation: `dotnet build src/Njord.slnx`

## 8. CloudCeiling tests

- [ ] Create `src/Njord.Features.Tests/Conditions/CloudCeiling/CloudCeilingTests.cs` — sealed class, Facts:
  - Returns UNLIMITED when all layers < 30%
  - Returns VERY_LOW when CloudCoverLow >= 50% and BoundaryLayer < 300m and Visibility < 1000m
  - Returns LOW when CloudCoverLow >= 50% and BoundaryLayer 300-1000m
  - Returns MODERATE when CloudCoverLow >= 50% and BoundaryLayer > 1000m
  - Returns HIGH when only CloudCoverMid >= 50%
  - Returns VERY_HIGH when only CloudCoverHigh >= 50%
  - Returns null when all cloud cover layers are null
  - Uses FreezingLevelHeight for HIGH ceiling estimate

Validation: `dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj`

## 9. gRPC mapping

- [ ] Add `ToProto()` extension methods in `src/Njord.Grpc/Mapping/FeatureResultMappingExtensions.cs` for InversionDetectionResult, AtmosphericStabilityResult, CloudCeilingResult
- [ ] Add enum mapping extensions: InversionStrength ↔ Proto, InversionType ↔ Proto, StabilityClass ↔ Proto, CeilingCategory ↔ Proto (explicit switch, one arm per member, default throws ArgumentOutOfRangeException)
- [ ] Add cases to `EnrichmentEventMappingExtensions.cs` for the three new result types → EnrichmentEvent

Validation: `dotnet build src/Njord.slnx`

## 10. gRPC mapping tests

- [ ] Add mapping Theory tests in `src/Njord.Grpc.Tests/Mapping/` for each enum direction (Domain → Proto, Proto → Domain) covering all members
- [ ] Add result mapping tests for each of the three features

Validation: `dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj`

## 11. docs/data/features.json

- [ ] Add three entries to `docs/data/features.json`:
  - `Insight:InversionDetection` — category Insight, output Condition, domains ["Forecast"], enabledByDefault false
  - `Insight:AtmosphericStability` — category Insight, output Condition, domains ["Forecast"], enabledByDefault false
  - `Insight:CloudCeiling` — category Insight, output Condition, domains ["Forecast"], enabledByDefault false

## 12. ha-njord: entity descriptions and translations

In `D:\GIT\ha-njord`:

- [ ] Add sensor descriptions for inversion_detection, atmospheric_stability, cloud_ceiling in `lifestyle.py` (or equivalent)
- [ ] Add translation entries to `strings.json`, `translations/en.json`, `translations/de.json`:
  - Entity names: "Inversion Detection"/"Inversionserkennung", "Atmospheric Stability"/"Atmosphärische Stabilität", "Cloud Ceiling"/"Wolkenuntergrenze"
  - Enum states for each sensor (strength/stability_class/ceiling_category with EN and DE labels)

## 13. Final validation

- [ ] `dotnet build src/Njord.slnx`
- [ ] `dotnet format src/Njord.slnx --verify-no-changes`
- [ ] Run all test projects:
  ```bash
  for p in src/Njord.*Tests; do
    [ "$p" = "src/Njord.Tests.Shared" ] && continue
    dotnet run --project "$p/$p.csproj" --no-build
  done
  ```
