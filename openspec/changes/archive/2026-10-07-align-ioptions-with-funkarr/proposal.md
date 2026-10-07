## Why

njord uses a single monolithic `NjordOptions` class with nested sub-options, registered once in `NjordServiceSetup`. FunkArr splits options per concern (10 standalone classes), each registered in its domain's setup container. This makes njord's options harder to test (every test must build the full `NjordOptions`), harder to validate (validators validate the root despite only caring about one section), and prevents granular `IOptionsMonitor<T>` subscriptions for runtime config changes. Additionally, 3 test files duplicate a `MutableOptionsMonitor` fake that FunkArr centralizes as `TestOptionsMonitor<T>`.

## What Changes

- **Extract sub-options**: Move `GrpcOptions`, `SensorOptions`, `PersistenceOptions` into standalone classes with their own `SectionName`, no longer nested on `NjordOptions`. `MqttOptions` and `EnrichmentOptions` get a `SectionName` and are *also* bindable standalone (feature libs already consume them that way), but **stay nested on `NjordOptions` too** — see Non-goals.
- **IOptionsMonitor for runtime changes**: Services that react to config changes use `IOptionsMonitor<SubOptions>` instead of `IOptions<NjordOptions>`.
- **Shared TestOptionsMonitor\<T\>**: Replace 3 duplicated `MutableOptionsMonitor` fakes with a single `TestOptionsMonitor<T>` in `Njord.Tests.Shared`.
- **Co-located validators**: Each sub-option owns its validation as `IValidateOptions<SubOptions>`, except `Mqtt` (see Non-goals). `NjordOptionsValidator` keeps only location/model/persistence/Mqtt validation.

## Non-goals

- Changing configuration file format — the JSON structure stays the same (`Njord:Mqtt:Host` etc.)
- Adding new configuration options
- Changing polling behavior (no API-budget impact)
- **Removing `Mqtt`/`Enrichment` from `NjordOptions`**: `AdminGrpcService` clones,
  mutates, and persists the whole `NjordOptions` object as one JSON blob
  (`IOptionsMonitor<NjordOptions>` + `ConfigPersistence.SaveAsync(NjordOptions)`)
  for every admin `Set*` RPC, including `SetEnrichment*`. Splitting these two
  out would mean rewriting that admin mutation/persistence pipeline to handle
  multiple cloned/persisted option objects instead of one — discovered during
  implementation to be materially larger and riskier than this proposal's
  scope, so it's intentionally left for a separate change if ever needed.

## Capabilities

### New Capabilities
- `standalone-sub-options`: Extraction of nested sub-options into standalone classes with per-concern registration and validation
- `test-options-monitor`: Shared `TestOptionsMonitor<T>` fake for options-dependent test specs

### Modified Capabilities
- `service-configuration`: Existing spec updated to reflect per-feature options registration instead of monolithic NjordServiceSetup

## Impact

- **Modified**: `src/Njord.Core/Configuration/NjordOptions.cs` — `Grpc` and `Sensors` removed (both were dead properties with zero usages); `Mqtt`/`Enrichment`/`Persistence` intentionally kept
- **Modified**: Each feature library's setup container (`*SetupContainer`, the per-domain successor to the former monolithic `NjordServiceSetup`) registers its own sub-options via `AddOptions<T>().Bind().ValidateOnStart()`
- **Modified**: Validators move from `NjordOptions`-level to sub-option-level, except Mqtt (stays on `NjordOptionsValidator`)
- **Modified**: Actors/services change from `IOptions<NjordOptions>` to `IOptions<SubOptions>` or `IOptionsMonitor<SubOptions>` where they don't need the admin-mutable whole
- **Added**: `src/Njord.Tests.Shared/TestOptionsMonitor.cs`
- **Modified**: 3 test files replace duplicated fakes with shared `TestOptionsMonitor<T>`
- **No API-budget impact**: no polling changes
