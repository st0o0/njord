## ADDED Requirements

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
