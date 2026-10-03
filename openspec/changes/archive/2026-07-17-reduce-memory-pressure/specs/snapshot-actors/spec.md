## MODIFIED Requirements

### Requirement: ForecastSnapshotActor holds latest forecasts with persistence
`ForecastSnapshotActor` SHALL be an Akka Persistence actor (snapshot-only, no event journal) holding the latest `ModelForecast` per (location, model) pair. It SHALL respond to `GetForecast(location, model)` with the stored `ModelForecast` or null. It SHALL respond to `GetAllForecasts` with all stored forecasts. It SHALL persist its state as a snapshot every N updates (default 20), not on every individual update. After a successful snapshot save, it SHALL delete all previous snapshots to prevent unbounded storage growth.

#### Scenario: Store and retrieve a forecast
- **WHEN** `ForecastSnapshotActor` receives `UpdateForecast(location, model, forecast)` followed by `GetForecast(location, model)`
- **THEN** it SHALL return the stored `ModelForecast` and send `Ack` for the update

#### Scenario: Snapshot saved after N updates
- **WHEN** `ForecastSnapshotActor` receives 20 `UpdateForecast` messages
- **THEN** it SHALL save a snapshot containing all current state

#### Scenario: Single update does not trigger snapshot
- **WHEN** `ForecastSnapshotActor` receives 1 `UpdateForecast` message
- **THEN** it SHALL NOT save a snapshot

#### Scenario: Old snapshots deleted after save
- **WHEN** a snapshot save succeeds
- **THEN** the actor SHALL delete all snapshots older than the current one

#### Scenario: State survives restart
- **WHEN** the actor restarts and a persisted snapshot exists
- **THEN** the actor SHALL recover its state from the snapshot

### Requirement: EnrichmentSnapshotActor holds latest enrichment results with persistence
`EnrichmentSnapshotActor` SHALL be an Akka Persistence actor (snapshot-only) holding the latest enrichment result per (location, typeName) pair. It SHALL respond to `GetEnrichment(location, typeName)` and `GetAllEnrichments(location)` queries. It SHALL persist its state as a snapshot every N updates (default 14), not on every individual update. After a successful snapshot save, it SHALL delete all previous snapshots.

#### Scenario: Store and retrieve an enrichment
- **WHEN** `EnrichmentSnapshotActor` receives `UpdateEnrichment(location, typeName, result)` followed by `GetEnrichment(location, typeName)`
- **THEN** it SHALL return the stored result and send `Ack` for the update

#### Scenario: Snapshot saved after N updates
- **WHEN** `EnrichmentSnapshotActor` receives 14 `UpdateEnrichment` messages
- **THEN** it SHALL save a snapshot containing all current state

#### Scenario: Old snapshots deleted after save
- **WHEN** a snapshot save succeeds
- **THEN** the actor SHALL delete all snapshots older than the current one

#### Scenario: State survives restart
- **WHEN** the actor restarts and a persisted snapshot exists
- **THEN** the actor SHALL recover its state from the snapshot
