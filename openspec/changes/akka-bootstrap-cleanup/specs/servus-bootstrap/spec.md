## MODIFIED Requirements

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
