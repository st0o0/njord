## ADDED Requirements

### Requirement: Features own their options
The options type, its validator and its options-to-settings conversion of an analysis feature SHALL be declared in that feature's library (`Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices`), and registered by that library's `ServiceCollectionExtensions`. `Njord.Core` SHALL NOT contain an options type of an analysis feature.

#### Scenario: Alert options location
- **WHEN** `AlertOptions` is located
- **THEN** it is declared in `Njord.Alerts` and `Njord.Core` has no `Alerts` configuration folder

#### Scenario: Feature registers its options
- **WHEN** `AddAlerts` is called
- **THEN** `AlertOptions` is bound to `Njord:Enrichment:Alerts`, validated at start, and nothing outside the feature registers it

### Requirement: Configuration orchestration lives in the host
`ConfigView`, `ConfigReloadPolicy`, `EffectiveConfigMappings`, `ConfigUpdateMappings`, `ConfigWriter` and `IConfigWriter` SHALL be declared in `Njord` (the host), because they name the options of every feature. `AnalysisSettingsFactory` SHALL be declared in `Njord.Pipeline`. `Njord.Core` SHALL reference no analysis feature library.

#### Scenario: Core stays feature-free
- **WHEN** `Njord.Core.csproj` and its types are inspected
- **THEN** no analysis feature library is referenced and no feature options type is named

#### Scenario: Reload policy still covers every options type
- **WHEN** the architecture spec lists all options types in the host and the feature libraries
- **THEN** each has a `ConfigReloadPolicy` entry

### Requirement: Behaviour is unchanged
Moving options SHALL NOT change configuration keys, defaults, validation messages, reload modes, gRPC config responses or persisted override files.

#### Scenario: Default options golden master
- **WHEN** the default-options golden master and the config gRPC golden masters are verified after the move
- **THEN** they are byte-identical
