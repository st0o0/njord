# servus-bootstrap Specification

## Purpose

Structured startup using Servus setup containers: service DI registrations and Akka.NET actor system configuration are encapsulated in domain-specific container classes, keeping Program.cs minimal.
## Requirements
### Requirement: AkkaSetupContainer configures the actor system infrastructure
The system SHALL provide an `AkkaSetupContainer` extending `ActorSystemSetupContainer` that names the actor system `njord` and configures logging, SQL persistence, remoting and cluster formation. Domain actors SHALL be registered via `IActorRegistration` instances resolved from DI, not directly in this container.

#### Scenario: Actor system name is "njord"
- **WHEN** the actor system setup container builds the system
- **THEN** the actor system is named `njord`

#### Scenario: All top-level actors are registered via IActorRegistration
- **WHEN** the actor system is started
- **THEN** every top-level actor is registered through an `IActorRegistration` from a setup container

#### Scenario: Persistence is configured from the persistence options
- **WHEN** the actor system setup container builds the system
- **THEN** `WithSqlPersistence` uses `PersistenceOptions.ConnectionString`, or for SQLite without a connection string `Data Source=` the full path of `StorageOptions.PersistencePath`, with the provider from `PersistenceOptions.Provider`

#### Scenario: Cluster forms via SeedNodes before singletons start
- **WHEN** the actor system setup container builds the system
- **THEN** `WithClustering` is called with `ClusterOptions.SeedNodes` containing the node's own address, so the cluster begins forming during actor system startup — before singleton proxies start their lookup timers

#### Scenario: Remoting uses a fixed port
- **WHEN** the actor system setup container configures remoting
- **THEN** `WithRemoting` uses a fixed port (2552) instead of an ephemeral port (0), because `SeedNodes` requires a known address

#### Scenario: No manual cluster join
- **WHEN** the actor system is started
- **THEN** there is no `cluster.Join(selfAddress)` call in a `WithActors` callback — the `SeedNodes` configuration handles cluster formation

### Requirement: Top-level actors are cluster singletons registered per feature
Every long-lived domain actor SHALL be registered as a cluster singleton with `WithSingleton<TMarker>` inside its feature's `IActorRegistration`; `ConfigManager` is the one exception and SHALL be created with `WithActors` and registered in the `ActorRegistry` under `IConfigManager`. The only shard region SHALL be the poll shard region (`WithShardRegion<IPollRegion>`, region name `poll-worker`) of per-target `PollWorker` entities, orchestrated by the `PollManager` singleton.

#### Scenario: Actor names are preserved
- **WHEN** actors are registered via feature `IActorRegistration` implementations
- **THEN** they use the singleton names `poll-manager`, `pipeline`, `forecast-read-model`, `gateway-supervisor`, `budget-tracker-supervisor`, `sensor-hub`, `mqtt-connection` and `mqtt-state`, the shard region name `poll-worker`, and `ConfigManager` is named `config-manager`

