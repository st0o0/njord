## Context

The previous changes (`typed-wire-enums`, `typed-mqtt-payloads`) established explicit switch-expression enum conversions and per-domain mapper classes in `Njord.Mqtt`. FunkArr uses the same style: `internal static` extension mappers, one class per domain, one mapper test per mapper. The remaining deviations are listed in the proposal.

## Goals / Non-Goals

**Goals:** one convention for all mappings, no mapping code in actors or containers, no wire change.

**Non-Goals:** new mapping libraries, changing protos, changing MQTT payloads, moving mappers between projects other than out of actors.

## Decisions

1. **Split by domain, keep names `<Domain>ProtoMappings`.** `AlertProtoMappings`, `IndexProtoMappings`, `TrendProtoMappings`, `DerivedProtoMappings`, `ConsensusProtoMappings`, plus `EnrichmentEventMappings` for `ToEnrichmentEvent` and `LatestAnalysisResponse`. Pure move: public signatures stay, call sites keep working through the extension syntax. Config: `ConfigRequestMappings` (`ToCommand`) and `ConfigResponseMappings` (`ToProto`); the response builders become `ConfigResponses` helper methods that are not part of the mapping rule.
2. **Alert enums throw.** `MapAlertType` and `MapAlertSeverity` fall back to `Unspecified`/`None` today. All members are mapped, so moving them to `WireEnumMappings` with a throwing default changes behaviour only for undefined values, matching the existing enum convention.
3. **Config update mapping goes to `Njord.Core`.** `ConfigUpdateMappings` sits beside `ConfigSnapshotMappings` (renamed from `ConfigSnapshotMapping`). Both are `internal` to Core with `InternalsVisibleTo` for the host and test projects where needed; if that is not available, they stay `public static` in Core as today.
4. **Options helpers are extensions on the options types**, in the options' project folder: `ParametersOptions.Resolve()` replaces `CoreSetupContainer.ResolveParameters` and the call inside `NjordOptionsValidator.TryResolveParameters` (the validator keeps its failure handling around the call).
5. **Health monitor gets a constructor** `NjordHealthMonitor(DateTimeOffset startedUtc, BudgetHealthSnapshot initialBudget)`; the public `init` property goes away. The gauge registration (`BudgetTelemetry.Observe`) moves to `BudgetSetupContainer`, which resolves the monitor; the host no longer references `BudgetTelemetry`.
6. **Container split follows the existing feature containers:** `CoreSetupContainer` (TimeProvider, meter adapter), `ConfigSetupContainer` (options, writer, `ConfigManager`), `HealthSetupContainer` (monitor, health check). Registration order in `Program.cs` stays Core, Config, Health, then the feature containers.
7. **Meter names come from the `Telemetry` classes**, each exposing `public const string MeterName`; the host builds the allow-list from these constants. This keeps the list a compile-time reference instead of a free-text set.
8. **Enforcement:** `MappingConventionArchitectureSpec` (reflection/ArchUnit): `*Mappings` types are `internal static` (or `public static` where the project already requires it, documented in the allow-list), have only static members, and no production type outside `*Mappings` declares a private static method named `Map*` that returns a proto or contract type.

## Risks / Trade-offs

- Pure moves can silently change overload resolution when several `ToProto` overloads exist; the golden masters and the per-mapping specs catch this.
- Splitting adds about ten small files; accepted for the one-topic-per-file rule used elsewhere.
- Moving the gauge registration changes when the observers are registered (container build instead of first monitor resolve); a spec reads the gauge values after updating the monitor.

## Migration Plan

No external migration. Implement in the order: enum mappings, splits, Core mapping move, options helpers, health/containers, architecture spec. Each step ends with the affected test projects green and unchanged golden masters.
