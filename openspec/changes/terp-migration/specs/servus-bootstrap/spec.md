## RENAMED Requirements

### FROM: servus-bootstrap
### TO: terp-bootstrap

## MODIFIED Requirements

### Requirement: AkkaSetupContainer configures the actor system infrastructure
The system SHALL provide an `AkkaSetup` extending `Terp.Akka.ActorSystemSetup` that names the actor system `njord` and configures logging, SQL persistence, remoting and cluster formation. `ActorSystemName` SHALL be a property override (not a method). Logging SHALL use `ClearLoggers()` and `AddLoggerFactory()` without explicit log-level configuration. Persistence selection SHALL be inline: `ConnectionString` set means PostgreSQL, absent means SQLite at `DataPath`. Remoting SHALL use `localhost:2552` (hardcoded). Clustering SHALL use a single seed node at `akka.tcp://njord@localhost:2552`. Domain actors SHALL be registered via `IActorRegistration` instances resolved from DI.

#### Scenario: Actor system name is "njord"
- **WHEN** the actor system setup builds the system
- **THEN** the actor system is named `njord`

#### Scenario: All top-level actors are registered via IActorRegistration
- **WHEN** the actor system is started
- **THEN** every top-level actor is registered through an `IActorRegistration` from a setup class

#### Scenario: Persistence is configured from ConnectionString presence
- **WHEN** `ConnectionString` is set in `PersistenceOptions`
- **THEN** `WithSqlPersistence` uses that connection string with `ProviderName.PostgreSQL`

#### Scenario: Persistence defaults to SQLite at DataPath
- **WHEN** `ConnectionString` is not set in `PersistenceOptions`
- **THEN** `WithSqlPersistence` uses `Data Source={absolute DataPath}` with `ProviderName.SQLiteMS`

#### Scenario: Cluster forms via SeedNodes before singletons start
- **WHEN** the actor system setup builds the system
- **THEN** `WithClustering` is called with `SeedNodes` containing `akka.tcp://njord@localhost:2552`

#### Scenario: Remoting uses a fixed port
- **WHEN** the actor system setup configures remoting
- **THEN** `WithRemoting` uses port 2552, hardcoded

#### Scenario: No manual cluster join
- **WHEN** the actor system is started
- **THEN** there is no `cluster.Join(selfAddress)` call -- `SeedNodes` handles cluster formation

#### Scenario: Logging uses LoggerFactory without explicit Akka log level
- **WHEN** the actor system setup configures logging
- **THEN** `ClearLoggers()` and `AddLoggerFactory()` are called without setting `loggers.LogLevel`

### Requirement: Top-level actors are cluster singletons registered per feature
Every long-lived domain actor SHALL be registered as a cluster singleton with `WithSingleton<TMarker>` inside its feature's `IActorRegistration`; the only shard region SHALL be the poll shard region (`WithShardRegion<IPollRegion>`, region name `poll-worker`) of per-target `PollWorker` entities, orchestrated by the `PollManager` singleton.

#### Scenario: Actor names are preserved
- **WHEN** actors are registered via feature `IActorRegistration` implementations
- **THEN** they use the singleton names `poll-manager`, `pipeline`, `gateway-supervisor`, `budget-tracker-supervisor`, `grpc-consumer`, and the shard region name `poll-worker`

### Requirement: NjordHost.Create is a pure composition chain
`NjordHost` SHALL expose two static methods: `Configure(WebApplicationBuilder)` returning the builder, and `Configure(WebApplication)` returning the app. `Configure(builder)` SHALL configure Serilog, clear logging providers, set Kestrel's `MinResponseDataRate` to null, then call `builder.Setup(new XxxSetup())` for each setup in the fixed order: Core, Config, Health, OpenMeteo, Budget, Polling, Pipeline, Domain, Grpc, Akka. `Configure(app)` SHALL call `app.Setup(new NjordApplicationSetup())`. Integration tests call `Configure(builder)`, override services, build, then call `Configure(app)`.

#### Scenario: NjordHost configures services via explicit Setup calls
- **WHEN** reading `NjordHost.Configure(builder)`
- **THEN** it SHALL call `builder.Setup(new XxxSetup())` for each setup in order, not `AppBuilder.Create(...).WithSetup<>()`

#### Scenario: Program.cs uses NjordHost.Configure
- **WHEN** the host starts
- **THEN** `Program.cs` calls `NjordHost.Configure(builder)`, builds, calls `NjordHost.Configure(app)`, then `app.RunAsync()`

#### Scenario: Integration test hook is preserved
- **WHEN** integration tests configure the host
- **THEN** they call `NjordHost.Configure(builder)`, override services, call `builder.Build()`, then `NjordHost.Configure(app)` and `app.StartAsync()`

## REMOVED Requirements

### Requirement: LoggingSetupContainer configures Serilog
**Reason**: Serilog configuration stays inline in `NjordHost.Configure(builder)` before the setup chain, not in a separate setup class. This matches the actual implementation and keeps the logging available for `DomainSetup` which runs later.
**Migration**: No action needed; the Serilog code stays in `NjordHost.Configure(builder)`.
