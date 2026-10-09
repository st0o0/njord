## ADDED Requirements

### Requirement: Persistence delete-failures are logged
Persistent actors that call `DeleteMessages`/`DeleteSnapshots` as part of their snapshot
cleanup SHALL register handlers for `DeleteMessagesFailure` and `DeleteSnapshotsFailure`
that log a warning, in addition to the existing `DeleteMessagesSuccess`/
`DeleteSnapshotSuccess` handlers.

#### Scenario: Journal cleanup failure is logged, not dropped
- **WHEN** a `DeleteMessagesFailure` or `DeleteSnapshotsFailure` message is received by
  `BudgetTrackerActor` or `ForecastHistoryActor`
- **THEN** the actor logs a warning including the failure cause instead of letting the
  message reach dead letters unhandled

## MODIFIED Requirements

### Requirement: Immutable state records for persistent actors
Each persistent actor (BudgetTrackerActor, SchedulerActor, ForecastSnapshotActor, EnrichmentSnapshotActor, ForecastHistoryActor) SHALL have a companion immutable `record` holding its full internal state. The actor SHALL NOT hold mutable fields for persistent state — all persistent state SHALL live in the state record. Business decisions derived from that state (e.g. whether a new fetch result counts as a data change, whether a rate-limit floor applies, how a recorded forecast snapshot is reduced to a history event) SHALL be made by named methods on the state record, not by comparisons or computations written inline in the actor's command handlers.

#### Scenario: State record is immutable
- **WHEN** a persistent actor processes a command that changes state
- **THEN** the actor creates a new state record instance via an `Apply()` call rather than mutating fields

#### Scenario: Actor is a thin shell
- **WHEN** inspecting a persistent actor's message handlers
- **THEN** each handler delegates business logic to state extension methods and only performs actor-specific operations (Persist, Tell, SaveSnapshot, Become)

#### Scenario: SchedulerActor delegates hash-change and rate-limit decisions
- **WHEN** `SchedulerActor` receives a `HashResult` or a `FetchFailed` with reason `RateLimited`
- **THEN** the decision (data changed vs. miss; whether the rate-limit floor extends `NextPollUtc`) is produced by a named `SchedulerState`/`ModelPollState` method, and the actor only applies the returned state and reacts to the returned outcome

#### Scenario: ForecastHistoryActor delegates snapshot reduction
- **WHEN** `ForecastHistoryActor` receives a `RecordSnapshot` command
- **THEN** the nearest-point lookup, per-parameter value extraction, and consensus-median computation are produced by a `ForecastHistoryState` method, and the actor only persists the returned event
