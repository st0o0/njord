## Context

njord registers all configuration through a single `NjordOptions` class with nested sub-option types. FunkArr splits options per concern, each registered in its domain's setup container. This change aligns njord with the FunkArr pattern.

Current state: `NjordServiceSetup` registers `NjordOptions` with 5 validators, then calls `AddNjordPipeline()`, `AddNjordEnrichment()`, etc. These extension methods currently do NOT register their own options — they rely on `NjordOptions` being registered upstream.

## Goals / Non-Goals

**Goals:**
- Per-concern options classes with own `SectionName` and validator
- Feature libraries self-contained: `AddNjordMqtt()` registers `MqttOptions`
- `IOptionsMonitor<SubOptions>` where runtime config changes matter
- Shared `TestOptionsMonitor<T>` eliminating test fake duplication

**Non-Goals:**
- Changing the JSON config structure (sections stay the same)
- Changing `ConfigPersistence` behavior

## Decisions

### D1: Sub-options stay in Njord.Core, not in feature libraries

**Decision**: The sub-option classes (`MqttOptions`, `GrpcOptions`, etc.) remain in `Njord.Core/Configuration/` because they are referenced by both feature libraries and the host. Moving them to feature libraries would create cross-references.

**Why**: Architecture zone rules forbid feature-to-feature references. `MqttOptions` is consumed by `Njord.Mqtt` but also referenced by `NjordServiceSetup` in the host and `NjordOptions` validators.

### D2: Feature library AddNjord* methods receive IConfiguration

**Decision**: Each `AddNjord*()` extension method gains an `IConfiguration configuration` parameter so it can bind its own sub-options section.

**Alternative**: Feature libraries read from `IServiceProvider` at resolve time. Rejected because `AddOptions().Bind()` needs the configuration at registration time.

### D3: IOptionsMonitor only where config actually changes at runtime

**Decision**: Use `IOptionsMonitor<T>` only in services that react to `ConfigPersistence` writes:
- `MqttConnectionActor` → `IOptionsMonitor<MqttOptions>` (reconnects on host change)
- `AdminGrpcService` → `IOptionsMonitor<NjordOptions>` (serves current config)
- `OptionsBudgetProvider` → `IOptionsMonitor<NjordOptions>` (budget recalc)

All other consumers use `IOptions<T>` (read once at startup).

### D4: TestOptionsMonitor\<T\> follows FunkArr's pattern

**Decision**: `TestOptionsMonitor<T>` implements `IOptionsMonitor<T>` with:
- Constructor takes initial `T value`
- `CurrentValue` property returns the current value
- `Update(T newValue)` sets the value and fires all `OnChange` listeners
- `Get(string? name)` returns `CurrentValue`
- `OnChange(Action<T, string?>)` registers a listener, returns `IDisposable`

### D5: Validators stay as IValidateOptions registered in DI

**Decision**: Keep the `IValidateOptions<T>` pattern (not FluentValidation). Each sub-option class gets its own validator registered in the feature library's `AddNjord*()` method. `NjordOptionsValidator` shrinks to validate only cross-cutting concerns (locations non-empty, models non-empty, persistence path writable).

## Risks / Trade-offs

- **[Breaking constructor signatures]** → Actors change from `IOptions<NjordOptions>` to `IOptions<SubOptions>`. Test specs that construct actors directly must update. Mitigated by doing one feature library at a time.
- **[IConfiguration threading through AddNjord*()]** → Each extension method needs the configuration object. Mitigated by passing it from `NjordServiceSetup` which already has it.
- **[NjordOptions still needed for cross-cutting]** → Some services (like `BudgetCalculator`) need both `NjordOptions.Locations` and sub-options. They keep `IOptions<NjordOptions>` for the cross-cutting parts.

## Open Questions

- Should `AlertOptions`, `ConsensusOptions`, `DerivedOptions`, `TrendOptions`, `IndexOptions`, `HistoryOptions` (currently nested in `EnrichmentOptions`) become standalone too, or stay nested under `EnrichmentOptions`?
