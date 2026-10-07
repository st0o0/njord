## MODIFIED Requirements

### Requirement: ForecastSnapshotActor stores the latest forecasts as ShardRegion entities
`ForecastSnapshotActor` SHALL be a ShardRegion entity managed by the `IForecastSnapshotRegion` ShardRegion. Each entity SHALL manage the forecast for a single `(location, modelId)` key. The entity's `PersistenceId` SHALL be derived from the entity id (e.g. `"forecast-snapshot-{location}|{modelId}"`). The entity SHALL hold a single `ModelForecast` value (not a dictionary). It SHALL respond to `UpdateForecast` (which implements `IWithModelKey`) with `Ack` after storing the forecast. It SHALL respond to `QueryForecast` (which implements `IWithModelKey`) with `ForecastFound` or `ForecastNotFound`. It SHALL persist its state as a snapshot every N updates (default 20). After a successful snapshot save, it SHALL delete all previous snapshots. The snapshot state SHALL be a dedicated DTO type (`ForecastSnapshotDto`). The entity SHALL passivate after idle timeout.

`QueryAllForecasts` SHALL be handled via Akka.Streams fan-out: `Source.From(knownKeys).Ask<T>(region, timeout, parallelism)` with `ResumingDecider` supervision. The caller SHALL derive entity keys from configured locations and models.

#### Scenario: Store and retrieve a forecast via ShardRegion
- **WHEN** `UpdateForecast("lucerne", "icon_d2", forecast)` is sent to the ShardRegion
- **THEN** the message is routed to entity `"lucerne|icon_d2"` which stores the forecast and responds `Ack`

#### Scenario: Query routed to correct entity
- **WHEN** `QueryForecast("lucerne", "icon_d2")` is sent to the ShardRegion
- **THEN** the message is routed to entity `"lucerne|icon_d2"` which responds with `ForecastFound`

#### Scenario: Unknown entity returns ForecastNotFound
- **WHEN** `QueryForecast("paris", "icon_d2")` is sent and no prior update exists
- **THEN** the entity responds with `ForecastNotFound`

#### Scenario: Snapshot saved after N updates per entity
- **WHEN** an entity receives 20 `UpdateForecast` messages
- **THEN** it saves a snapshot containing its single forecast as `ForecastSnapshotDto`

#### Scenario: Entity passivates after idle timeout
- **WHEN** an entity receives no messages for the configured idle period
- **THEN** it passivates and is removed from memory

#### Scenario: State survives restart per entity
- **WHEN** an entity restarts and a persisted snapshot exists for its key
- **THEN** the entity recovers its single forecast from the snapshot

#### Scenario: QueryAllForecasts uses Akka.Streams fan-out
- **WHEN** `QueryAllForecasts` is requested
- **THEN** the handler fans out `QueryForecast` to all known entity keys via `Source.From(keys).Ask<T>(region)` with bounded parallelism and `ResumingDecider`

### Requirement: EnrichmentSnapshotActor stores the latest enrichment results as ShardRegion entities
`EnrichmentSnapshotActor` SHALL be a ShardRegion entity managed by the `IEnrichmentSnapshotRegion` ShardRegion. Each entity SHALL manage the enrichment result for a single `(location, typeName)` key. The entity's `PersistenceId` SHALL be derived from the entity id (e.g. `"enrichment-snapshot-{location}|{typeName}"`). The entity SHALL hold a single enrichment result value (not a dictionary). It SHALL respond to `UpdateEnrichment` (which implements `IWithEnrichmentKey`) with `Ack`. It SHALL respond to `QueryEnrichment` (which implements `IWithEnrichmentKey`) with `EnrichmentFound` or `EnrichmentNotFound`. It SHALL persist its state as a snapshot every N updates (default 14). After a successful snapshot save, it SHALL delete all previous snapshots. The entity SHALL passivate after idle timeout.

`QueryAllEnrichments(location)` SHALL be handled via Akka.Streams fan-out to the ShardRegion, deriving entity keys from the location and registered enrichment type names.

#### Scenario: Store and retrieve an enrichment via ShardRegion
- **WHEN** `UpdateEnrichment("lucerne", "consensus", result)` is sent to the ShardRegion
- **THEN** the message is routed to entity `"lucerne|consensus"` which stores the result and responds `Ack`

#### Scenario: Query routed to correct entity
- **WHEN** `QueryEnrichment("lucerne", "alerts")` is sent to the ShardRegion
- **THEN** the message is routed to entity `"lucerne|alerts"` which responds with `EnrichmentFound` or `EnrichmentNotFound`

#### Scenario: Snapshot saved after N updates per entity
- **WHEN** an entity receives 14 `UpdateEnrichment` messages
- **THEN** it saves a snapshot containing its single enrichment result

#### Scenario: Entity passivates after idle timeout
- **WHEN** an entity receives no messages for the configured idle period
- **THEN** it passivates and is removed from memory

#### Scenario: QueryAllEnrichments uses Akka.Streams fan-out
- **WHEN** `QueryAllEnrichments("lucerne")` is requested
- **THEN** the handler fans out `QueryEnrichment` to all known enrichment type keys for that location via `Source.From(keys).Ask<T>(region)` with bounded parallelism and `ResumingDecider`

### Requirement: GrpcSnapshotConsumerActor routes events from producer BroadcastHubs to ShardRegion entities
`GrpcSnapshotConsumerActor` SHALL subscribe to `ModelStateActor` and `EnrichmentActor` BroadcastHubs via SourceRef. It SHALL route `PerModelUpdate` events to the `IForecastSnapshotRegion` ShardRegion and `EnrichmentUpdate` events to the `IEnrichmentSnapshotRegion` ShardRegion. Messages sent to ShardRegions SHALL implement the appropriate routing marker interface (`IWithModelKey` or `IWithEnrichmentKey`) so the `NjordMessageExtractor` can extract the entity id.

#### Scenario: Forecast update routed to ShardRegion
- **WHEN** a PerModelUpdate event arrives from ModelStateActor
- **THEN** it is converted to an `UpdateForecast` message (implementing `IWithModelKey`) and sent to the `IForecastSnapshotRegion` ShardRegion via Ask

#### Scenario: Enrichment update routed to ShardRegion
- **WHEN** an EnrichmentUpdate event arrives from EnrichmentActor
- **THEN** it is converted to an `UpdateEnrichment` message (implementing `IWithEnrichmentKey`) and sent to the `IEnrichmentSnapshotRegion` ShardRegion via Ask
