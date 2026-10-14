## REMOVED Requirements

### Requirement: Three-tier persistence model
**Reason**: One scenario named the removed `PollScheduler`; the rule is unchanged and restated for the poll worker.
**Migration**: See "Three-tier persistence model for every persistent actor".

### Requirement: Transient runtime state stays on the actor
**Reason**: Its scenario described the removed `PollScheduler`; the rule is restated with the poll worker and manager.
**Migration**: See "Transient runtime state stays on the actor class".

## MODIFIED Requirements

### Requirement: Persistence delete-failures are logged
Persistent actors that call `DeleteMessages`/`DeleteSnapshots` as part of their snapshot
cleanup SHALL register handlers for `DeleteMessagesFailure` and `DeleteSnapshotsFailure`
that log a warning, in addition to the existing `DeleteMessagesSuccess`/
`DeleteSnapshotSuccess` handlers.

#### Scenario: Journal cleanup failure is logged, not dropped
- **WHEN** a `DeleteMessagesFailure` or `DeleteSnapshotsFailure` message is received by
  `BudgetTracker` or by a `PollWorker`
- **THEN** the actor logs a warning including the failure cause instead of letting the
  message reach dead letters unhandled

## ADDED Requirements

### Requirement: Three-tier persistence model for every persistent actor
Each persistent actor SHALL maintain three distinct state tiers: Internal State (the immutable state record with full domain logic), Snapshot (caller-facing view via `GetSnapshot()`), and Persisted (journal DTO via `GetPersistenceState()`). The `GetPersistenceState()` method SHALL produce the extend-only DTO type.

Every persistent actor SHALL implement `GetPersistenceState()` and use it in `SaveSnapshot`. Every persistent actor SHALL implement `FromPersistence()` and use it in the `Recover<SnapshotOffer>` handler. No-op snapshot recovery handlers are not permitted.

#### Scenario: GetPersistenceState produces existing DTO
- **WHEN** `BudgetTrackerState.GetPersistenceState()` is called
- **THEN** it returns a `PersistedBudgetTrackerState` matching the existing DTO schema

#### Scenario: FromPersistence restores state from DTO
- **WHEN** an actor recovers from a `SnapshotOffer` containing a persisted state DTO
- **THEN** `<Name>State.FromPersistence(dto)` produces a valid state record, and the actor resumes with correct values

#### Scenario: Persistence roundtrip preserves state
- **WHEN** a state record is converted via `GetPersistenceState()` and then restored via `FromPersistence()`
- **THEN** the resulting state record is equal to the original

#### Scenario: PollWorker implements real snapshot recovery
- **WHEN** a `PollWorker` receives a `SnapshotOffer` during recovery
- **THEN** it SHALL restore state via `PollWorkerState.FromPersistence()`, including the next poll time, instead of ignoring the snapshot

#### Scenario: PollWorker snapshot answers the manager
- **WHEN** a `PollWorker` reports to the manager
- **THEN** the report carries `PollWorkerState.GetSnapshot(target)`, a `PollStateEntry`, never the state record itself

### Requirement: Transient runtime state stays on the actor class
For actors with transient runtime state (actor refs, request in flight, retry counters, pending timers, aggregated views of other actors), those fields SHALL remain as mutable fields on the actor class. Only persistent domain state moves to the state record.

#### Scenario: PollWorker transient fields
- **WHEN** `PollWorker` is inspected
- **THEN** the id of the request in flight and the poll timer are actor fields, while target, discovery interval, started flag and the poll state live in `PollWorkerState`

#### Scenario: PollManager transient fields
- **WHEN** `PollManager` is inspected
- **THEN** the latest reported poll state per target, the per-location poll-cycle tracker, the watched workers and the restart attempts are actor fields; the manager persists nothing
