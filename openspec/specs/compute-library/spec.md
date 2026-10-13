## Purpose

Pure mathematical computation library extracted from Njord.Core, holding all weather analysis types with zero Akka/DI dependencies.
## Requirements
### Requirement: Core references Compute for validation
`Njord.Core` SHALL hold a `ProjectReference` to `Njord.Compute` so that options-to-settings conversion can produce Compute's settings records. Each options validator SHALL live in `Njord.Core/Configuration/` next to the options type it validates.

#### Scenario: Core gains Compute reference
- **WHEN** `Njord.Core.csproj` is inspected
- **THEN** it SHALL include a `ProjectReference` to `Njord.Compute`

#### Scenario: Validators stay in Core
- **WHEN** `Njord.Core/Configuration/` is inspected
- **THEN** every `*Options` type has exactly one `*OptionsValidator` in the same folder

### Requirement: Njord.Compute.Tests is pure xUnit
The solution SHALL contain a `Njord.Compute.Tests` executable test project with all Analysis test specs moved from `Njord.Core.Tests/Analysis/`. The project SHALL NOT reference Akka.Hosting.TestKit or any Akka package.

#### Scenario: Compute tests run without Akka
- **WHEN** `dotnet run --project Njord.Compute.Tests/Njord.Compute.Tests.csproj` is executed
- **THEN** all tests SHALL pass without any Akka actor system

#### Scenario: All Analysis specs moved
- **WHEN** `Njord.Core.Tests` is inspected
- **THEN** the `Analysis/` directory SHALL NOT exist

### Requirement: Repeated horizon-scan and threshold-classification shapes are shared
Lookups that more than one feature performs on consensus data SHALL be provided once by Compute's query vocabulary: finding a parameter's series, restricting a series to a horizon window, finding the extreme or first qualifying value with its horizon and agreement, averaging agreement over a window or above a threshold, and counting contributing models at a horizon or over a window. Classification of a value against severity thresholds SHALL be provided once by the severity thresholds type. Feature code SHALL NOT re-implement these loops.

#### Scenario: New alert type reuses the shared scan and classification helpers
- **WHEN** a new alert rule is added
- **THEN** it expresses its scan with the query vocabulary and its classification with the severity thresholds, and does not introduce a new copy of either loop

#### Scenario: Existing alert results are unchanged
- **WHEN** the alert, index, derived and trend computations are migrated to the query vocabulary
- **THEN** the MQTT and gRPC golden masters remain identical without re-approval

### Requirement: Compute receives settings records instead of options
`Njord.Compute` SHALL NOT contain options types or reference `Microsoft.Extensions.Options`. Computations SHALL receive immutable settings records (e.g. alert settings with typed severity thresholds, index settings with resolved preferences) that are built from validated options outside Compute. Options types (`AlertOptions`, `IndexOptions`, `IndexPreferences`, `LocationIndexOverride`, `ConsensusOptions`, `TrendOptions`, `DerivedOptions`) SHALL live in `Njord.Core` under `Configuration/`, each next to its validator.

#### Scenario: Compute has no options
- **WHEN** `Njord.Compute` is inspected
- **THEN** it contains no `*Options` type and no `Configuration/` folder

#### Scenario: Settings records are immutable
- **WHEN** any settings record consumed by Compute is inspected
- **THEN** it is a record with init-only or positional properties and no array indexing by position for thresholds

### Requirement: Njord.Compute holds only shared analysis building blocks
The solution SHALL contain a `Njord.Compute` class library that holds what all analysis features share: consensus building and statistics, the query vocabulary over consensus data, and the shared analysis contracts (result records, settings records, severity thresholds). Feature-specific computation (alert rules, index scores, derived and trend formulas) SHALL NOT live in Compute. The library SHALL reference only `Njord.Weather` and `Njord.Sensors`: no Akka, no DI, no ASP.NET, no options dependencies, and no `InternalsVisibleTo` to any feature library.

#### Scenario: Compute references only Weather and Sensors
- **WHEN** `Njord.Compute.csproj` is inspected
- **THEN** its only `ProjectReference`s SHALL be `Njord.Weather` and `Njord.Sensors`

#### Scenario: Compute contains no Akka types
- **WHEN** `Njord.Compute` is built
- **THEN** it SHALL NOT reference any `Akka.*` package

#### Scenario: No feature logic in Compute
- **WHEN** `Njord.Compute` is inspected
- **THEN** it contains no alert rule, index score, derived formula or trend formula

#### Scenario: No friend assemblies
- **WHEN** `Njord.Compute.csproj` is inspected
- **THEN** it contains no `InternalsVisibleTo` entry other than its own test project

