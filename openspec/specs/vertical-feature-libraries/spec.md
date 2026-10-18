# vertical-feature-libraries Specification

## Purpose

Each analysis feature (alerts, derived values, trends, indices) is a self-contained library with its own analyzer, a DI extension method and clean type names, orchestrated by the `ForecastPipeline`.
## Requirements
### Requirement: Each feature provides a DI extension method

Each feature library SHALL expose an `IServiceCollection` extension method (e.g. `AddAlerts()`, `AddTrends()`) that registers its analyzer, all its rules or scores, and binds and validates its options. The Host calls these extensions; the feature owns its registrations.

#### Scenario: Registering alert analysis
- **WHEN** the Host calls `services.AddAlerts()`
- **THEN** the `AlertAnalyzer` and all 14 alert rules are registered and available for injection

#### Scenario: Feature options are library-owned
- **WHEN** a feature has configurable options (e.g. alert thresholds)
- **THEN** the feature's DI extension binds and validates them; the options type itself lives in `Njord.Core/Configuration/`

### Requirement: Feature types use clean names

Analysis types SHALL follow one naming scheme: `<Feature>Analyzer` for a feature's entry point, `<Name>Rule` for a single alert rule, `<Name>Score` for a single index score, `<Feature>Formulas` for pure static math, `Consensus<Role>` for consensus types. No analysis type name SHALL contain "Enrichment", "Computer", "Calculator", "Scorer", "Evaluator" or "Factory".

#### Scenario: No enrichment naming
- **WHEN** any type in a feature library is inspected
- **THEN** its name does not contain "Enrichment"

#### Scenario: No legacy naming
- **WHEN** any type in Compute or a feature library is inspected
- **THEN** its name contains none of "Enrichment", "Computer", "Calculator", "Scorer", "Evaluator", "Factory"

### Requirement: Analysis feature libraries are independent and hold computation only
Each analysis feature (Alerts, Derived, Trends, Indices) SHALL be an independent project (`Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices`) that references only `Njord.Core` and below. Feature libraries SHALL NOT reference each other, the Host, or any consumer library. Each library SHALL contain its computation logic (rules, scores, formulas) and its DI extension. Result records and settings records SHALL live in the feature's folder of `Njord.Messages`, so consumers and messages can use them without referencing a feature library.

#### Scenario: Adding a new analysis feature
- **WHEN** a developer creates a new analysis feature
- **THEN** they create one new project with its compute logic and DI extension plus one folder in `Njord.Messages` with its result and settings records; no other feature library changes

#### Scenario: Analysis library references
- **WHEN** any analysis library project is inspected
- **THEN** it references only `Njord.Core` (and transitively Weather, Sensors, Messages, Consensus, Persistence), never another feature library, a consumer, or the Host

#### Scenario: Njord.Consensus retains only consensus types
- **WHEN** `Njord.Consensus` is inspected after restructuring
- **THEN** it contains only consensus building and statistics, the consensus records and settings, and the query vocabulary; no feature result or settings records

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

