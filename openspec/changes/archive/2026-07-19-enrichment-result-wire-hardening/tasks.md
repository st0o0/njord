## 1. Simple records — AlertResult, DerivedResult

- [x] 1.1 Add `[property: JsonProperty]` to `AlertResult`, `Alert` in `src/Njord/Domain/Analysis/AlertResult.cs` and `AlertEvaluator.cs`; add `[JsonProperty]` to `AlertType` and `AlertSeverity` enums if needed; update any test code constructing these types
- [x] 1.2 Add `[property: JsonProperty]` to `DerivedResult`, `HorizonDerived`, `ScalarDerived` in `src/Njord/Domain/Analysis/DerivedResult.cs`; update test call sites
- [x] 1.3 Commit: `feat(persistence): add [JsonProperty] to AlertResult, DerivedResult and nested types`

## 2. Tuple replacement — IndexResult

- [x] 2.1 Create `FrostProtectionInfo` and `VpdInfo` named records in `src/Njord/Domain/Analysis/IndexResult.cs` with `[property: JsonProperty]`; replace tuple properties on `IndexResult`; add `[property: JsonProperty]` to `IndexResult` and `ScoreEnvelope`
- [x] 2.2 Update all call sites and tests constructing `IndexResult` with tuple syntax — grep for `FrostProtection` and `Vpd` tuple construction across `src/`
- [x] 2.3 Commit: `feat(persistence): add [JsonProperty] to IndexResult, replace tuples with named records`

## 3. Tuple replacement — TrendResult

- [x] 3.1 Create `PrecipTimingInfo`, `ExtremaTimingInfo`, `StabilityInfo`, `DecayInfo` named records in `src/Njord/Domain/Analysis/TrendResult.cs` with `[property: JsonProperty]`; replace tuple properties on `TrendResult`; add `[property: JsonProperty]` to `TrendResult`, `ParameterTrend`
- [x] 3.2 Add `[property: JsonProperty]` to `WeatherChangeResult` in `src/Njord/Domain/Analysis/TrendAnalyzer.cs`
- [x] 3.3 Update all call sites and tests constructing `TrendResult` with tuple syntax
- [x] 3.4 Commit: `feat(persistence): add [JsonProperty] to TrendResult, replace tuples with named records`

## 4. Tuple replacement — EnergyResult

- [x] 4.1 Create `CopOptimalEntry` named record in `src/Njord/Domain/Analysis/EnergyResult.cs` with `[property: JsonProperty]`; replace `(int HoursFromNow, double Cop)` in `CopOptimal` list; add `[property: JsonProperty]` to `EnergyResult`
- [x] 4.2 Update all call sites and tests constructing `EnergyResult` with tuple syntax for CopOptimal
- [x] 4.3 Commit: `feat(persistence): add [JsonProperty] to EnergyResult, replace CopOptimal tuple`

## 5. Tuple replacement — ConsensusResult + domain types

- [x] 5.1 Create `OutlierInfo` and `ConfidenceIntervalInfo` named records in `src/Njord/Domain/Analysis/ConsensusResult.cs` with `[property: JsonProperty]`; replace tuple properties on `HorizonConsensus`; add `[property: JsonProperty]` to `ConsensusResult`, `ParameterConsensus`, `HorizonConsensus`
- [x] 5.2 Add `[property: JsonProperty]` to `WeatherModel` in `src/Njord/Domain/Weather/WeatherModel.cs` and `ParameterDef` in `src/Njord/Domain/Weather/ParameterDef.cs`
- [x] 5.3 Update all call sites and tests constructing `HorizonConsensus` with tuple syntax for Outlier/ConfidenceInterval
- [x] 5.4 Commit: `feat(persistence): add [JsonProperty] to ConsensusResult, replace tuples, harden domain types`

## 6. Verify snapshot tests + cleanup

- [x] 6.1 Add Verify snapshot tests in `src/Njord.Tests/Persistence/EnrichmentResultSerializationSpec.cs` — round-trip each of the 6 result types through `EnrichmentSnapshotMapping.ToDto`/`ToDomain` and verify the nested JSON wire format
- [x] 6.2 Remove the EnrichmentEntryDto caveat from `CLAUDE.md`
- [x] 6.3 Commit: `feat(persistence): add Verify tests for enrichment result wire format, remove caveat`

## Validation

- [x] 7.1 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/` — all tests must pass
- [x] 7.2 Run `dotnet build Njord.slnx` from `src/` — 0 errors, 0 warnings (excluding pre-existing)
