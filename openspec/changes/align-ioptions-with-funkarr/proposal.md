## Why

njord uses a single monolithic `NjordOptions` class with nested sub-options, registered once in `NjordServiceSetup`. FunkArr splits options per concern (10 standalone classes), each registered in its domain's setup container. This makes njord's options harder to test (every test must build the full `NjordOptions`), harder to validate (validators validate the root despite only caring about one section), and prevents granular `IOptionsMonitor<T>` subscriptions for runtime config changes. Additionally, 3 test files duplicate a `MutableOptionsMonitor` fake that FunkArr centralizes as `TestOptionsMonitor<T>`.

## What Changes

- **Extract sub-options**: Move `MqttOptions`, `GrpcOptions`, `SensorOptions`, `EnrichmentOptions`, `PersistenceOptions` into standalone classes with their own `SectionName`. Each feature library's `AddNjord*()` method registers its own options. `NjordOptions` keeps only cross-cutting: Locations, Models, Horizons, PollIntervalMinutes, OpenMeteoBaseUrl, Parameters.
- **IOptionsMonitor for runtime changes**: Services that react to config changes use `IOptionsMonitor<SubOptions>` instead of `IOptions<NjordOptions>`.
- **Shared TestOptionsMonitor\<T\>**: Replace 3 duplicated `MutableOptionsMonitor` fakes with a single `TestOptionsMonitor<T>` in `Njord.Tests.Shared`.
- **Co-located validators**: Each sub-option owns its validation as `IValidateOptions<SubOptions>`. `NjordOptionsValidator` keeps only location/model/persistence validation.

## Non-goals

- Changing configuration file format — the JSON structure stays the same (`Njord:Mqtt:Host` etc.)
- Adding new configuration options
- Changing polling behavior (no API-budget impact)

## Capabilities

### New Capabilities
- `standalone-sub-options`: Extraction of nested sub-options into standalone classes with per-concern registration and validation
- `test-options-monitor`: Shared `TestOptionsMonitor<T>` fake for options-dependent test specs

### Modified Capabilities
- `service-configuration`: Existing spec updated to reflect per-feature options registration instead of monolithic NjordServiceSetup

## Impact

- **Modified**: `src/Njord.Core/Configuration/` — NjordOptions shrinks, sub-options become standalone
- **Modified**: Every feature library's `ServiceCollectionExtensions` — adds own `AddOptions<T>().Bind().ValidateOnStart()`
- **Modified**: `src/Njord/Configuration/NjordServiceSetup.cs` — delegates options registration to feature libs
- **Modified**: Validators move from `NjordOptions`-level to sub-option-level
- **Modified**: Actors/services change from `IOptions<NjordOptions>` to `IOptions<SubOptions>` or `IOptionsMonitor<SubOptions>`
- **Added**: `src/Njord.Tests.Shared/TestOptionsMonitor.cs`
- **Modified**: 3 test files replace duplicated fakes with shared `TestOptionsMonitor<T>`
- **No API-budget impact**: no polling changes
