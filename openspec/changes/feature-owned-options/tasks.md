## 1. Guards first

- [ ] 1.1 Add the reflection spec that every options type (with a `SectionName` constant) has a `ConfigReloadPolicy` entry, and the architecture rule that Core names no feature options (failing first)

## 2. Move feature options (suite and golden masters green after each feature)

- [ ] 2.1 Derived: `DerivedOptions`, validator, conversion into `Njord.Derived`; register in its `ServiceCollectionExtensions`
- [ ] 2.2 Trends: same for `Njord.Trends`
- [ ] 2.3 Alerts: same for `Njord.Alerts` (options, validator, `AlertSettingsConversion`)
- [ ] 2.4 Indices: options, validator, conversion, `IndexPreferences`, `LocationIndexOverride`, `PreferenceResolver` into `Njord.Indices`
- [ ] 2.5 Consensus: options, validator, conversion into the host configuration folder (consensus library stays options-free)
- [ ] 2.6 Move the matching specs into the feature test projects or `Njord.Tests`

## 3. Move orchestration

- [ ] 3.1 Move `ConfigView`, `ConfigReloadPolicy`, `EffectiveConfigMappings`, `ConfigUpdateMappings`, `ConfigWriter`, `IConfigWriter` to the host with their specs
- [ ] 3.2 Move `AnalysisSettingsFactory` to `Njord.Pipeline`; update `PipelineSetupContainer` and `ConfigManager`
- [ ] 3.3 Remove the empty feature folders and any feature references from `Njord.Core`

## 4. Enforcement and docs

- [ ] 4.1 Make the new architecture rules pass; update `AGENTS.md` (solution structure, configuration section, test counts) and `docs/development`
- [ ] 4.2 Run `dotnet format whitespace`, build `Njord.slnx`, run every test project, `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`
