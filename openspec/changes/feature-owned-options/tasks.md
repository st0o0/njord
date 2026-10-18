## 1. Guards first

- [x] 1.1 Add the reflection spec that every options type (with a `SectionName` constant) has a `ConfigReloadPolicy` entry and the architecture rule that Core declares no analysis feature options

## 2. Move feature options

- [x] 2.1 Alerts, Derived, Trends, Indices: options, validators, conversions (and index preferences) into the feature libraries
- [x] 2.2 Consensus options, validator and conversion into `Njord.Pipeline`
- [x] 2.3 Feature `Add*` extensions take `IConfiguration` and register their options; `AnalysisSetupContainer` passes it
- [x] 2.4 Move the matching specs into the feature test projects and `Njord.Pipeline.Tests`

## 3. Move orchestration

- [x] 3.1 Move `ConfigReloadPolicy`, `ConfigWriter`, `IConfigWriter`, `EffectiveConfigMappings`, `ConfigUpdateMappings`, `AnalysisUpdate` to the host; add `AnalysisConfigView`; slim `ConfigView` in Core
- [x] 3.2 Move `AnalysisSettingsFactory` to `Njord.Pipeline`; `ConfigManager` builds its starting settings from current values; remove the startup `AnalysisSettings` singleton
- [x] 3.3 Move orchestration specs and the default-options golden master to `Njord.Tests`; `Njord.Tests.Shared` references the host

## 4. Enforcement and docs

- [x] 4.1 Update architecture allow-lists; update `AGENTS.md` (solution structure, configuration section, test counts) and the `njord-enrichment-feature` skill
- [x] 4.2 `dotnet format whitespace`, build `Njord.slnx` with 0 warnings, run every test project, `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`
