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

### Requirement: Compute receives settings records instead of options
`Njord.Compute` SHALL NOT contain options types or reference `Microsoft.Extensions.Options`. Computations SHALL receive immutable settings records (e.g. alert settings with typed severity thresholds, index settings with resolved preferences) that are built from validated options outside Compute. Options types (`AlertOptions`, `IndexOptions`, `IndexPreferences`, `LocationIndexOverride`, `ConsensusOptions`, `TrendOptions`, `DerivedOptions`) SHALL live in `Njord.Core` under `Configuration/`, each next to its validator.

#### Scenario: Compute has no options
- **WHEN** `Njord.Compute` is inspected
- **THEN** it contains no `*Options` type and no `Configuration/` folder

#### Scenario: Settings records are immutable
- **WHEN** any settings record consumed by Compute is inspected
- **THEN** it is a record with init-only or positional properties and no array indexing by position for thresholds

