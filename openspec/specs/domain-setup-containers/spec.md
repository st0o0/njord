# domain-setup-containers Specification

## Purpose

Domain-specific Servus `IServiceSetupContainer` implementations with an `IActorRegistration` DI pattern so each domain assembly declares its own services and actors.
## Requirements
### Requirement: IActorRegistration interface for domain actor declarations
The system SHALL define an `IActorRegistration` interface with a method that accepts an `AkkaConfigurationBuilder` and `IServiceProvider`. Domain setup containers SHALL register `IActorRegistration` implementations in DI during their `SetupServices` call. The central `AkkaSetupContainer` SHALL resolve all `IActorRegistration` instances from the service provider and invoke them during `BuildSystem`.

#### Scenario: Domain container registers its actors via IActorRegistration
- **WHEN** a domain setup container's `SetupServices` is called
- **THEN** it SHALL register an `IActorRegistration` implementation that configures its actors on `AkkaConfigurationBuilder`

#### Scenario: AkkaSetupContainer resolves and executes all registrations
- **WHEN** `AkkaSetupContainer.BuildSystem` is called
- **THEN** it SHALL resolve all `IActorRegistration` instances from `IServiceProvider` and invoke each one on the builder

#### Scenario: Registration order is deterministic
- **WHEN** multiple `IActorRegistration` implementations are registered
- **THEN** they SHALL be invoked in the order they were registered in DI (matching the container chain order in Program.cs)

### Requirement: CoreSetupContainer registers shared services
The host's `CoreSetupContainer` SHALL register `TimeProvider.System` and the Prometheus meter adapter limited to the meters declared by the feature libraries' `Telemetry` classes. Options, the config writer and the `ConfigManager` registration SHALL be done by `ConfigSetupContainer`; the health monitor and the health checks by `HealthSetupContainer`.

#### Scenario: TimeProvider is registered exactly once
- **WHEN** the host's services are built
- **THEN** exactly one `TimeProvider` registration exists, `TimeProvider.System`, added by `CoreSetupContainer`

#### Scenario: Options are bound and validated
- **WHEN** the host's services are built
- **THEN** `NjordOptions`, `PersistenceOptions` and `StorageOptions` are bound with `ValidateOnStart` enabled, by `ConfigSetupContainer`

#### Scenario: Meter allow-list follows the telemetry classes
- **WHEN** a feature library declares a new `Telemetry` meter name
- **THEN** it is exported without editing a hard-coded list in the host

### Requirement: MqttSetupContainer registers MQTT services and actors conditionally
`MqttSetupContainer` SHALL bind and validate `MqttOptions` and, only when `Njord:Mqtt:Enabled` is `true`, register `MqttEgressTuning`, `MqttNetPublisher` (also as `IMqttConnection` and `IMqttTransport`) and an `IActorRegistration` that creates the `MqttConnection` (`IMqttConnectionActor`) and `MqttStatePublisher` (`IMqttStatePublisher`) singletons.

#### Scenario: MQTT actors are only registered when enabled
- **WHEN** `MqttSetupContainer.SetupServices` is called with MQTT disabled
- **THEN** no `IActorRegistration` for MQTT actors is registered

#### Scenario: MQTT health check is conditional
- **WHEN** the host starts with MQTT enabled
- **THEN** `CoreSetupContainer` registers the `mqtt-connection` health check (`MqttConnectionHealthCheck`), and it is absent when MQTT is disabled

### Requirement: AkkaSetupContainer configures only infrastructure
The system SHALL provide an `AkkaSetupContainer` extending `ActorSystemSetupContainer` that configures logging, persistence (SQLite/PostgreSQL), remoting, and clustering. It SHALL resolve all `IActorRegistration` instances from DI and invoke them. It SHALL NOT directly register any domain actors.

#### Scenario: No domain actors in AkkaSetupContainer
- **WHEN** `AkkaSetupContainer.BuildSystem` is called
- **THEN** it SHALL contain no direct `WithClusterSingleton`, `WithShardRegion`, or `WithResolvableActors` calls for domain actors — only `IActorRegistration` invocations

### Requirement: OpenMeteoSetupContainer registers the client and the gateway
`OpenMeteoSetupContainer` (src/Njord.OpenMeteo/Configuration) SHALL bind and validate `OpenMeteoOptions`, register `IBudgetProvider` (`OptionsBudgetProvider`), register the fetch timeouts (admission 60 s, HTTP 30 s, reply 120 s) unless already registered, register `IOpenMeteoClient` as a typed `HttpClient` whose base address comes from `OpenMeteoOptions.OpenMeteoBaseUrl`, with the HTTP fetch timeout (30 seconds) and an IPv4-only socket connect callback, and register an `IActorRegistration` that creates `OpenMeteoGateway` as a cluster singleton under `IForecastGateway`, wrapped in a backoff-on-failure supervisor (3 s to 30 s, unlimited retries).

#### Scenario: OpenMeteoClient is resolvable
- **WHEN** `OpenMeteoSetupContainer.SetupServices` is called
- **THEN** `IOpenMeteoClient` is resolvable from the service provider with IPv4-forced socket handling

#### Scenario: Gateway is a supervised singleton
- **WHEN** the actor system starts
- **THEN** `OpenMeteoGateway` runs as a singleton resolvable through `IForecastGateway`

### Requirement: PipelineSetupContainer registers scheduling, pipeline and read-model actors
`PipelineSetupContainer` (src/Njord.Pipeline/Configuration) SHALL bind and validate `PollingOptions` and `ConsensusOptions`, register the startup `AnalysisSettings` built by `AnalysisSettingsFactory` from the validated options, register the fetch timeouts unless already registered, and register an `IActorRegistration` that creates the poll shard region `poll-worker` of `PollWorker` entities under `IPollRegion` (shared `ShardMessageExtractor`, no passivation, no remember-entities) before three cluster singletons: `PollManager` under `IPollManager` (singleton name `poll-manager`, inside a backoff-on-failure supervisor, 3 s to 30 s, unlimited retries), `ForecastPipeline` under `IForecastPipeline` (inside a backoff-on-failure supervisor, 3 s to 30 s, unlimited retries) and `ForecastReadModel` under `IForecastReadModel`. It SHALL NOT register a CoordinatedShutdown stream-stop task.

#### Scenario: Pipeline actors are declared via IActorRegistration
- **WHEN** `PipelineSetupContainer.SetupServices` is called
- **THEN** it registers an `IActorRegistration` that configures the poll shard region, `PollManager`, `ForecastPipeline` and `ForecastReadModel`

#### Scenario: Startup analysis settings are registered
- **WHEN** `ForecastPipeline` is created
- **THEN** it receives the `AnalysisSettings` built from the validated analysis options

#### Scenario: Pipeline is a supervised singleton
- **WHEN** the analysis graph of `ForecastPipeline` ends and the pipeline fails
- **THEN** its supervisor restarts it after the backoff delay and it stays resolvable through `IForecastPipeline`

#### Scenario: Polling runs in a single-node cluster
- **WHEN** the polling registration runs in an actor system that forms a single-node cluster with persistence
- **THEN** the manager starts a worker for every configured target in the shard region, each target is fetched, and the manager's `QueryPollStates` reports its last change

### Requirement: BudgetSetupContainer registers the budget tracker
`BudgetSetupContainer` (src/Njord.Budget/Configuration) SHALL register an `IActorRegistration` that creates `BudgetTracker` as a cluster singleton under `IBudgetTracker`, wrapped in a backoff-on-failure supervisor (3 s to 30 s, unlimited retries).

#### Scenario: Budget tracker is a supervised singleton
- **WHEN** the actor system starts
- **THEN** `BudgetTracker` runs as a singleton resolvable through `IBudgetTracker`

### Requirement: AnalysisSetupContainer registers the analysis libraries
The host's `AnalysisSetupContainer` SHALL register `ConsensusBuilder` and call `AddAlerts()`, `AddDerived()`, `AddTrends()` and `AddIndices()`. It SHALL register no actors.

#### Scenario: Analyzers are resolvable
- **WHEN** the host's services are built
- **THEN** `AlertAnalyzer`, `DerivedAnalyzer`, `TrendAnalyzer`, `IndexAnalyzer` and `ConsensusBuilder` are resolvable for `ForecastPipeline`

### Requirement: GrpcSetupContainer registers only gRPC framework services
`GrpcSetupContainer` (src/Njord.Grpc/Configuration) SHALL call `AddGrpc()` and SHALL register no actors; the gRPC services resolve actors through the `ActorRegistry` markers.

#### Scenario: No actor registration in the gRPC library
- **WHEN** `GrpcSetupContainer.SetupServices` is called
- **THEN** it registers no `IActorRegistration`

### Requirement: SensorSetupContainer registers sensor options and the SensorHub actor
`SensorSetupContainer` (src/Njord.SensorHub/Configuration) SHALL bind and validate `SensorOptions` and register an `IActorRegistration` that creates `SensorHub` as a cluster singleton under `ISensorHub`.

#### Scenario: SensorHub is declared via IActorRegistration
- **WHEN** `SensorSetupContainer.SetupServices` is called
- **THEN** it registers an `IActorRegistration` that configures `SensorHub`

### Requirement: ConfigSetupContainer registers options, the config writer and the ConfigManager
`ConfigSetupContainer` SHALL bind and validate (`ValidateOnStart`) `NjordOptions`, `PersistenceOptions` and `StorageOptions` with their validators, register the `ResolvedParameterSet` through `ParametersOptions.Resolve()`, `ConfigView`, the `IConfigWriter` for `data/appsettings.Override.json` and an `IActorRegistration` creating the `ConfigManager` under `IConfigManager`.

#### Scenario: Options are bound and validated
- **WHEN** `ConfigSetupContainer.SetupServices` is called
- **THEN** `NjordOptions`, `PersistenceOptions` and `StorageOptions` are bound with `ValidateOnStart` enabled

### Requirement: HealthSetupContainer registers the monitor and the health checks
`HealthSetupContainer` SHALL register the `NjordHealthMonitor` created with the service start time and the initial budget health from `PollingOptions.ToInitialBudgetHealth()`, and the `pipeline` health check. It SHALL NOT register telemetry gauges.

#### Scenario: Monitor starts with the configured budget
- **WHEN** the monitor is resolved
- **THEN** its budget limits equal the effective budget of the configured polling options and its used counters are zero

### Requirement: BudgetSetupContainer registers the budget gauges
`BudgetSetupContainer` SHALL register the observable budget gauges, reading their values from the `NjordHealthMonitor` it resolves from DI.

#### Scenario: Gauge follows the monitor
- **WHEN** the monitor receives a new budget health
- **THEN** the gauges report the health record's daily and monthly used and limit values

