## MODIFIED Requirements

### Requirement: ForecastSnapshotActor holds latest forecasts with persistence
`ForecastSnapshotActor` SHALL be an Akka Persistence actor (snapshot-only, no event journal) holding the latest `ModelForecast` per (location, model) pair. It SHALL respond to `GetForecast(location, model)` with the stored `ModelForecast` or null. It SHALL respond to `GetAllForecasts` with all stored forecasts. It SHALL persist its state as a snapshot every N updates (default 20), not on every individual update. After a successful snapshot save, it SHALL delete all previous snapshots to prevent unbounded storage growth. The snapshot state SHALL be a dedicated DTO type (`ForecastSnapshotDto`), not domain objects directly. The actor SHALL map domain objects to DTOs on save and DTOs to domain objects on recovery.

#### Scenario: Store and retrieve a forecast
- **WHEN** `ForecastSnapshotActor` receives `UpdateForecast(location, model, forecast)` followed by `GetForecast(location, model)`
- **THEN** it SHALL return the stored `ModelForecast` and send `Ack` for the update

#### Scenario: Overwrite on new data
- **WHEN** a new `UpdateForecast` arrives for the same (location, model)
- **THEN** the actor SHALL replace the previous forecast and persist a new snapshot

#### Scenario: Unknown forecast returns null
- **WHEN** `GetForecast` is called for a (location, model) with no stored data
- **THEN** the actor SHALL return null

#### Scenario: Snapshot saved after N updates
- **WHEN** `ForecastSnapshotActor` receives 20 `UpdateForecast` messages
- **THEN** it SHALL save a snapshot containing all current state as a `ForecastSnapshotDto`

#### Scenario: Single update does not trigger snapshot
- **WHEN** `ForecastSnapshotActor` receives 1 `UpdateForecast` message
- **THEN** it SHALL NOT save a snapshot

#### Scenario: Old snapshots deleted after save
- **WHEN** a snapshot save succeeds with `SequenceNr > 0`
- **THEN** the actor SHALL delete all snapshots older than the current one

#### Scenario: State survives restart
- **WHEN** the actor restarts and a persisted snapshot exists
- **THEN** the actor SHALL recover its state from the DTO snapshot and map it back to domain objects

### Requirement: EnrichmentSnapshotActor holds latest enrichment results with persistence
`EnrichmentSnapshotActor` SHALL be an Akka Persistence actor (snapshot-only) holding the latest enrichment result per (location, typeName) pair. It SHALL respond to `GetEnrichment(location, typeName)` and `GetAllEnrichments(location)` queries. It SHALL persist its state as a snapshot every N updates (default 14), not on every individual update. After a successful snapshot save, it SHALL delete all previous snapshots. The snapshot state SHALL be a dedicated DTO type (`EnrichmentSnapshotDto`), not domain objects directly. The actor SHALL map domain objects to DTOs on save and DTOs to domain objects on recovery.

#### Scenario: Store and retrieve an enrichment
- **WHEN** `EnrichmentSnapshotActor` receives `UpdateEnrichment(location, typeName, result)` followed by `GetEnrichment(location, typeName)`
- **THEN** it SHALL return the stored result and send `Ack` for the update

#### Scenario: GetAllEnrichments returns all types for a location
- **WHEN** multiple enrichment types are stored for location "lucerne"
- **THEN** `GetAllEnrichments("lucerne")` SHALL return all of them

#### Scenario: Snapshot saved after N updates
- **WHEN** `EnrichmentSnapshotActor` receives 14 `UpdateEnrichment` messages
- **THEN** it SHALL save a snapshot containing all current state as an `EnrichmentSnapshotDto`

#### Scenario: Old snapshots deleted after save
- **WHEN** a snapshot save succeeds with `SequenceNr > 0`
- **THEN** the actor SHALL delete all snapshots older than the current one

#### Scenario: State survives restart
- **WHEN** the actor restarts and a persisted snapshot exists
- **THEN** the actor SHALL recover its state from the DTO snapshot and map it back to domain objects
