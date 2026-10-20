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
The host's `CoreSetupContainer` SHALL register `TimeProvider.System` and the Prometheus meter adapter limited to the meters declared by the feature libraries' `Telemetry` classes. Options, the config writer and the `ConfigManager` registration SHALL be done by `ConfigSetupContainer`; the health options and the host-level health checks by `HealthSetupContainer` (each feature registers its own check).

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

### Requirement: BudgetSetupContainer registers the budget tracker
`BudgetSetupContainer` (src/Njord.Budget/Configuration) SHALL register an `IActorRegistration` that creates `BudgetTracker` as a cluster singleton under `IBudgetTracker`, wrapped in a backoff-on-failure supervisor (3 s to 30 s, unlimited retries).

#### Scenario: Budget tracker is a supervised singleton
- **WHEN** the actor system starts
- **THEN** `BudgetTracker` runs as a singleton resolvable through `IBudgetTracker`

### Requirement: GrpcSetupContainer registers only gRPC framework services
`GrpcSetupContainer` (src/Njord.Grpc/Configuration) SHALL call `AddGrpc()` and SHALL register no actors; the gRPC services resolve actors through the `ActorRegistry` markers.

#### Scenario: No actor registration in the gRPC library
- **WHEN** `GrpcSetupContainer.SetupServices` is called
- **THEN** it registers no `IActorRegistration`

### Requirement: HealthSetupContainer registers the health options and the host-level checks
`HealthSetupContainer` SHALL bind and validate `HealthOptions` (`Njord:Health`), add request timeouts, and register the `journal` check (failure status `Unhealthy`) and the `data-directory` check (failure status `Degraded`). It SHALL NOT register a shared health state or telemetry gauges; the `pipeline`, `budget` and `mqtt-connection` checks are registered by their feature containers.

#### Scenario: Checks are registered with their failure status
- **WHEN** the host's services are built with MQTT disabled
- **THEN** the registered checks are `pipeline`, `budget`, `journal` and `data-directory`, with `Unhealthy` as failure status except `data-directory`, which is `Degraded`

#### Scenario: Thresholds bind from the shipped defaults
- **WHEN** `HealthOptions` is resolved
- **THEN** it holds the defaults of the `Njord:Health` section in `appsettings.json`

### Requirement: BudgetSetupContainer registers the budget usage state, gauges and check
`BudgetSetupContainer` SHALL register the `BudgetUsageState`, the observable budget gauges (reading the usage state and the effective budget from `IOptions<PollingOptions>`) and the `budget` health check (failure status `Unhealthy`).

#### Scenario: Gauges follow the usage and the configured budget
- **WHEN** the usage state receives new values and the budget override changes
- **THEN** the gauges report the new used values and the new effective limits

#### Scenario: Budget check is registered
- **WHEN** the host's services are built
- **THEN** a health check named `budget` is registered

