## Requirements

### Requirement: Entity actors registered as ShardRegions

ForecastHistoryActor, ForecastSnapshotActor, and EnrichmentSnapshotActor SHALL be registered as ShardRegions. ForecastHistoryActor entities SHALL be keyed by location. ForecastSnapshotActor entities SHALL be keyed by location|modelId. EnrichmentSnapshotActor entities SHALL be keyed by location|typeName.

#### Scenario: ForecastHistoryActor routes by location
- **WHEN** a message implementing IWithLocation is sent to the ForecastHistory ShardRegion
- **THEN** the message SHALL be routed to the entity instance for that location

#### Scenario: ForecastSnapshotActor routes by location and model
- **WHEN** a message implementing IWithModelKey is sent to the ForecastSnapshot ShardRegion
- **THEN** the message SHALL be routed to the entity instance for that location|modelId combination

#### Scenario: EnrichmentSnapshotActor routes by location and type
- **WHEN** a message implementing IWithEnrichmentKey is sent to the EnrichmentSnapshot ShardRegion
- **THEN** the message SHALL be routed to the entity instance for that location|typeName combination

### Requirement: Message routing marker interfaces

Messages targeting shard entities SHALL implement a routing marker interface that exposes the fields needed for EntityId extraction. IWithLocation SHALL expose `Location`. IWithModelKey SHALL extend IWithLocation and expose `ModelId`. IWithEnrichmentKey SHALL extend IWithLocation and expose `TypeName`.

#### Scenario: Snapshot update message carries routing marker
- **WHEN** an UpdateForecast message is created for a specific location and model
- **THEN** the message SHALL implement IWithModelKey with the target location and modelId

#### Scenario: Enrichment query message carries routing marker
- **WHEN** a QueryEnrichment message is created for a specific location and type
- **THEN** the message SHALL implement IWithEnrichmentKey with the target location and typeName

### Requirement: NjordMessageExtractor extracts EntityId from markers

A NjordMessageExtractor SHALL extract the EntityId from messages using their routing marker interface. For IWithModelKey, the EntityId SHALL be `"{Location}|{ModelId}"`. For IWithEnrichmentKey, the EntityId SHALL be `"{Location}|{TypeName}"`. For IWithLocation (without a more specific marker), the EntityId SHALL be `"{Location}"`.

#### Scenario: ModelKey message extraction
- **WHEN** the MessageExtractor receives a message implementing IWithModelKey with Location "borken" and ModelId "icon_d2"
- **THEN** the extracted EntityId SHALL be "borken|icon_d2"

#### Scenario: EnrichmentKey message extraction
- **WHEN** the MessageExtractor receives a message implementing IWithEnrichmentKey with Location "borken" and TypeName "consensus"
- **THEN** the extracted EntityId SHALL be "borken|consensus"

#### Scenario: Location-only message extraction
- **WHEN** the MessageExtractor receives a message implementing IWithLocation with Location "borken"
- **THEN** the extracted EntityId SHALL be "borken"

### Requirement: Shard entities passivate after idle timeout

Shard entity actors SHALL passivate (stop) after a configurable period of inactivity. The passivation timeout SHALL be configurable per ShardRegion.

#### Scenario: Entity passivates after no messages
- **WHEN** a shard entity receives no messages for the configured idle timeout duration
- **THEN** the entity SHALL be passivated (stopped) by the shard infrastructure

#### Scenario: Entity reactivates on next message
- **WHEN** a message arrives for a passivated entity
- **THEN** the shard infrastructure SHALL create a new instance of that entity
- **AND** the entity SHALL recover its persisted state

### Requirement: Per-entity PersistenceId

Each shard entity instance SHALL derive its PersistenceId from a fixed prefix and its EntityId. This SHALL produce a unique PersistenceId per entity, isolating each entity's journal and snapshot store.

#### Scenario: ForecastSnapshot entity PersistenceId
- **WHEN** a ForecastSnapshotActor entity is created with EntityId "borken|icon_d2"
- **THEN** its PersistenceId SHALL incorporate the EntityId (e.g. "forecast-snapshot-borken|icon_d2")

#### Scenario: ForecastHistory entity PersistenceId
- **WHEN** a ForecastHistoryActor entity is created with EntityId "borken"
- **THEN** its PersistenceId SHALL incorporate the EntityId (e.g. "forecast-history-borken")
