## ADDED Requirements

### Requirement: Features own their options
The options type, its validator and its options-to-settings conversion of an analysis feature SHALL be declared in that feature's library (`Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices`, namespace `Njord.<Feature>.Configuration`) and registered by that library's `Add<Feature>(IConfiguration)` extension. The consensus options SHALL be declared in `Njord.Pipeline`, which runs the consensus; `Njord.Consensus` SHALL contain no options. `Njord.Core` SHALL NOT contain an options type of an analysis feature or of consensus.

#### Scenario: Alert options location
- **WHEN** `AlertOptions` is located
- **THEN** it is declared in `Njord.Alerts` and `Njord.Core` has no `Alerts` configuration folder

#### Scenario: Feature registers its options
- **WHEN** `AddAlerts(configuration)` is called
- **THEN** `AlertOptions` is bound to `Njord:Enrichment:Alerts`, validated at start, and nothing outside the feature registers it

#### Scenario: Core stays feature-free
- **WHEN** the types of `Njord.Core` are inspected
- **THEN** none is named `AlertOptions`, `DerivedOptions`, `TrendOptions`, `IndexOptions` or `ConsensusOptions`

### Requirement: Configuration orchestration lives in the host
`ConfigReloadPolicy`, `ConfigWriter`, `IConfigWriter`, `EffectiveConfigMappings`, `ConfigUpdateMappings` and `AnalysisConfigView` SHALL be declared in the host (`Njord.Configuration`), because they name the options of every feature. `AnalysisSettingsFactory` SHALL be declared in `Njord.Pipeline`. `ConfigView` in `Njord.Core` SHALL expose only the forecast scope and the polling options. A reflection spec SHALL require a `ConfigReloadPolicy` entry for every production type with a `SectionName` constant.

#### Scenario: Reload policy covers every options type
- **WHEN** the architecture spec lists all production types with a `SectionName` constant
- **THEN** each has a `ConfigReloadPolicy` entry

#### Scenario: Core view stays small
- **WHEN** `ConfigView` is inspected
- **THEN** it exposes `Scope` and `Polling` and no analysis section

### Requirement: Config manager starts from the persisted configuration
`ConfigManager` SHALL compute its initial `AnalysisSettings` from the current values of the analysis option monitors and the running forecast scope, not from a singleton built at startup.

#### Scenario: Restarted manager reports current settings
- **WHEN** the analysis configuration was changed at runtime and `ConfigManager` restarts
- **THEN** `QueryAnalysisSettings` answers the changed settings

### Requirement: Behaviour is unchanged
Moving options SHALL NOT change configuration keys, defaults, validation messages, reload modes, gRPC config responses or persisted override files.

#### Scenario: Default options golden master
- **WHEN** the default-options golden master and the config gRPC golden masters are verified after the move
- **THEN** they are byte-identical
