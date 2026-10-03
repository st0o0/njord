## Why

The configuration layer has inconsistencies that cause confusion and a correctness issue: `EnrichmentOptions` is dual-bound (independently as `IOptions<EnrichmentOptions>` AND nested on `NjordOptions.Enrichment`), creating two separate instances from the same config section. Runtime mutations via AdminGrpcService update only the `NjordOptions` path — the independent `IOptions<EnrichmentOptions>` never sees changes. `SensorOptions` breaks the nesting pattern (not a property on `NjordOptions` unlike Mqtt/Grpc/Enrichment/Persistence). Multiple files contain 8+ types, naming is inconsistent, and options types carry business logic.

## What Changes

- **Remove dual-binding**: Delete `IOptions<EnrichmentOptions>` and `IOptions<SensorOptions>` registrations. All consumers switch to `IOptions<NjordOptions>` and access `.Value.Enrichment` / `.Value.Sensors`. Add `Sensors` property to `NjordOptions`.
- **Enrichment validators consolidated**: `ConsensusOptionsValidator`, `HistoryOptionsValidator`, `IndexOptionsValidator` become `IValidateOptions<NjordOptions>` (they already cross-reference `NjordOptions` for location names).
- **File-per-type**: Split `EnrichmentOptions.cs` (8 types → 8 files). Split `EnrichmentOptionsValidation.cs` (3 validators → 3 files). Extract `SensorOptionsValidator` from `SensorOptions.cs`.
- **Renames**: `AlertThresholdOptions` → `AlertOptions`. `BudgetValidator` → `BudgetCalculator`.
- **Move logic out of Options**: `NjordOptions.EffectiveBudget` → method on `BudgetCalculator`. `LocationOptions.ResolveModels()` → inline at call-site.
- **Consistency**: `PersistenceOptions` from record to class. Remove `PersistenceProvider` enum from `PersistenceOptions.cs` into own file.
- **Fix mutation in validation**: `IndexOptionsValidator` stops clamping sensitivities, emits a validation failure instead.

## Non-goals

- No behavioral changes to config binding or validation outcomes (except: IndexOptionsValidator now rejects instead of silently clamping).
- No API-budget impact — pure config-layer refactoring.
- No changes to ConfigPersistence serialization format.

## Capabilities

### New Capabilities

_(none — structural refactoring only)_

### Modified Capabilities

- `service-configuration`: Validators consolidated to `IValidateOptions<NjordOptions>`, `SensorOptions` becomes nested, `EnrichmentOptions` no longer independently registered.

## Impact

- **Configuration**: `NjordServiceSetup.cs` — remove 2 `IOptions<>` registrations, consolidate validators.
- **Enrichment**: All 6 enrichment consumers lose `IOptions<EnrichmentOptions>` constructor param, use `IOptions<NjordOptions>` instead.
- **Domain/Sensors**: `SensorHubActor` and any sensor consumer switches from `IOptions<SensorOptions>` to `IOptions<NjordOptions>`.
- **gRPC**: `AdminGrpcService` unchanged (already uses `NjordOptions` only).
- **Tests**: Constructor signatures change for enrichment features and validators.
