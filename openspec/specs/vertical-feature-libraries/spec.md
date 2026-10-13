# vertical-feature-libraries Specification

## Purpose
TBD - created by archiving change restructure-enrichment-pipeline. Update Purpose after archive.
## Requirements
### Requirement: Each analysis feature is a self-contained library

Each analysis feature (Alerts, Derived, Trends, Indices) SHALL be an independent project (`Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices`) that references only `Njord.Core` and below. Feature libraries SHALL NOT reference each other, the Host, or any consumer library. Each library SHALL contain its computation logic (rules, scores, formulas) and its DI extension. Result records and settings records SHALL live in `Njord.Compute`, so consumers and messages can use them without referencing a feature library.

#### Scenario: Adding a new analysis feature
- **WHEN** a developer creates a new analysis feature
- **THEN** they create one new project with its compute logic and DI extension, and add its result and settings records to Compute; no other feature library changes

#### Scenario: Feature library references
- **WHEN** any feature library project is inspected
- **THEN** it references only `Njord.Core` (and transitively Weather, Sensors, Messages, Compute, Persistence), never another feature library, a consumer, or the Host

#### Scenario: Njord.Compute retains only consensus and shared types
- **WHEN** `Njord.Compute` is inspected after restructuring
- **THEN** it contains only consensus building and statistics, the query vocabulary, and shared result and settings records

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

