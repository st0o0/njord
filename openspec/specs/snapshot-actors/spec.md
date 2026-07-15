# snapshot-actors Specification

## Purpose

Akka Persistence actors (snapshot-only, no event journal) that hold the latest forecast and enrichment state. Replaces the in-memory `ForecastSnapshotStore` and `EnrichmentSnapshotStore` with proper actors that survive restarts via persisted snapshots. A `SnapshotConsumerActor` routes events from the EgressActor BroadcastHub to both snapshot actors using Ask/Ack backpressure.

## Requirements

### Requirement: ForecastSnapshotActor holds latest forecasts with persistence
`ForecastSnapshotActor` SHALL be an Akka Persistence actor (snapshot-only, no event journal) holding the latest `ModelForecast` per (location, model) pair. It SHALL respond to `GetForecast(location, model)` with the stored `ModelForecast` or null. It SHALL respond to `GetAllForecasts` with all stored forecasts. It SHALL persist its state as a snapshot after each update.

#### Scenario: Store and retrieve a forecast
- **WHEN** `ForecastSnapshotActor` receives `UpdateForecast(location, model, forecast)` followed by `GetForecast(location, model)`
- **THEN** it SHALL return the stored `ModelForecast` and send `Ack` for the update

#### Scenario: Overwrite on new data
- **WHEN** a new `UpdateForecast` arrives for the same (location, model)
- **THEN** the actor SHALL replace the previous forecast and persist a new snapshot

#### Scenario: Unknown forecast returns null
- **WHEN** `GetForecast` is called for a (location, model) with no stored data
- **THEN** the actor SHALL return null

#### Scenario: State survives restart
- **WHEN** the actor restarts and a persisted snapshot exists
- **THEN** the actor SHALL recover its state from the snapshot

### Requirement: EnrichmentSnapshotActor holds latest enrichment results with persistence
`EnrichmentSnapshotActor` SHALL be an Akka Persistence actor (snapshot-only) holding the latest enrichment result per (location, typeName) pair. It SHALL respond to `GetEnrichment(location, typeName)` and `GetAllEnrichments(location)` queries. It SHALL persist its state as a snapshot after each update.

#### Scenario: Store and retrieve an enrichment
- **WHEN** `EnrichmentSnapshotActor` receives `UpdateEnrichment(location, typeName, result)` followed by `GetEnrichment(location, typeName)`
- **THEN** it SHALL return the stored result and send `Ack` for the update

#### Scenario: GetAllEnrichments returns all types for a location
- **WHEN** multiple enrichment types are stored for location "lucerne"
- **THEN** `GetAllEnrichments("lucerne")` SHALL return all of them

#### Scenario: State survives restart
- **WHEN** the actor restarts and a persisted snapshot exists
- **THEN** the actor SHALL recover its state from the snapshot

### Requirement: SnapshotConsumerActor routes events from BroadcastHub to snapshot actors
`SnapshotConsumerActor` SHALL subscribe to the EgressActor BroadcastHub. It SHALL route `PerModelUpdate` events to `ForecastSnapshotActor` via Ask and wait for Ack before processing the next event. It SHALL route `EnrichmentUpdate` events to `EnrichmentSnapshotActor` via Ask/Ack.

#### Scenario: Forecast update routed with backpressure
- **WHEN** a `PerModelUpdate` arrives from the BroadcastHub
- **THEN** the consumer SHALL Ask `ForecastSnapshotActor` with `UpdateForecast` and wait for `Ack` before consuming the next event

#### Scenario: Enrichment update routed with backpressure
- **WHEN** an `EnrichmentUpdate` arrives from the BroadcastHub
- **THEN** the consumer SHALL Ask `EnrichmentSnapshotActor` with `UpdateEnrichment` and wait for `Ack`
