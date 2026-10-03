## 1. Move actor messages out of Domain

- [x] 1.1 Create `src/Njord/Enrichment/ForecastHistoryMessages.cs` — moved `RecordSnapshot`, `QueryHistory`, `HistoryResponse`
- [x] 1.2 Remove actor messages and `ForecastRecorded` from `src/Njord/Domain/Analysis/ForecastHistory.cs`
- [x] 1.3 Update `src/Njord/Enrichment/ForecastHistoryActor.cs` — replaced `ForecastRecorded` with `ForecastRecord`, simplified OnRecover and Persist callback
- [x] 1.4 Update `src/Njord/Enrichment/EnrichmentActor.cs` — no changes needed (uses Njord.Enrichment already)
- [x] 1.5 Update `src/Njord.Tests/Enrichment/ForecastHistoryActorSpec.cs` — no changes needed (uses Njord.Enrichment already)
- [x] 1.6 Verify no remaining references to `ForecastRecorded` — confirmed zero references

## 2. Split DailyForecastPoint into typed dictionaries

- [x] 2.1 Modify `src/Njord/Domain/Weather/DailyForecastPoint.cs` — split into `NumericValues` (double?) and `MetaValues` (string?), added `GetNumeric` and `GetMeta`
- [x] 2.2 Modify `src/Njord/Ingest/OpenMeteoClient.cs` — daily parsing routes by `ParameterDef.ValueType`
- [x] 2.3 Update `src/Njord/Egress/HorizonProjection.cs` — uses `GetNumeric`/`GetMeta` based on ValueType
- [x] 2.4 Update `src/Njord/Domain/Weather/ForecastDataHash.cs` — iterates both `NumericValues` and `MetaValues`
- [x] 2.5 Write `src/Njord.Tests/Domain/Weather/DailyForecastPointSpec.cs` — 4 tests

## 3. Test updates

- [x] 3.1 Update `src/Njord.Tests/Mqtt/StatePayloadBuilderSpec.cs` — new DailyForecastPoint constructor
- [x] 3.2 Update `src/Njord.Tests/Egress/HorizonProjectionSpec.cs` — new DailyForecastPoint constructor
- [x] 3.3 No Verify snapshot changes needed

## 4. Validation

- [x] 4.1 Unit tests: 409 pass (4 new)
- [x] 4.2 Integration tests: 7 pass
- [x] 4.3 Build: 0 errors, 0 warnings
- [x] 4.4 Slopwatch: 2 pre-existing warnings only
