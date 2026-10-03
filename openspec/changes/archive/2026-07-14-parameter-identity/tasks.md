## 1. Foundation: ParameterRegistry typed properties

- [x] 1.1 Add static `ParameterDef` properties to `src/Njord/Domain/Weather/ParameterRegistry.cs` — 20 typed properties initialised from `BuildAll()` in static constructor
- [x] 1.2 Write `src/Njord.Tests/Domain/Weather/ParameterRegistryTypedAccessSpec.cs` — 3 tests

## 2. Foundation: ResolvedParameterSet lookup API

- [x] 2.1 Add `Get(ParameterDef)` and `Contains(ParameterDef)` to `ResolvedParameterSet` — `HashSet<ParameterDef>` per granularity
- [x] 2.2 Write `src/Njord.Tests/Domain/Weather/ResolvedParameterSetLookupSpec.cs` — 4 tests

## 3. Foundation: ForecastSeries querying API

- [x] 3.1 Add `Window`, `Mean`, `Values` to `src/Njord/Domain/Weather/ForecastSeries.cs`
- [ ] 3.2 Add `Window(DateOnly from, DateOnly to)` to `src/Njord/Domain/Weather/DailyForecastSeries.cs` — deferred to domain-boundaries change (DailyForecastPoint refactor)
- [x] 3.3 Write `src/Njord.Tests/Domain/Weather/ForecastSeriesQuerySpec.cs` — 6 tests

## 4. Migrate Compute methods to typed access

- [x] 4.1 Migrate `src/Njord/Domain/Analysis/AlertEvaluator.cs` — 12 of 13 fields now use typed properties (PrecipSum stays as GetByApiName, it's a daily param)
- [x] 4.2 Migrate `src/Njord/Domain/Analysis/IndexResult.cs` — 9 `FirstOrDefault` → `parameters.Get(ParameterRegistry.*)`. Mean24h deletion deferred (needs ForecastSeries.Mean integration into the existing loop pattern)
- [x] 4.3 Migrate `src/Njord/Domain/Analysis/EnergyResult.cs` — 7 `FirstOrDefault` → typed access. Mean24h deletion deferred
- [x] 4.4 Migrate `src/Njord/Domain/Analysis/DerivedResult.cs` — 8 `FirstOrDefault` → typed access
- [x] 4.5 Migrate `src/Njord/Domain/Analysis/TrendResult.cs` — `string[]` → `ParameterDef[]`, `Dictionary<string, double>` → `Dictionary<ParameterDef, double>`, 3 `FirstOrDefault` → typed access
- [x] 4.6 Migrate `src/Njord/Domain/Analysis/ConsensusResult.cs` — no string lookups to migrate (uses parameter iteration, not string lookup)
- [x] 4.7 Migrate `src/Njord/Domain/Analysis/HistoryResult.cs` — string constant → `ParameterRegistry.Temperature2m.ApiName`, 2 `FirstOrDefault` → typed access

## 5. Migrate HistoryAnalyzer to ParameterDef keys

- [ ] 5.1–5.5 Deferred to domain-boundaries change — HistoryAnalyzer/ForecastHistory string-keyed API touches the same files as the actor message move and persistence event changes

## 6. Test updates for migrated Compute methods

- [x] 6.1–6.7 All existing tests pass without modification — the typed access returns the same ParameterDef instances

## 7. Validation

- [x] 7.1 Unit tests: 405 pass (13 new)
- [x] 7.2 Integration tests: 7 pass
- [x] 7.3 Build: 0 errors, 0 warnings
- [x] 7.4 Slopwatch: 2 pre-existing warnings only
