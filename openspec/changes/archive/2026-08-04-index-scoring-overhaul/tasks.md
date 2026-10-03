## 1. Remove HDD/CDD

- [x] 1.1 Remove `HeatingDegreeDays` and `CoolingDegreeDays` from `src/Njord/Domain/Analysis/IndexScorer.cs`; remove `HeatingBaseTemp` and `CoolingBaseTemp` from `IndexOptions` in `src/Njord/Configuration/EnrichmentOptions.cs`
- [x] 1.2 Remove `Hdd` and `Cdd` properties from `IndexResult` record in `src/Njord/Domain/Analysis/IndexResult.cs`; remove their computation in `IndexResult.Compute`; remove from `ComputeScoresFromMeans`
- [x] 1.3 Remove HDD/CDD discovery components from `src/Njord/Enrichment/Features/IndexEnrichment.cs` and `src/Njord/Mqtt/DiscoveryPayloadBuilder.cs`; remove from state payload in `src/Njord/Mqtt/StatePayloadBuilder.cs`
- [x] 1.4 Remove HDD/CDD mapping from `src/Njord/Grpc/EnrichmentProtoMapper.cs`
- [x] 1.5 Update tests: `src/Njord.Tests/Domain/Analysis/IndexScorerSpec.cs` (remove HDD/CDD tests), `src/Njord.Tests/Mqtt/DiscoveryPayloadBuilderSpec.cs`, verified snapshot files in `src/Njord.Tests/Persistence/`

## 2. Preference Configuration Model

- [x] 2.1 Create `IndexPreferences` class in `src/Njord/Configuration/EnrichmentOptions.cs` with all sensitivity multipliers (default 1.0) and ideal-point parameters; create `LocationIndexOverride` class with `Location`, optional `Preferences`, optional `ScoreOverrides`
- [x] 2.2 Restructure `IndexOptions`: add `Preferences` (IndexPreferences), `ScoreOverrides` (IDictionary<string, IndexPreferences>), `LocationOverrides` (IList<LocationIndexOverride>); remove `IndoorTemp` (moves into preferences)
- [x] 2.3 Create `ResolvedPreferences` record in `src/Njord/Domain/Analysis/` with all non-nullable preference properties
- [x] 2.4 Create `PreferenceResolver` in `src/Njord/Domain/Analysis/` implementing the 5-level cascade: location.score → location.global → score → global → hardcoded default; returns `IReadOnlyDictionary<(string Location, string Score), ResolvedPreferences>`
- [x] 2.5 Add config validation: clamp sensitivities to [0.0, 5.0], warn on unknown score names, warn on unknown location names; integrate into existing config validation flow
- [x] 2.6 Write `PreferenceResolverSpec` in `src/Njord.Tests/Domain/Analysis/` testing all 5 cascade levels, partial overrides, unknown names, sensitivity clamping

## 3. Fix Outdoor Score Formula

- [x] 3.1 Add `BreezeScore` method to `IndexScorer` — bell curve with peak at 2–4 m/s, windstill (< 1 m/s) penalized, strong wind (> 8 m/s) penalized; accepts `WindSensitivity` multiplier
- [x] 3.2 Add `HumidityPenaltyScore` method (or reuse existing `HumidityScore`) with `HumiditySensitivity` multiplier support
- [x] 3.3 Update `OutdoorScore` signature to accept humidity and `ResolvedPreferences`; apply new weights (0.30 temp, 0.20 humidity, 0.20 rain, 0.15 breeze, 0.15 cloud); use `TempComfort` with `IdealTemp` and `HeatSensitivity`
- [x] 3.4 Write/update `IndexScorerSpec` tests: pleasant day ≥ 80, stormy day ≤ 10, schwül day (33°C/85%/0.5 m/s) ≤ 40, high sensitivity schwül ≤ 32, shifted ideal temp

## 4. Thread Preferences Through All Scorers

- [x] 4.1 Update all `IndexScorer` method signatures to accept `ResolvedPreferences`: `LaundryDrying`, `RunningComfort`, `CyclingComfort`, `BbqWeather`, `IrrigationNeed`, `SolarYield`, `Ventilation`
- [x] 4.2 Apply sensitivity multipliers in each scorer's penalty terms; use ideal-point parameters from preferences (RunningComfort: `IdealTempLow`/`IdealTempHigh`; BbqWeather: `MinTemp`/`IdealWindLow`/`IdealWindHigh`; Ventilation: `IndoorTemp`)
- [x] 4.3 Update `IndexResult.Compute` to accept a preference resolver/dictionary; resolve per-score preferences and pass to each scorer call
- [x] 4.4 Update envelope computation (`ComputeScoresFromMeans`) to use `ResolvedPreferences`
- [x] 4.5 Update `IndexEnrichment` to build the resolved preferences dictionary at compute time and pass to `IndexResult.Compute`
- [x] 4.6 Update all existing `IndexScorerSpec` tests to pass `ResolvedPreferences` with default values; add tests for custom sensitivity and ideal-point scenarios per scorer

## 5. Wire Format and Egress

- [x] 5.1 Update `StatePayloadBuilder.FromIndices` to remove `hdd`/`cdd` keys; ensure `OutdoorScore` call site passes humidity
- [x] 5.2 Update `EnrichmentProtoMapper` to remove HDD/CDD; ensure new `OutdoorScore` fields map correctly
- [x] 5.3 Update verified snapshot files in `src/Njord.Tests/Persistence/` for `IndexResult` serialization without `hdd`/`cdd`
- [x] 5.4 Update `src/Njord.Tests/Grpc/EnrichmentProtoMapperSpec.cs` for removed HDD/CDD mapping

## 6. Validation

- [x] 6.1 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 6.2 Run `dotnet build Njord.slnx` from `src/` — zero warnings
- [x] 6.3 Run `dotnet slopwatch` from repo root
- [x] 6.4 Run `dotnet format --verify-no-changes` from `src/`
