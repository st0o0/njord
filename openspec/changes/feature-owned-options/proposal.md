## Why

After `contracts-as-folders`, each analysis feature owns its contracts (in `Njord.Messages`) and its logic, but its configuration still lives in `Njord.Core`: `Njord.Core/Configuration/{Alerts,Consensus,Derived,Indices,Trends}` hold the options, validators, preference resolver and options-to-settings conversions, and Core's `ConfigView`, `ConfigReloadPolicy`, `AnalysisSettingsFactory`, `EffectiveConfigMappings`, `ConfigUpdateMappings` and `ConfigWriter` name every feature's options. Adding or changing a feature therefore touches Core and the feature. FunkArr keeps options next to the code that uses them; the feature libraries should own theirs. A second defect sits in the same code: `ConfigManager` is seeded with an `AnalysisSettings` singleton built once from `IOptions`, so a restarted `ConfigManager` would hand out the startup values instead of the persisted configuration.

## What Changes

- Options, validators, `*SettingsConversion` and (for indices) `IndexPreferences`, `LocationIndexOverride` and `PreferenceResolver` move from `Njord.Core/Configuration/<Feature>` into `Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices` (folder and namespace `Configuration`). Consensus options move to `Njord.Pipeline`, which runs the consensus; `Njord.Consensus` stays options-free.
- Each feature registers its own options: `AddAlerts`, `AddDerived`, `AddTrends`, `AddIndices` take an `IConfiguration` and call `AddNjordOptions<T>`.
- `AnalysisSettingsFactory` moves to `Njord.Pipeline`. The orchestration that names every feature's options moves to the host (`Njord.Configuration`): `ConfigReloadPolicy`, `ConfigWriter`, `IConfigWriter`, `EffectiveConfigMappings`, `ConfigUpdateMappings`, `AnalysisUpdate`, plus a new `AnalysisConfigView`.
- `ConfigView` in Core keeps only the forecast scope and the polling options (what `PollManager` and the health check read); the five analysis sections are read through the host's `AnalysisConfigView`.
- `ConfigManager` computes its starting analysis settings from the current option values; the startup `AnalysisSettings` singleton is removed.
- Tests move with their types: feature options specs to the feature test projects, orchestration specs and the default-options golden master to `Njord.Tests`; `Njord.Tests.Shared` references the host so shared harnesses can build the real writer and views.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `vertical-feature-libraries`: features also own their options, validators and settings conversion; Core is feature-free.
- `domain-setup-containers`: the pipeline container no longer registers a startup `AnalysisSettings`; the analysis container passes the configuration to the feature registrations.

## Impact

- About 25 files move out of `Njord.Core/Configuration`; `Njord/Configuration` receives the orchestration; feature `ServiceCollectionExtensions` change signature (`IConfiguration` parameter); test projects and `Njord.Tests.Shared` helpers (`TestConfigView`, new `TestAnalysisConfigView`, `ConfigHarness`) change.
- No configuration keys, wire formats, MQTT payloads or persisted data change; operators notice nothing. Commit type `refactor:`.
- Follows `contracts-as-folders` and `options-parity-with-funkarr`.
