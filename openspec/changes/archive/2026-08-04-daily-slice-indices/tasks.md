## 1. DaySlice Domain Types and TimeSliceAggregator

- [x] 1.1 Create `DaySlice` sealed record in `src/Njord/Domain/Analysis/DaySlice.cs` with `DayOffset`, `DayMeans`, `NightMeans`, `FullDayMeans` (all `IReadOnlyDictionary<ParameterDef, double?>`), `DaylightHoursCount`, `NighttimeHoursCount`
- [x] 1.2 Create `TimeSliceAggregator` static class in `src/Njord/Domain/Analysis/TimeSliceAggregator.cs` with `AggregateDaySlices(ConsensusSnapshot, ResolvedParameterSet, TimeProvider) → IReadOnlyList<DaySlice>`. Implement hour-to-day mapping (UTC midnight boundaries), is_day partitioning, and parameter mean computation per slice
- [x] 1.3 Write `TimeSliceAggregatorSpec` in `src/Njord.Tests/Domain/Analysis/TimeSliceAggregatorSpec.cs` — cover: 3 full days, partial today, fewer than 3 days, midnight boundary, is_day missing fallback, all-null means, empty consensus

## 2. NightVentilation Scorer

- [x] 2.1 Rename `IndexScorer.Ventilation` to `IndexScorer.NightVentilation` in `src/Njord/Domain/Analysis/IndexScorer.cs` — method name only, formula unchanged
- [x] 2.2 Update `IndexScorerSpec` in `src/Njord.Tests/Domain/Analysis/IndexScorerSpec.cs` — rename all Ventilation test methods to NightVentilation, verify scenarios from night-ventilation spec

## 3. DaySliceIndexResult and DayScoreSet

- [x] 3.1 Create `DayScoreSet` sealed record in `src/Njord/Domain/Analysis/DaySliceIndexResult.cs` with all score fields (Laundry, Outdoor, Running, Cycling, Bbq, Irrigation, Solar, NightVentilation), HoursIncluded, and per-score ScoreEnvelope fields. All properties with `[JsonProperty]` pinned wire names (`night_ventilation`, not `ventilation`)
- [x] 3.2 Create `DaySliceIndexResult` sealed record in the same file with `Location`, `Days` (`IReadOnlyList<DayScoreSet>`), `FrostProtection`, `Vpd`. All properties with `[JsonProperty]` pinned wire names
- [x] 3.3 Implement `DaySliceIndexResult.Compute` — use `TimeSliceAggregator` to get day slices, compute activity scores from DayMeans, utility scores from FullDayMeans, NightVentilation from NightMeans, envelopes per day. FrostProtection and VPD computed once (existing logic, unchanged)
- [x] 3.4 Update `IndexResultSpec` in `src/Njord.Tests/Domain/Analysis/IndexResultSpec.cs` — rewrite for `DaySliceIndexResult`: multi-day output, daylight-only activity scores, full-day utility scores, nighttime NightVentilation, zero-daylight-hours fallback, frost/VPD unchanged

## 4. Preference Resolution Update

- [x] 4.1 Update `PreferenceResolver.ScoreNames` in `src/Njord/Domain/Analysis/PreferenceResolver.cs` — replace `"Ventilation"` with `"NightVentilation"`
- [x] 4.2 Update `PreferenceResolverSpec` in `src/Njord.Tests/Domain/Analysis/PreferenceResolverSpec.cs` — verify NightVentilation key resolution, verify old Ventilation key triggers unknown-score warning

## 5. IndexEnrichment Adaptation

- [x] 5.1 Update `IndexEnrichment.Compute` in `src/Njord/Enrichment/Features/IndexEnrichment.cs` — call `DaySliceIndexResult.Compute` instead of `IndexResult.Compute`, yield `EgressEvent.EnrichmentUpdate` with the new result type
- [x] 5.2 Update `IndexEnrichment.BuildDiscoveryPayload` — iterate day offsets (d0/d1/d2), register per-day score components (`outdoor_d0`, `outdoor_d1`, etc.) with day-offset state topics. Frost/VPD components reference d0 topic only
- [x] 5.3 Update `IndexEnrichment.ToStateMessages` — produce one `MqttMessage` per day offset on `<baseTopic>/<location>/indices/d0` etc., include `hours_included` in each payload
- [x] 5.4 Update `IndexEnrichmentSpec` in `src/Njord.Tests/Enrichment/Features/IndexEnrichmentSpec.cs` — verify 3 state messages, day-offset topics, discovery component count, frost/VPD on d0 only

## 6. MQTT State and Discovery Payloads

- [x] 6.1 Update `StatePayloadBuilder.FromIndices` in `src/Njord/Mqtt/StatePayloadBuilder.cs` — accept `DaySliceIndexResult`, produce one `MqttMessage` per `DayScoreSet` with day-offset topic suffix. Use `night_ventilation` key. Include `hours_included`. Frost/VPD fields on d0 message only
- [x] 6.2 Update `TopicScheme` in `src/Njord/Egress/TopicScheme.cs` if needed — add helper for day-offset enrichment topics (`EnrichmentTopic` with day offset parameter)
- [x] 6.3 Update state payload tests to verify per-day JSON structure, `night_ventilation` key, `hours_included` field, frost/VPD on d0 only

## 7. Persistence DTO Update

- [x] 7.1 Add or update persistence DTO for `DaySliceIndexResult` in `src/Njord/Persistence/` — bump version, ensure old `IndexResult` snapshots remain deserializable (recovery handles version ≥ 1). New DTO must handle the `Days` list structure

## 8. Cleanup and Migration

- [x] 8.1 Remove old `IndexResult` record (or mark as legacy for deserialization only) in `src/Njord/Domain/Analysis/IndexResult.cs`
- [x] 8.2 ~~Add tombstone logic~~ — skipped per "no preemptive legacy code" guideline; this is unreleased, no retained topics to clear yet
- [x] 8.3 Update `EgressEvent` / `EnrichmentUpdate` handling if the result type change requires adapter updates in `src/Njord/Egress/`

## 9. Validation

- [x] 9.1 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 9.2 Run `dotnet slopwatch` from repo root
- [x] 9.3 Run `dotnet format --verify-no-changes` from `src/`
- [x] 9.4 Build succeeds: `dotnet build Njord.slnx` from `src/`
