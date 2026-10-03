## 1. File-per-type: EnrichmentOptions.cs split

- [x] 1.1 Extract `ConsensusOptions` → `src/Njord/Configuration/ConsensusOptions.cs`
- [x] 1.2 Extract `AlertThresholdOptions` → `src/Njord/Configuration/AlertOptions.cs` (rename to `AlertOptions`)
- [x] 1.3 Extract `DerivedOptions` → `src/Njord/Configuration/DerivedOptions.cs`
- [x] 1.4 Extract `TrendOptions` → `src/Njord/Configuration/TrendOptions.cs`
- [x] 1.5 Extract `IndexOptions` → `src/Njord/Configuration/IndexOptions.cs`
- [x] 1.6 Extract `IndexPreferences` → `src/Njord/Configuration/IndexPreferences.cs`
- [x] 1.7 Extract `LocationIndexOverride` → `src/Njord/Configuration/LocationIndexOverride.cs`
- [x] 1.8 Extract `HistoryOptions` → `src/Njord/Configuration/HistoryOptions.cs`
- [x] 1.9 Update `EnrichmentOptions.cs` to only contain `EnrichmentOptions` class, update property type `AlertThresholdOptions` → `AlertOptions`

## 2. File-per-type: Validators and other splits

- [x] 2.1 Extract `ConsensusOptionsValidator` → `src/Njord/Configuration/ConsensusOptionsValidator.cs`
- [x] 2.2 Extract `HistoryOptionsValidator` → `src/Njord/Configuration/HistoryOptionsValidator.cs`
- [x] 2.3 Extract `IndexOptionsValidator` → `src/Njord/Configuration/IndexOptionsValidator.cs`, delete `EnrichmentOptionsValidation.cs`
- [x] 2.4 Extract `SensorOptionsValidator` from `SensorOptions.cs` → `src/Njord/Configuration/SensorOptionsValidator.cs`
- [x] 2.5 Extract `PersistenceProvider` enum from `PersistenceOptions.cs` → `src/Njord/Configuration/PersistenceProvider.cs`
- [x] 2.6 Change `PersistenceOptions` from `sealed record` to `sealed class`

## 3. Renames and logic extraction

- [x] 3.1 Rename `BudgetValidator` → `BudgetCalculator` in `src/Njord/Configuration/BudgetValidator.cs` → `BudgetCalculator.cs`, add `GetEffectiveBudget(NjordOptions)` method
- [x] 3.2 Remove `EffectiveBudget` computed property from `NjordOptions`, update all callers to use `BudgetCalculator.GetEffectiveBudget(options)`
- [x] 3.3 Remove `ResolveModels()` method from `LocationOptions`, inline the model-merge logic at call-sites
- [x] 3.4 Update all references from `AlertThresholdOptions` → `AlertOptions` across the codebase

## 4. Add SensorOptions to NjordOptions

- [x] 4.1 Add `public SensorOptions Sensors { get; set; } = new();` to `NjordOptions`

## 5. Remove dual-binding and consolidate validators

- [x] 5.1 Remove `services.AddOptions<EnrichmentOptions>()` registration from `NjordServiceSetup.cs`
- [x] 5.2 Remove `services.AddOptions<SensorOptions>()` registration from `NjordServiceSetup.cs`
- [x] 5.3 Change `ConsensusOptionsValidator`, `HistoryOptionsValidator`, `IndexOptionsValidator` from `IValidateOptions<EnrichmentOptions>` to `IValidateOptions<NjordOptions>` — access enrichment config via `options.Enrichment.*`
- [x] 5.4 Change `SensorOptionsValidator` from `IValidateOptions<SensorOptions>` to `IValidateOptions<NjordOptions>` — access sensor config via `options.Sensors.*`
- [x] 5.5 Remove `IOptions<NjordOptions>` injection from `IndexOptionsValidator` (it now receives `NjordOptions` directly)

## 6. Fix IndexOptionsValidator mutation

- [x] 6.1 Replace clamping logic in `IndexOptionsValidator` with validation failure for sensitivity values outside [0.0, 5.0]

## 7. Update enrichment consumers

- [x] 7.1 Update `EnrichmentActor` — replace `IOptions<EnrichmentOptions>` with `IOptions<NjordOptions>`, access `.Value.Enrichment`
- [x] 7.2 Update `AlertEnrichment` — replace `IOptions<EnrichmentOptions>` with `IOptions<NjordOptions>`
- [x] 7.3 Update `DerivedEnrichment` — remove `IOptions<EnrichmentOptions>` (already has `IOptions<NjordOptions>`)
- [x] 7.4 Update `TrendEnrichment` — replace `IOptions<EnrichmentOptions>` with `IOptions<NjordOptions>`
- [x] 7.5 Update `IndexEnrichment` — remove `IOptions<EnrichmentOptions>` (already has `IOptions<NjordOptions>`)
- [x] 7.6 Update `HistoryEnrichment` — remove `IOptions<EnrichmentOptions>` (already has `IOptions<NjordOptions>`)

## 8. Update sensor consumers

- [x] 8.1 Update `SensorHubActor` — replace `IOptions<SensorOptions>` with `IOptions<NjordOptions>`, access `.Value.Sensors`

## 9. Update tests

- [x] 9.1 Update enrichment feature tests — constructor signatures changed (no more `IOptions<EnrichmentOptions>`)
- [x] 9.2 Update enrichment actor tests — constructor signature changed
- [x] 9.3 Update validator tests — validators now receive `NjordOptions` instead of `EnrichmentOptions`/`SensorOptions`
- [x] 9.4 Update `SensorHubActor` tests — constructor signature changed

## 10. Validation

- [x] 10.1 Build succeeds: `dotnet build Njord.slnx` from `src/`
- [x] 10.2 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 10.3 Run `dotnet slopwatch` from repo root
- [x] 10.4 Run `dotnet format --verify-no-changes` from `src/`
