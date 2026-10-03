## MODIFIED Requirements

### Requirement: ForecastHistoryActor persists forecast records via Akka.Persistence
The `ForecastHistoryActor` SHALL be a `ReceivePersistentActor` with PersistenceId `"forecast-history-{location}"`. It SHALL accept `RecordSnapshot` messages containing a `ModelSnapshot` and persist `ForecastRecordDto` events with: timestamp, location, and consensus values. Per-model forecast values SHALL NOT be stored in the record to reduce memory footprint. On recovery, it SHALL rebuild in-memory `ForecastHistory` state from persisted events. It SHALL take a snapshot every `HistoryOptions.SnapshotInterval` events (default 100). Records older than the retention window SHALL be excluded from analysis during recovery.

#### Scenario: Persist and recover
- **WHEN** the actor receives a `RecordSnapshot` and restarts
- **THEN** the recovered state contains the previously persisted record with consensus values

#### Scenario: Snapshot taken after 100 events
- **WHEN** 100 `ForecastRecordDto` events have been persisted with the default `SnapshotInterval`
- **THEN** the actor saves a snapshot of the current `ForecastHistory`

#### Scenario: Old records excluded during recovery
- **WHEN** the actor recovers and retention is 30 days
- **THEN** records older than 30 days are not included in the analysis state

#### Scenario: Records contain only consensus values
- **WHEN** a `RecordSnapshot` is processed
- **THEN** the persisted `ForecastRecord` SHALL contain consensus values and an empty model values dictionary

### Requirement: ForecastHistoryActor responds to history queries
The `ForecastHistoryActor` SHALL accept `QueryHistory` messages and respond with a `ForecastHistoryResult` (a `HistoryQueryResponse`) containing the current `ForecastHistory` state (all records within the retention window).

#### Scenario: Query returns current state
- **WHEN** the actor has 100 records within retention and receives a `QueryHistory`
- **THEN** it responds with a `ForecastHistoryResult` containing 100 records

### Requirement: HistoryResult aggregates all history analysis and serializes to MQTT
`HistoryResult` SHALL be a record holding the location and all history analysis values (per-model MAE, weights, drift, seasonal preference, anomaly detection, weighted consensus values). `StatePayloadBuilder.FromHistory(result, baseTopic)` SHALL serialize it into a single `MqttMessage` on topic `{baseTopic}/{location}/history` with a flat JSON payload.

#### Scenario: History message content
- **WHEN** `StatePayloadBuilder.FromHistory` is called for location "lucerne" with baseTopic "njord"
- **THEN** one message has topic `njord/lucerne/history`

#### Scenario: Cold start produces nulls
- **WHEN** insufficient history exists
- **THEN** the JSON contains null values for MAE, drift, and anomaly fields

#### Scenario: Retained message
- **WHEN** `StatePayloadBuilder.FromHistory` produces a message
- **THEN** the message has Retain = true
