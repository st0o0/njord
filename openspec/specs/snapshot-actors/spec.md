# snapshot-actors Specification

## Purpose

Akka Persistence actors (snapshot-only, no event journal) that hold the latest forecast and enrichment state. Replaces the in-memory `ForecastSnapshotStore` and `EnrichmentSnapshotStore` with proper actors that survive restarts via persisted snapshots. A `SnapshotConsumerActor` routes events from the EgressActor BroadcastHub to both snapshot actors using Ask/Ack backpressure.

## Requirements

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

### Requirement: SnapshotConsumerActor routes events from BroadcastHub to snapshot actors
`SnapshotConsumerActor` SHALL subscribe to the EgressActor BroadcastHub. It SHALL route `PerModelUpdate` events to `ForecastSnapshotActor` via Ask and wait for Ack before processing the next event. It SHALL route `EnrichmentUpdate` events to `EnrichmentSnapshotActor` via Ask/Ack.

#### Scenario: Forecast update routed with backpressure
- **WHEN** a `PerModelUpdate` arrives from the BroadcastHub
- **THEN** the consumer SHALL Ask `ForecastSnapshotActor` with `UpdateForecast` and wait for `Ack` before consuming the next event

#### Scenario: Enrichment update routed with backpressure
- **WHEN** an `EnrichmentUpdate` arrives from the BroadcastHub
- **THEN** the consumer SHALL Ask `EnrichmentSnapshotActor` with `UpdateEnrichment` and wait for `Ack`

### Requirement: ForecastSnapshotActor recovers state from snapshot after restart
`ForecastSnapshotActor` SHALL recover all previously stored `ModelForecast` entries from its latest snapshot when restarted with the same `PersistenceId`. After recovery, `GetForecast` and `GetAllForecasts` SHALL return the same data that was stored before the restart.

#### Scenario: State recovered after actor restart
- **WHEN** `ForecastSnapshotActor` has stored 20+ forecasts (triggering a snapshot), is gracefully stopped, and a new instance with the same `PersistenceId` is created
- **THEN** `GetAllForecasts` on the new instance SHALL return all previously stored forecasts

#### Scenario: Updates before snapshot threshold are lost on restart
- **WHEN** `ForecastSnapshotActor` has stored fewer than 20 forecasts (no snapshot triggered), is stopped, and a new instance is created
- **THEN** `GetAllForecasts` on the new instance SHALL return an empty collection (state was only in memory)

#### Scenario: Actor accepts new updates after recovery
- **WHEN** `ForecastSnapshotActor` recovers from a snapshot and receives a new `UpdateForecast`
- **THEN** it SHALL store the new forecast and respond with `Ack`

### Requirement: EnrichmentSnapshotActor recovers state from snapshot after restart
`EnrichmentSnapshotActor` SHALL recover all previously stored enrichment entries from its latest snapshot when restarted with the same `PersistenceId`. After recovery, `GetEnrichment` and `GetAllEnrichments` SHALL return the same data that was stored before the restart.

#### Scenario: State recovered after actor restart
- **WHEN** `EnrichmentSnapshotActor` has stored 14+ enrichments (triggering a snapshot), is stopped, and a new instance with the same `PersistenceId` is created
- **THEN** `GetAllEnrichments` on the new instance SHALL return the previously stored enrichments

#### Scenario: Actor accepts new updates after recovery
- **WHEN** `EnrichmentSnapshotActor` recovers from a snapshot and receives a new `UpdateEnrichment`
- **THEN** it SHALL store the new enrichment and respond with `Ack`

### Requirement: Snapshot actors handle snapshot store failures during recovery
When the snapshot store returns an error during recovery, the snapshot actor's behaviour SHALL be observable and deterministic. The test suite SHALL verify what happens: whether the actor crashes, restarts, becomes responsive with empty state, or remains permanently dead.

#### Scenario: Snapshot load failure during recovery
- **WHEN** the snapshot store is configured to fail on load and a snapshot actor starts
- **THEN** the actor's fate (crash/restart/dead) SHALL be documented by the test outcome

#### Scenario: Actor ref remains valid after recovery failure and restart
- **WHEN** the snapshot store transiently fails during recovery but succeeds on retry
- **THEN** the actor SHALL eventually become responsive to `GetForecast` queries
