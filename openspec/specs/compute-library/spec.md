## Purpose

Pure mathematical computation library extracted from Njord.Core, holding all weather analysis types with zero Akka/DI dependencies.

## Requirements

### Requirement: Njord.Compute is a pure computation library
The solution SHALL contain a `Njord.Compute` class library that holds all weather analysis computation types (consensus, alerts, derived values, trends, indices, history, time-slice aggregation, preference resolution). The library SHALL reference only `Njord.Domain` — no Akka, no DI, no ASP.NET dependencies.

#### Scenario: Compute references only Domain
- **WHEN** `Njord.Compute.csproj` is inspected
- **THEN** its only `ProjectReference` SHALL be `Njord.Domain`

#### Scenario: Compute contains no Akka types
- **WHEN** `Njord.Compute` is built
- **THEN** it SHALL NOT reference any `Akka.*` package

#### Scenario: Analysis namespace preserved
- **WHEN** types are moved from `Njord.Core/Analysis/` to `Njord.Compute`
- **THEN** they SHALL retain the `Njord.Analysis` namespace

### Requirement: Compute-parameter Options live in Compute
Options records that define computation parameters (`AlertOptions`, `HistoryOptions`, `IndexOptions`, `IndexPreferences`, `LocationIndexOverride`) SHALL reside in `Njord.Compute` under the `Njord.Configuration` namespace. These are pure POCOs with no DI or validation logic.

#### Scenario: Options moved to Compute
- **WHEN** `Njord.Compute` is inspected
- **THEN** `AlertOptions`, `HistoryOptions`, `IndexOptions`, `IndexPreferences`, and `LocationIndexOverride` SHALL exist in the project

#### Scenario: Options no longer in Core
- **WHEN** `Njord.Core` is inspected
- **THEN** `AlertOptions`, `HistoryOptions`, `IndexOptions`, `IndexPreferences`, and `LocationIndexOverride` SHALL NOT exist in the project

### Requirement: Core references Compute for validation
`Njord.Core` SHALL hold a `ProjectReference` to `Njord.Compute` so that options validators (`AlertOptionsValidator`, `HistoryOptionsValidator`, `IndexOptionsValidator`) can reference the options types. The validators SHALL remain in Core.

#### Scenario: Core gains Compute reference
- **WHEN** `Njord.Core.csproj` is inspected
- **THEN** it SHALL include a `ProjectReference` to `Njord.Compute`

#### Scenario: Validators stay in Core
- **WHEN** `Njord.Core` is inspected
- **THEN** `AlertOptionsValidator`, `HistoryOptionsValidator`, and `IndexOptionsValidator` SHALL exist in the project

### Requirement: Njord.Compute.Tests is pure xUnit
The solution SHALL contain a `Njord.Compute.Tests` executable test project with all Analysis test specs moved from `Njord.Core.Tests/Analysis/`. The project SHALL NOT reference Akka.Hosting.TestKit or any Akka package.

#### Scenario: Compute tests run without Akka
- **WHEN** `dotnet run --project Njord.Compute.Tests/Njord.Compute.Tests.csproj` is executed
- **THEN** all tests SHALL pass without any Akka actor system

#### Scenario: All Analysis specs moved
- **WHEN** `Njord.Core.Tests` is inspected
- **THEN** the `Analysis/` directory SHALL NOT exist
