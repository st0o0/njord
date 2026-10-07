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
The system SHALL provide a `CoreSetupContainer` implementing `IServiceSetupContainer` that registers `NjordOptions` binding and validation, `SensorOptions` binding and validation, `ParameterRegistry`, `TimeProvider.System`, `NjordHealthState`, budget metrics, `ConfigPersistence`, and the Prometheus meter adapter.

#### Scenario: Options are bound and validated
- **WHEN** `CoreSetupContainer.SetupServices` is called
- **THEN** `NjordOptions` and `SensorOptions` SHALL be bound with `ValidateOnStart` enabled

#### Scenario: TimeProvider is registered exactly once
- **WHEN** `CoreSetupContainer.SetupServices` is called
- **THEN** `TimeProvider.System` SHALL be registered via `TryAddSingleton` (no duplicate)

### Requirement: IngestSetupContainer registers ingest services
The system SHALL provide an `IngestSetupContainer` implementing `IServiceSetupContainer` that registers `IOpenMeteoClient` with its HTTP client configuration.

#### Scenario: OpenMeteoClient is resolvable
- **WHEN** `IngestSetupContainer.SetupServices` is called
- **THEN** `IOpenMeteoClient` SHALL be resolvable from the service provider with IPv4-forced socket handling

### Requirement: PipelineSetupContainer registers pipeline services and actors
The system SHALL provide a `PipelineSetupContainer` implementing `IServiceSetupContainer` that registers pipeline DI services (`IBudgetProvider`, `IBudgetGate`) and an `IActorRegistration` for `SchedulerActor` (BackoffSupervisor singleton), `BudgetTrackerActor` (BackoffSupervisor singleton), `PipelineActor` (singleton), and the `CoordinatedShutdown` stream-stop task.

#### Scenario: Pipeline actors are declared via IActorRegistration
- **WHEN** `PipelineSetupContainer.SetupServices` is called
- **THEN** it SHALL register an `IActorRegistration` that configures `SchedulerActor`, `BudgetTrackerActor`, `PipelineActor`, and the shutdown task

#### Scenario: Pipeline health check is registered
- **WHEN** `PipelineSetupContainer.SetupServices` is called
- **THEN** `PipelineHealthCheck` SHALL be registered

### Requirement: EgressSetupContainer registers egress actors
The system SHALL provide an `EgressSetupContainer` implementing `IServiceSetupContainer` that registers an `IActorRegistration` for `ModelStateActor` (singleton) and `ForecastSnapshotActor` (ShardRegion via `IForecastSnapshotRegion`).

#### Scenario: ForecastSnapshotActor is registered as ShardRegion
- **WHEN** `EgressSetupContainer`'s `IActorRegistration` is invoked
- **THEN** `ForecastSnapshotActor` SHALL be registered as a ShardRegion under `IForecastSnapshotRegion` with passivation timeout

### Requirement: EnrichmentSetupContainer registers enrichment services and actors
The system SHALL provide an `EnrichmentSetupContainer` implementing `IServiceSetupContainer` that registers enrichment options, validators, computers, `IEnrichmentFeature` implementations, and an `IActorRegistration` for `EnrichmentActor` (singleton), `EnrichmentSnapshotActor` (ShardRegion via `IEnrichmentSnapshotRegion`), and `ForecastHistoryActor` (ShardRegion via `IForecastHistoryRegion`).

#### Scenario: EnrichmentSnapshotActor is registered as ShardRegion
- **WHEN** `EnrichmentSetupContainer`'s `IActorRegistration` is invoked
- **THEN** `EnrichmentSnapshotActor` SHALL be registered as a ShardRegion under `IEnrichmentSnapshotRegion` with passivation timeout

### Requirement: MqttSetupContainer registers MQTT services and actors conditionally
The system SHALL provide an `MqttSetupContainer` implementing `IServiceSetupContainer` that registers `MqttOptions` binding, `IEnrichmentPresenter` implementations, and conditionally (when MQTT is enabled) the MQTT transport services and an `IActorRegistration` for `MqttConnectionActor`, `MqttStateActor`, and `MqttDiscoveryActor`.

#### Scenario: MQTT actors are only registered when enabled
- **WHEN** `MqttSetupContainer.SetupServices` is called with MQTT disabled
- **THEN** no `IActorRegistration` for MQTT actors SHALL be registered

#### Scenario: MQTT health check is conditional
- **WHEN** `MqttSetupContainer.SetupServices` is called with MQTT enabled
- **THEN** `MqttConnectionHealthCheck` SHALL be registered

### Requirement: GrpcSetupContainer registers gRPC services and actors
The system SHALL provide a `GrpcSetupContainer` implementing `IServiceSetupContainer` that registers gRPC framework services and an `IActorRegistration` for `GrpcSnapshotConsumerActor` (singleton).

#### Scenario: GrpcSnapshotConsumerActor is declared via IActorRegistration
- **WHEN** `GrpcSetupContainer.SetupServices` is called
- **THEN** it SHALL register an `IActorRegistration` that configures `GrpcSnapshotConsumerActor`

### Requirement: SensorSetupContainer registers sensor services and actors
The system SHALL provide a `SensorSetupContainer` implementing `IServiceSetupContainer` that registers an `IActorRegistration` for `SensorHubActor` (singleton).

#### Scenario: SensorHubActor is declared via IActorRegistration
- **WHEN** `SensorSetupContainer.SetupServices` is called
- **THEN** it SHALL register an `IActorRegistration` that configures `SensorHubActor`

### Requirement: AkkaSetupContainer configures only infrastructure
The system SHALL provide an `AkkaSetupContainer` extending `ActorSystemSetupContainer` that configures logging, persistence (SQLite/PostgreSQL), remoting, and clustering. It SHALL resolve all `IActorRegistration` instances from DI and invoke them. It SHALL NOT directly register any domain actors.

#### Scenario: No domain actors in AkkaSetupContainer
- **WHEN** `AkkaSetupContainer.BuildSystem` is called
- **THEN** it SHALL contain no direct `WithClusterSingleton`, `WithShardRegion`, or `WithResolvableActors` calls for domain actors — only `IActorRegistration` invocations
