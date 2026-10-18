## Why

Mapping code in njord is mostly extension methods in small static classes already, but the convention is applied unevenly: `AnalysisProtoMappings` (446 lines) and `ConfigProtoMappings` (325 lines) mix five or six domains and non-mapping helpers, two enum-to-enum conversions are private methods with silent fallbacks, `ConfigSnapshotMapping` and `ConfigManager` hold private mapping helpers inside an actor, and `CoreSetupContainer` builds options and health snapshots inline. The enum-conversion and MQTT-payload changes set the standard; this change brings the rest to it.

## What Changes

- Enum-to-enum conversions `MapAlertType` and `MapAlertSeverity` become `ToProto()` in `WireEnumMappings` with a throwing default (no wire change; all members are covered).
- `AnalysisProtoMappings` is split into one static class per domain (alert, index, trend, derived, consensus) plus a thin enrichment-event dispatcher.
- `ConfigProtoMappings` is split into request-to-command mappings and result-to-proto mappings; `Applied/Validated/Rejected/Rethrow` leave the mapping classes.
- `ConfigSnapshotMapping` becomes `ConfigSnapshotMappings` with `ToSection()`-style extensions on the options types; the options-to-message and analysis-update mapping inside `ConfigManager` moves into a static `ConfigUpdateMappings` class in `Njord.Core`.
- One `ToTimestamp(this DateTimeOffset)` extension replaces private and inline Timestamp conversions; names are unified to `ToProto` / `ToCommand` / `ToDomain` / `ToMessage`, no `Map*` methods.
- `ParametersOptions` gets one `Resolve()` extension used by the container and the options validator; the initial budget health snapshot comes from `PollingOptions.ToInitialBudgetSnapshot()`.
- `NjordHealthMonitor` takes its start time and initial snapshot in the constructor; budget gauge registration moves into `BudgetSetupContainer`.
- `CoreSetupContainer` is split into `CoreSetupContainer`, `ConfigSetupContainer` and `HealthSetupContainer`; the meter allow-list is derived from the `Telemetry` classes.
- `MappingConventionArchitectureSpec` enforces the rules.

## Capabilities

### New Capabilities
- `mapping-conventions`: where mappings live, how they are named, what they may contain, and how they are tested and enforced.

### Modified Capabilities
- `domain-setup-containers`: the `CoreSetupContainer` requirement is narrowed; `ConfigSetupContainer`, `HealthSetupContainer` and the budget gauge registration are added.

## Impact

- `Njord.Grpc/Mapping/*`, `Njord.Core/Configuration/ConfigSnapshotMapping.cs`, `Njord/Configuration/{CoreSetupContainer,ConfigManager}.cs`, `Njord.Core/Health/NjordHealthMonitor.cs`, `Njord.Budget` setup, `Njord.Architecture.Tests`, matching mapping specs.
- No wire, MQTT or persistence change; gRPC and MQTT golden masters stay byte-identical (the proof).
- Not a breaking change; commit type `refactor:`.
