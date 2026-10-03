## ADDED Requirements

### Requirement: ForecastSnapshotStore captures latest forecast per model
The `ForecastSnapshotStore` SHALL be a thread-safe singleton that maintains the latest `ForecastSnapshot` per (location, model) pair. It SHALL be updated from the egress event stream and queryable by the gRPC service.

#### Scenario: Snapshot is populated from PerModelUpdate
- **WHEN** a `PerModelUpdate` egress event is received for (lucerne, icon_d2)
- **THEN** the store SHALL hold a `ForecastSnapshot` for that pair with parsed hourly and daily data

#### Scenario: Snapshot is overwritten on new data
- **WHEN** a new `PerModelUpdate` arrives for the same (location, model)
- **THEN** the store SHALL replace the previous snapshot with the new data

#### Scenario: Query returns null for unknown pair
- **WHEN** `TryGet("lucerne", "unknown_model")` is called
- **THEN** the store SHALL return null

#### Scenario: Snapshot includes update timestamp
- **WHEN** a snapshot is stored
- **THEN** it SHALL carry an `UpdatedAt` timestamp reflecting when the data was received

### Requirement: SnapshotConsumerActor subscribes to egress BroadcastHub
A `SnapshotConsumerActor` SHALL subscribe to the `EgressActor`'s BroadcastHub via `RequestEgressSource`. It SHALL filter for `PerModelUpdate` events and update the `ForecastSnapshotStore`. It SHALL parse the per-horizon JSON payloads into typed `ForecastSnapshot` objects.

#### Scenario: Actor requests source on startup
- **WHEN** `SnapshotConsumerActor` starts
- **THEN** it SHALL send `RequestEgressSource` to the `EgressActor`

#### Scenario: Actor parses horizon payloads into snapshot
- **WHEN** a `PerModelUpdate` with horizon payloads `{"h3": "{\"temperature\":28.8,...}", ...}` arrives
- **THEN** the actor SHALL parse each horizon's JSON and populate the snapshot's hourly points with typed values
