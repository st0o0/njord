## ADDED Requirements

### Requirement: Poll worker records carry location coordinates
`PollingStarted` and `PersistedPollWorkerState` SHALL carry the target location's latitude and longitude as trailing nullable members defaulting to null. The poll worker SHALL persist them, and state recovery (event replay and snapshot restore) SHALL rebuild the target location with them, falling back to `0` when they are null.

#### Scenario: Started worker persists coordinates
- **WHEN** a `PollWorker` handles `StartPolling` for `Location("lucerne", 47.05, 8.31)`
- **THEN** the persisted `PollingStarted` has `Latitude` 47.05 and `Longitude` 8.31

#### Scenario: Recovered worker keeps coordinates
- **WHEN** a `PollWorker` recovers from its journal after having been started with `Location("lucerne", 47.05, 8.31)`
- **THEN** its target location has latitude 47.05 and longitude 8.31

#### Scenario: Snapshot restore keeps coordinates
- **WHEN** a worker state with coordinates 47.05 / 8.31 is mapped to `PersistedPollWorkerState` and back
- **THEN** the restored target location has latitude 47.05 and longitude 8.31

#### Scenario: Old data falls back to zero
- **WHEN** a `PollingStarted` event or snapshot without coordinates (null) is applied or restored
- **THEN** the target location has latitude 0 and longitude 0 and the worker is otherwise unchanged

### Requirement: Started worker refreshes a changed target
A `PollWorker` that is already started SHALL persist a new `PollingStarted` when `StartPolling` arrives with different coordinates, instead of ignoring the command.

#### Scenario: Restart with changed coordinates
- **WHEN** a started worker for `lucerne` at 47.05 / 8.31 receives `StartPolling` for `lucerne` at 47.10 / 8.40
- **THEN** the worker persists `PollingStarted` and its target location has latitude 47.10 and longitude 8.40, with domain poll states preserved

#### Scenario: Restart with identical target
- **WHEN** a started worker receives `StartPolling` with the same coordinates and discovery interval
- **THEN** no event is persisted and the worker reports `Planned`
