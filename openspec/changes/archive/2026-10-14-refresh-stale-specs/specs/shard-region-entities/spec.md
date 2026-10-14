## REMOVED Requirements

### Requirement: Entity actors registered as ShardRegions
**Reason**: No actor is hosted as a ShardRegion any more. `ForecastHistoryActor`, `ForecastSnapshotActor` and `EnrichmentSnapshotActor` were removed, together with `NjordMessageExtractor` and the routing markers `IWithModelKey`/`IWithEnrichmentKey`. All actors are cluster singletons registered with `WithSingleton` (or plain actors via `WithActors` for `ConfigManager`).
**Migration**: See `domain-setup-containers` for the current singleton registrations per setup container.

### Requirement: Message routing marker interfaces
**Reason**: No actor is hosted as a ShardRegion any more. `ForecastHistoryActor`, `ForecastSnapshotActor` and `EnrichmentSnapshotActor` were removed, together with `NjordMessageExtractor` and the routing markers `IWithModelKey`/`IWithEnrichmentKey`. All actors are cluster singletons registered with `WithSingleton` (or plain actors via `WithActors` for `ConfigManager`).
**Migration**: See `domain-setup-containers` for the current singleton registrations per setup container.

### Requirement: NjordMessageExtractor extracts EntityId from markers
**Reason**: No actor is hosted as a ShardRegion any more. `ForecastHistoryActor`, `ForecastSnapshotActor` and `EnrichmentSnapshotActor` were removed, together with `NjordMessageExtractor` and the routing markers `IWithModelKey`/`IWithEnrichmentKey`. All actors are cluster singletons registered with `WithSingleton` (or plain actors via `WithActors` for `ConfigManager`).
**Migration**: See `domain-setup-containers` for the current singleton registrations per setup container.

### Requirement: Shard entities passivate after idle timeout
**Reason**: No actor is hosted as a ShardRegion any more. `ForecastHistoryActor`, `ForecastSnapshotActor` and `EnrichmentSnapshotActor` were removed, together with `NjordMessageExtractor` and the routing markers `IWithModelKey`/`IWithEnrichmentKey`. All actors are cluster singletons registered with `WithSingleton` (or plain actors via `WithActors` for `ConfigManager`).
**Migration**: See `domain-setup-containers` for the current singleton registrations per setup container.

### Requirement: Per-entity PersistenceId
**Reason**: No actor is hosted as a ShardRegion any more. `ForecastHistoryActor`, `ForecastSnapshotActor` and `EnrichmentSnapshotActor` were removed, together with `NjordMessageExtractor` and the routing markers `IWithModelKey`/`IWithEnrichmentKey`. All actors are cluster singletons registered with `WithSingleton` (or plain actors via `WithActors` for `ConfigManager`).
**Migration**: See `domain-setup-containers` for the current singleton registrations per setup container.
