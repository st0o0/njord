# vertical-feature-libraries Specification

## Purpose
TBD - created by archiving change restructure-enrichment-pipeline. Update Purpose after archive.
## Requirements
### Requirement: Each analysis feature is a self-contained library

Each analysis feature (Alerts, Derived, Trends, Indices, History) SHALL be an
independent project (`Njord.Alerts`, `Njord.Derived`, `Njord.Trends`,
`Njord.Indices`, `Njord.History`) that references only `Njord.Core` and below.
Feature libraries SHALL NOT reference each other, the Host, or any consumer
library. Each library SHALL contain its computation logic (moved from
`Njord.Compute/Analysis/`), its result types, and its options.

#### Scenario: Adding a new analysis feature
- **WHEN** a developer creates a new analysis feature
- **THEN** they create a single new project with its input interface, compute
  logic, result types, and DI extension — no changes to other feature libraries
  are required

#### Scenario: Feature library references
- **WHEN** any feature library project is inspected
- **THEN** it references only `Njord.Core` (and transitively Weather, Sensors,
  Messages, Compute, Persistence) — never another feature library, a consumer,
  or the Host

#### Scenario: Njord.Compute retains only consensus and shared types
- **WHEN** `Njord.Compute` is inspected after restructuring
- **THEN** it contains only `ConsensusComputer`, `ConsensusResult`,
  `ConsensusSnapshot`, `ConsensusSnapshotFactory`, and shared utility types —
  no feature-specific computation

### Requirement: Each feature defines its own typed input interface

Each feature library SHALL define a public input interface (e.g. `IAlertInput`,
`ITrendInput`) declaring exactly the data it requires. The interface SHALL be
defined in the feature library, not in Core or a shared contracts project.

#### Scenario: Alert feature input
- **WHEN** the alert analyzer is invoked
- **THEN** it receives an `IAlertInput` providing `ConsensusSnapshot` — nothing
  else

#### Scenario: Index feature input
- **WHEN** the index computer is invoked
- **THEN** it receives an `IIndexInput` providing `ConsensusSnapshot` and
  `SensorSnapshot?` — no trend or history data

#### Scenario: Trend feature input
- **WHEN** the trend analyzer is invoked
- **THEN** it receives an `ITrendInput` providing `ConsensusSnapshot` (current)
  and `ConsensusSnapshot?` (previous) — no sensor or history data

#### Scenario: History feature input
- **WHEN** the history tracker records a snapshot
- **THEN** it receives an `IHistoryInput` providing `ModelSnapshot` (raw
  per-model data, not consensus)

### Requirement: Each feature provides a DI extension method

Each feature library SHALL expose an `IServiceCollection` extension method
(e.g. `AddAlerts()`, `AddTrends()`) that registers all services the feature
requires. The Host calls these extensions; the feature owns its registrations.

#### Scenario: Registering alert analysis
- **WHEN** the Host calls `services.AddAlerts()`
- **THEN** the `AlertAnalyzer` and any supporting types are registered and
  available for injection

#### Scenario: Feature options are library-owned
- **WHEN** a feature has configurable options (e.g. alert thresholds)
- **THEN** the options type is defined in the feature library and bound to a
  config section by the feature's own DI extension — not in Core

### Requirement: Feature types use clean names

Analysis types SHALL use descriptive names without the "Enrichment" prefix:
`AlertAnalyzer`, `DerivedComputer`, `TrendAnalyzer`, `IndexComputer`,
`HistoryTracker`.

#### Scenario: No enrichment naming
- **WHEN** any type in a feature library is inspected
- **THEN** its name does not contain "Enrichment"

### Requirement: History retains its persistent actor

`Njord.History` SHALL contain the `ForecastHistoryActor` (persistent actor with
event sourcing and snapshot support). The actor is defined in the library but
registered centrally in the Host's actor setup. Persistence IDs and DTO wire
format SHALL remain unchanged for data continuity.

#### Scenario: History actor definition
- **WHEN** the `Njord.History` project is inspected
- **THEN** it contains the `HistoryTracker` (compute), the
  `ForecastHistoryActor` (persistent actor), and all history domain types

#### Scenario: History persistence continuity
- **WHEN** the restructured service starts against an existing persistence store
- **THEN** the `ForecastHistoryActor` recovers its state from existing
  `ForecastRecordDto` events and `ForecastHistorySnapshotDto` snapshots without
  data loss

