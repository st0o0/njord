## MODIFIED Requirements

### Requirement: ForecastHistoryActor persists forecast records via Akka.Persistence
The `ForecastHistoryActor` SHALL be a `ReceivePersistentActor` with PersistenceId `"forecast-history-{location}"`. It SHALL accept `RecordSnapshot` messages containing a `ModelSnapshot` and persist `ForecastRecorded` events with: timestamp, location, and consensus values. Per-model forecast values SHALL NOT be stored in the record to reduce memory footprint. On recovery, it SHALL rebuild in-memory `ForecastHistory` state from persisted events. It SHALL take a snapshot every 100 events. Records older than the retention window SHALL be excluded from analysis during recovery.

#### Scenario: Persist and recover
- **WHEN** the actor receives a `RecordSnapshot` and restarts
- **THEN** the recovered state contains the previously persisted record with consensus values

#### Scenario: Snapshot taken after 100 events
- **WHEN** 100 `ForecastRecorded` events have been persisted
- **THEN** the actor saves a snapshot of the current `ForecastHistory`

#### Scenario: Old records excluded during recovery
- **WHEN** the actor recovers and retention is 30 days
- **THEN** records older than 30 days are not included in the analysis state

#### Scenario: Records contain only consensus values
- **WHEN** a `RecordSnapshot` is processed
- **THEN** the persisted `ForecastRecord` SHALL contain consensus values and an empty model values dictionary
