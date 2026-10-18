## Why

After `analysis-contracts-per-feature`, each analysis feature owns its contracts and logic, but its configuration still lives in `Njord.Core`: `Njord.Core/Configuration/{Alerts,Consensus,Derived,Indices,Trends}` hold the options, validators, preference resolver and options-to-settings conversions, and Core's `ConfigView`, `ConfigReloadPolicy`, `AnalysisSettingsFactory`, `ConfigSnapshotMappings` and `ConfigUpdateMappings` name every feature's options. Adding or changing a feature therefore touches Core, Compute-level settings and the feature. FunkArr keeps options next to the code that uses them; the feature libraries should own theirs.

## What Changes

- Options, validators, `*SettingsConversion` and (for indices) `IndexPreferences`, `LocationIndexOverride` and `PreferenceResolver` move from `Njord.Core/Configuration/<Feature>` into `Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices`; consensus options move to `Njord.Consensus` (or stay in Core if the consensus project must stay options-free, decided in design).
- Each feature registers its own options through its `ServiceCollectionExtensions` (`AddNjordOptions<T>` from `options-parity-with-funkarr`).
- The configuration orchestration that needs every feature's options moves out of Core into the host's `Configuration` folder next to `ConfigManager`: `ConfigView`, `ConfigReloadPolicy`, `EffectiveConfigMappings`, `ConfigUpdateMappings`, `ConfigWriter` and `IConfigWriter`; `AnalysisSettingsFactory` moves to `Njord.Pipeline`, which already references all four feature libraries and builds the analysis settings.
- `Njord.Core` keeps only options that are cross-cutting (`NjordOptions` scope, polling, OpenMeteo, storage, persistence, MQTT allow-list, sensors) and generic configuration infrastructure (`ConfigOverrideFile`, `CollectionDefaults`, `ConfigSections`, `AddNjordOptions`).
- Tests move with their types: feature options specs to the feature test projects, orchestration specs to `Njord.Tests`.

## Capabilities

### New Capabilities
<!-- covered by requirements added to analysis-feature-projects -->

### Modified Capabilities
- `analysis-feature-projects`: features also own their options, validators and settings conversion.

## Impact

- `Njord.Core/Configuration/*` (about 25 files move), `Njord/Configuration/*` (receives the orchestration), `Njord.Pipeline/Configuration/PipelineSetupContainer.cs`, feature `ServiceCollectionExtensions`, project references (host references features already), architecture specs (`ZoneArchitectureSpec`, `LayerReferenceSpec`, `ConfigReloadPolicy` coverage), tests and `AGENTS.md`.
- No configuration keys, wire formats, MQTT payloads or persisted data change; operators notice nothing.
- Not breaking; commit type `refactor:`. Applies after `analysis-contracts-per-feature` and `options-parity-with-funkarr`.
