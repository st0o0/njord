## Context

The `Configuration/` directory has 12 files defining ~20 types. `EnrichmentOptions.cs` alone holds 8 types. Three enrichment validators live in one file. Options types carry computed properties (`EffectiveBudget`) and business logic (`ResolveModels`). `EnrichmentOptions` and `SensorOptions` are independently registered as `IOptions<>` despite being logically part of `NjordOptions`.

## Goals / Non-Goals

**Goals:**
- Single options root: `IOptions<NjordOptions>` is the only injected options type
- One type per file, consistent naming
- Options types are pure POCOs — no logic
- Validators only validate, never mutate

**Non-Goals:**
- No changes to config JSON structure or binding paths
- No `IOptionsMonitor` migration for enrichment features (they're singletons, snapshot-at-startup is correct)
- No new validation rules

## Decisions

### D1: Eliminate independent IOptions registrations

Remove `services.AddOptions<EnrichmentOptions>()` and `services.AddOptions<SensorOptions>()` from `NjordServiceSetup`. All consumers inject `IOptions<NjordOptions>` and access `.Value.Enrichment` or `.Value.Sensors`.

**Why**: The dual-binding creates two separate instances. AdminGrpcService mutates `NjordOptions.Enrichment` but enrichment features hold the other instance. Consolidating to one path eliminates the confusion.

### D2: Add SensorOptions as property on NjordOptions

Add `public SensorOptions Sensors { get; set; } = new();` to `NjordOptions`. Config path `Njord:Sensors` already binds correctly through the nested property.

### D3: Validators become IValidateOptions<NjordOptions>

All enrichment validators (`ConsensusOptionsValidator`, `HistoryOptionsValidator`, `IndexOptionsValidator`) and `SensorOptionsValidator` validate through `IValidateOptions<NjordOptions>`. They access `options.Enrichment.*` or `options.Sensors.*` directly — no need for separate `IOptions<>` injection. `IndexOptionsValidator` no longer needs to inject `IOptions<NjordOptions>` separately for cross-referencing locations — it already receives the full options.

### D4: IndexOptionsValidator stops mutating

Current behavior: silently clamps sensitivities to [0, 5]. New behavior: returns `ValidateOptionsResult.Fail($"... sensitivity {value} out of range [0, 5]")`. The `PreferenceResolver.ClampSensitivity` method already clamps at resolution time, so the validator's clamping was redundant anyway.

### D5: Logic extracted from Options types

- `NjordOptions.EffectiveBudget` → `BudgetCalculator.GetEffectiveBudget(NjordOptions)` (static method on the renamed class)
- `LocationOptions.ResolveModels(globalModels)` → inlined at the single call-site in `NjordOptionsValidator` and pipeline setup
- `PersistenceOptions` changed from record to class (consistency)

### D6: File-per-type splits

| Source | Types to extract |
|---|---|
| `EnrichmentOptions.cs` | `ConsensusOptions.cs`, `AlertOptions.cs` (renamed), `DerivedOptions.cs`, `TrendOptions.cs`, `IndexOptions.cs`, `IndexPreferences.cs`, `LocationIndexOverride.cs`, `HistoryOptions.cs` |
| `EnrichmentOptionsValidation.cs` | `ConsensusOptionsValidator.cs`, `HistoryOptionsValidator.cs`, `IndexOptionsValidator.cs` (delete source) |
| `SensorOptions.cs` | `SensorOptionsValidator.cs` (extract validator) |
| `PersistenceOptions.cs` | `PersistenceProvider.cs` (extract enum) |
| `BudgetValidator.cs` | Rename to `BudgetCalculator.cs`, rename class |

### D7: AlertThresholdOptions → AlertOptions

The "Threshold" qualifier is redundant — all alert config is threshold config. Rename class and all references. Config JSON key `Alerts` stays the same (property name on `EnrichmentOptions` is already `Alerts`).

## Risks / Trade-offs

- **Large constructor signature changes** → Every enrichment feature's constructor changes. Tests need updating. Mechanical but noisy.
- **IndexOptionsValidator behavior change** → Configs with sensitivities > 5 will now fail validation instead of being silently clamped. This is the correct behavior since `PreferenceResolver` already clamps at runtime, but users with existing out-of-range configs will see startup failures. Documented in release notes.
