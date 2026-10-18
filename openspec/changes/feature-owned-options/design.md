## Context

Options of the five analysis features, their validators and the `*SettingsConversion` extensions sit in `Njord.Core/Configuration/<Feature>`. Core's remaining files that name feature options: `ConfigView`, `ConfigReloadPolicy`, `AnalysisSettingsFactory`, `ConfigSnapshotMappings`/`EffectiveConfigMappings`, `ConfigUpdateMappings`, and `ConfigWriter` (through the reload policy). Users of those types are the host (`ConfigManager`, setup containers), `Njord.Pipeline` (`AnalysisSettingsFactory` only) and tests. `Njord.Pipeline` already references the four feature libraries; the host references everything.

## Goals / Non-Goals

**Goals:** a feature is one folder of code plus one contracts project; Core is feature-free; no behaviour change.

**Non-Goals:** changing option shapes, keys or reload modes; moving `NjordOptions`, `PollingOptions` or other cross-cutting options out of Core.

## Decisions

1. **Orchestration to the host, not to a new project.** The host already owns `ConfigManager`, the only writer-side consumer, and references all features. A separate `Njord.Configuration` project would add a layer with a single consumer. Types stay `internal` to the host where possible; the specs for them move into `Njord.Tests`.
2. **`AnalysisSettingsFactory` to `Njord.Pipeline`.** The pipeline builds `AnalysisSettings` from the features' options; it is the one library allowed to reference all features. The host's `ConfigManager` calls it through the Pipeline project reference it already has.
3. **Consensus options.** Consensus is a shared base used by every feature, so `Njord.Consensus` stays free of options and Akka. `ConsensusOptions`, its validator and conversion therefore move to the host's configuration folder next to the orchestration types, not into the library; this is the one options type not owned by its subject, recorded in the spec's scenario.
4. **Indices preference resolution.** `PreferenceResolver`, `IndexPreferences` and `LocationIndexOverride` are index domain logic over options; they move with `IndexOptions` into `Njord.Indices`, and `EffectiveConfigMappings` reads them from there.
5. **Reload policy coverage by architecture spec.** `ConfigReloadPolicy` stays an explicit table (types and property overrides); a spec reflects over all options types (those with a `SectionName` constant) in the host and feature libraries and fails when one has no entry, replacing today's implicit reliance on Core owning every type.
6. **Order within the change:** (a) move the pure feature pieces (options, validator, conversion) per feature with their specs, (b) switch registration to the feature extensions, (c) move orchestration types to the host and `AnalysisSettingsFactory` to Pipeline, (d) remove Core's feature folders and references, (e) tighten the architecture specs.

## Risks / Trade-offs

- Tests that currently live in `Njord.Core.Tests` and reference feature options must relocate to avoid a Core.Tests to feature references; the shared `AppSettingsDefaults` helper keeps working because it binds by section path.
- The host grows by about ten files; accepted since it already composes the whole system and these types have no other consumer.
- `ConfigWriter` moving to the host removes its use from other libraries; none use it today (only the host and tests).

## Migration Plan

No operator impact. Each step leaves the suite green; the default-options golden master and the config gRPC golden masters are the proof of unchanged behaviour.
