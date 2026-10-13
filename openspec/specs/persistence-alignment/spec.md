# persistence-alignment Specification

## Purpose

FunkArr-aligned persistence patterns — snapshot scheduling via LastSequenceNr, Apply takes event records, journal cleanup after snapshot.

## Requirements

### Requirement: Snapshot scheduling uses LastSequenceNr
Persistent actors SHALL use `LastSequenceNr % SnapshotInterval == 0` for
snapshot scheduling via a private `MaybeSnapshot()` helper. Snapshot counters
SHALL NOT exist on domain state records.

#### Scenario: BudgetTracker snapshots by sequence number
- **GIVEN** a BudgetTracker has persisted events
- **WHEN** `LastSequenceNr` is divisible by `SnapshotInterval`
- **THEN** it saves a snapshot

#### Scenario: BudgetTrackerState has no snapshot counter
- **GIVEN** a `BudgetTrackerState` record
- **WHEN** its fields are inspected
- **THEN** it has no `EventsSinceSnapshot` or similar scheduling field

### Requirement: Apply methods take event records
State `Apply` methods SHALL take the persistence event record directly, not
decomposed primitive values. Decomposition happens inside the method. This
matches FunkArr where `Recover<Event>(evt => _state = _state.Apply(evt))`.

#### Scenario: BudgetTrackerState.Apply takes ApiCallRecorded
- **GIVEN** an `ApiCallRecorded` event
- **WHEN** applied to `BudgetTrackerState`
- **THEN** the `Apply` method signature accepts `ApiCallRecorded` (not `int weight, DateTimeOffset now`)

#### Scenario: PollSchedulerState.Apply takes DataChanged
- **GIVEN** a `DataChanged` message
- **WHEN** applied to `PollSchedulerState`
- **THEN** the `Apply` method signature accepts `DataChanged` (not individual string/int/DateTimeOffset parameters)

### Requirement: Journal cleanup after successful snapshot
Persistent actors SHALL delete old journal entries and snapshots after a
successful snapshot save, preventing unbounded growth. The `SaveSnapshotSuccess`
handler SHALL call `DeleteMessages` and `DeleteSnapshots`. Failure handlers
SHALL log warnings.

#### Scenario: Journal entries are cleaned after snapshot
- **GIVEN** a snapshot is saved successfully at sequence number N
- **WHEN** the `SaveSnapshotSuccess` handler runs
- **THEN** it calls `DeleteMessages(N)` and `DeleteSnapshots` for entries before N

#### Scenario: Cleanup failure is logged but does not crash
- **GIVEN** journal deletion fails
- **WHEN** the `DeleteMessagesFailure` handler runs
- **THEN** it logs a warning and the actor continues normally
