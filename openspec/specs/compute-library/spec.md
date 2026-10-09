## Purpose

Pure mathematical computation library extracted from Njord.Core, holding all weather analysis types with zero Akka/DI dependencies.
## Requirements
### Requirement: Njord.Compute is a pure computation library
The solution SHALL contain a `Njord.Compute` class library that holds all weather analysis computation types (consensus, alerts, derived values, trends, indices, history, time-slice aggregation, preference resolution). The library SHALL reference only `Njord.Weather` and `Njord.Sensors` — no Akka, no DI, no ASP.NET dependencies.

#### Scenario: Compute references only Weather and Sensors
- **WHEN** `Njord.Compute.csproj` is inspected
- **THEN** its only `ProjectReference`s SHALL be `Njord.Weather` and `Njord.Sensors`

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

### Requirement: Repeated horizon-scan and threshold-classification shapes are shared
Alert computations in `AlertEvaluator` that scan a parameter's `ByHorizon` consensus data
up to a maximum-hours cutoff to find an extreme median value, the earliest qualifying
hour, and an agreement list SHALL use one shared helper for that scan instead of a
per-alert-type copy of the loop. Alert computations that classify an extreme value against
a 3-tier threshold list into an `AlertSeverity` SHALL use one shared classification helper
instead of a per-alert-type copy of the switch expression.

#### Scenario: New alert type reuses the shared scan and classification helpers
- **WHEN** a new alert type is added to `AlertEvaluator`
- **THEN** its implementation calls the shared horizon-scan helper and the shared
  threshold-classification helper, and does not introduce a new copy of either loop/switch
  shape

#### Scenario: Existing alert results are unchanged
- **WHEN** any existing alert type's evaluation method is migrated to use the shared
  helpers
- **THEN** its computed severity, confidence, and earliest-qualifying-hour for every
  existing `Njord.Compute.Tests` case remain identical to the pre-migration result

