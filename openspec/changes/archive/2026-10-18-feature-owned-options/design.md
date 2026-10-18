## Context

Options of the five analysis features, their validators and the `*SettingsConversion` extensions sit in `Njord.Core/Configuration/<Feature>`. Core files that name feature options: `ConfigView`, `ConfigReloadPolicy`, `AnalysisSettingsFactory`, `EffectiveConfigMappings`, `ConfigUpdateMappings` and `ConfigWriter` (through the reload policy). `Njord.Pipeline` references the four feature libraries and `PollManager` reads `ConfigView`; the host references everything and `Njord.Grpc.Tests` already references the host.

## Goals / Non-Goals

**Goals:** a feature is its logic, its options and its contracts folder; Core is feature-free; no behaviour change; fix the stale startup settings.
**Non-Goals:** changing option shapes, keys or reload modes; moving cross-cutting options (`NjordOptions`, polling, OpenMeteo, storage, persistence, MQTT, sensors) out of Core.

## Decisions

1. **Orchestration to the host, not to a new project.** The host owns `ConfigManager`, the only writer-side consumer, and references all features. A separate configuration project would add a layer with a single consumer.
2. **`ConfigView` is split.** `PollManager` (Pipeline) needs the scope and polling options and cannot reference the host, so `ConfigView` stays in Core with exactly those two. The analysis sections move to the host's `AnalysisConfigView`, consumed by `EffectiveConfigMappings.ToEffectiveConfig(view, analysis)`.
3. **`AnalysisSettingsFactory` and consensus options to `Njord.Pipeline`.** Pipeline is the one library allowed to reference all features and it runs the consensus (`ConsensusBuilder`). `ConsensusOptions`, its validator and conversion therefore live there; `Njord.Consensus` stays free of options and Akka.
4. **Indices preference resolution** (`PreferenceResolver`, `IndexPreferences`, `LocationIndexOverride`) is index domain logic over options and moves with `IndexOptions`.
5. **Features register their own options.** `AddAlerts(services, configuration)` and its siblings call `AddNjordOptions<T>`; the host's `AnalysisSetupContainer` only passes the configuration. The conventional validator lookup (`<T>Validator` in the namespace of `T`) works unchanged in the feature assemblies.
6. **Reload policy coverage by architecture spec.** `ConfigReloadPolicy` stays an explicit table in the host; a spec reflects over all production types with a `SectionName` constant and fails when one has no entry. A second spec asserts Core declares none of the five feature options types.
7. **Startup staleness fix.** `ConfigManager` builds its initial `AnalysisSettings` from the monitors' current values and the running scope. The `AnalysisSettings` singleton in `PipelineSetupContainer` is removed, so a restarted `ConfigManager` reports the persisted configuration.
8. **Tests.** `Njord.Tests.Shared` references the host so `ConfigHarness` (real `ConfigWriter`) and `TestAnalysisConfigView` can be shared by `Njord.Tests` and `Njord.Grpc.Tests`. The default-options golden master moves to `Njord.Tests`, which gains `Verify.XunitV3`.

## Risks / Trade-offs

- The host grows by about ten files; accepted since it already composes the whole system and these types have no other consumer.
- `Njord.Tests.Shared` now depends on the host project; both consumers of the harness already did.
- `ConfigView` loses five properties; their only reader was the effective-config mapping.

## Migration Plan

No operator impact. The default-options and config gRPC golden masters prove unchanged behaviour; the suite stays green after the move.
