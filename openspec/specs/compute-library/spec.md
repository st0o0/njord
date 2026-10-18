## Purpose

Pure mathematical computation library extracted from Njord.Core, holding all weather analysis types with zero Akka/DI dependencies.
## Requirements
### Requirement: Repeated horizon-scan and threshold-classification shapes are shared
Lookups that more than one feature performs on consensus data SHALL be provided once by the query vocabulary in `Njord.Consensus`: finding a parameter's series, restricting a series to a horizon window, finding the extreme or first qualifying value with its horizon and agreement, averaging agreement over a window or above a threshold, and counting contributing models at a horizon or over a window. Classification of a value against severity thresholds SHALL be provided once by the severity thresholds type. Feature code SHALL NOT re-implement these loops.

#### Scenario: New alert type reuses the shared scan and classification helpers
- **WHEN** a new alert rule is added
- **THEN** it expresses its scan with the query vocabulary and its classification with the severity thresholds, and does not introduce a new copy of either loop

#### Scenario: Existing alert results are unchanged
- **WHEN** the alert, index, derived and trend computations are migrated to the query vocabulary
- **THEN** the MQTT and gRPC golden masters remain identical without re-approval

### Requirement: Core references the consensus project
`Njord.Core` SHALL hold a `ProjectReference` to `Njord.Consensus` so that options-to-settings conversion can produce the consensus settings; the feature settings records come from `Njord.Messages`. Each options validator SHALL live in `Njord.Core/Configuration/` next to the options type it validates.

#### Scenario: Core references consensus
- **WHEN** `Njord.Core.csproj` is inspected
- **THEN** it SHALL include a `ProjectReference` to `Njord.Consensus` and none to `Njord.Compute`

#### Scenario: Validators stay in Core
- **WHEN** `Njord.Core/Configuration/` is inspected
- **THEN** every `*Options` type has exactly one `*OptionsValidator` in the same folder

### Requirement: Njord.Consensus.Tests is pure xUnit
The solution SHALL contain a `Njord.Consensus.Tests` executable test project with the consensus builder, statistics and query vocabulary specs. The project SHALL NOT reference Akka.Hosting.TestKit or any Akka package. Specs for the analysis contract types SHALL live in `Njord.Messages.Tests`.

#### Scenario: Consensus tests run without Akka
- **WHEN** `dotnet run --project Njord.Consensus.Tests/Njord.Consensus.Tests.csproj` is executed
- **THEN** all tests SHALL pass without any Akka actor system

#### Scenario: Compute tests are gone
- **WHEN** the solution is inspected
- **THEN** there is no `Njord.Compute.Tests` project

### Requirement: Consensus and contracts receive settings and carry no options
`Njord.Consensus` and the analysis contract folders of `Njord.Messages` SHALL NOT contain options types or reference `Microsoft.Extensions.Options`. Computations SHALL receive immutable settings records (e.g. alert settings with typed severity thresholds, index settings with resolved preferences) that are built from validated options outside these projects. Options types (`AlertOptions`, `IndexOptions`, `IndexPreferences`, `LocationIndexOverride`, `ConsensusOptions`, `TrendOptions`, `DerivedOptions`) SHALL live in `Njord.Core` under `Configuration/`, each next to its validator.

#### Scenario: Consensus and contracts have no options
- **WHEN** `Njord.Consensus` or `Njord.Messages` is inspected
- **THEN** it contains no `*Options` type and does not reference the options package

#### Scenario: Settings records are immutable
- **WHEN** any settings record consumed by an analyzer is inspected
- **THEN** it is a record with init-only or positional properties and no array indexing by position for thresholds

### Requirement: Njord.Consensus holds only shared consensus building blocks
The solution SHALL contain a `Njord.Consensus` class library that holds what all analysis features share: consensus building and statistics, the consensus records, `ConsensusSettings` and `ConsensusMethod`, and the query vocabulary over consensus data (`ConsensusQueries`, `HorizonWindow`, `HorizonHit`, `ScanDirection`). Feature-specific computation (alert rules, index scores, derived and trend formulas) and feature result or settings records SHALL NOT live in Consensus. The library SHALL reference only `Njord.Weather` and `Njord.Sensors`: no Akka, no DI, no ASP.NET, no options dependencies, and no `InternalsVisibleTo` to any feature library.

#### Scenario: Consensus references only Weather and Sensors
- **WHEN** `Njord.Consensus.csproj` is inspected
- **THEN** its only `ProjectReference`s SHALL be `Njord.Weather` and optionally `Njord.Sensors`

#### Scenario: Consensus contains no Akka types
- **WHEN** `Njord.Consensus` is built
- **THEN** it SHALL NOT reference any `Akka.*` package

#### Scenario: No feature logic in Consensus
- **WHEN** `Njord.Consensus` is inspected
- **THEN** it contains no alert rule, index score, derived formula, trend formula or feature result record

#### Scenario: No friend assemblies
- **WHEN** `Njord.Consensus.csproj` is inspected
- **THEN** it contains no `InternalsVisibleTo` entry other than its own test project

