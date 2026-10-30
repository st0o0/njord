# servus-bootstrap Specification

## Purpose

Structured startup using Servus setup containers: service DI registrations and Akka.NET actor system configuration are encapsulated in domain-specific container classes, keeping Program.cs minimal.
## Requirements
### Requirement: AkkaSetupContainer configures the actor system infrastructure
The system SHALL provide an `AkkaSetupContainer` extending `ActorSystemSetupContainer` that names the actor system `njord` and configures logging, SQL persistence, remoting and cluster formation. Logging SHALL use `ClearLoggers()` and `AddLoggerFactory()` without explicit log-level configuration. Persistence selection SHALL be inline: `ConnectionString` set means PostgreSQL, absent means SQLite at `DataPath`. Remoting SHALL use `localhost:2552` (hardcoded). Clustering SHALL use a single seed node at `akka.tcp://njord@localhost:2552`. Domain actors SHALL be registered via `IActorRegistration` instances resolved from DI.

#### Scenario: Actor system name is "njord"
- **WHEN** the actor system setup container builds the system
- **THEN** the actor system is named `njord`

#### Scenario: All top-level actors are registered via IActorRegistration
- **WHEN** the actor system is started
- **THEN** every top-level actor is registered through an `IActorRegistration` from a setup container

#### Scenario: Persistence is configured from ConnectionString presence
- **WHEN** `ConnectionString` is set in `PersistenceOptions`
- **THEN** `WithSqlPersistence` uses that connection string with `ProviderName.PostgreSQL`

#### Scenario: Persistence defaults to SQLite at DataPath
- **WHEN** `ConnectionString` is not set in `PersistenceOptions`
- **THEN** `WithSqlPersistence` uses `Data Source={absolute DataPath}` with `ProviderName.SQLiteMS`

#### Scenario: Cluster forms via SeedNodes before singletons start
- **WHEN** the actor system setup container builds the system
- **THEN** `WithClustering` is called with `SeedNodes` containing `akka.tcp://njord@localhost:2552`

#### Scenario: Remoting uses a fixed port
- **WHEN** the actor system setup container configures remoting
- **THEN** `WithRemoting` uses port 2552, hardcoded

#### Scenario: No manual cluster join
- **WHEN** the actor system is started
- **THEN** there is no `cluster.Join(selfAddress)` call -- `SeedNodes` handles cluster formation

#### Scenario: Logging uses LoggerFactory without explicit Akka log level
- **WHEN** the actor system setup container configures logging
- **THEN** `ClearLoggers()` and `AddLoggerFactory()` are called without setting `loggers.LogLevel`

### Requirement: Top-level actors are cluster singletons registered per feature
Every long-lived domain actor SHALL be registered as a cluster singleton with `WithSingleton<TMarker>` inside its feature's `IActorRegistration`; `ConfigManager` is the one exception and SHALL be created with `WithActors` and registered in the `ActorRegistry` under `IConfigManager`. The only shard region SHALL be the poll shard region (`WithShardRegion<IPollRegion>`, region name `poll-worker`) of per-target `PollWorker` entities, orchestrated by the `PollManager` singleton.

#### Scenario: Actor names are preserved
- **WHEN** actors are registered via feature `IActorRegistration` implementations
- **THEN** they use the singleton names `poll-manager`, `pipeline`, `forecast-read-model`, `gateway-supervisor`, `budget-tracker-supervisor`, `sensor-hub`, `mqtt-connection` and `mqtt-state`, the shard region name `poll-worker`, and `ConfigManager` is named `config-manager`
