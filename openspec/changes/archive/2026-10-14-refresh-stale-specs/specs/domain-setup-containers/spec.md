## ADDED Requirements

### Requirement: OpenMeteoSetupContainer registers the client and the gateway
`OpenMeteoSetupContainer` (src/Njord.OpenMeteo/Configuration) SHALL bind and validate `OpenMeteoOptions`, register `IBudgetProvider` (`OptionsBudgetProvider`), register `IOpenMeteoClient` as a typed `HttpClient` whose base address comes from `OpenMeteoOptions.OpenMeteoBaseUrl`, with a 30-second timeout and an IPv4-only socket connect callback, and register an `IActorRegistration` that creates `OpenMeteoGateway` as a cluster singleton under `IForecastGateway`, wrapped in a backoff-on-failure supervisor (3 s to 30 s, unlimited retries).

#### Scenario: OpenMeteoClient is resolvable
- **WHEN** `OpenMeteoSetupContainer.SetupServices` is called
- **THEN** `IOpenMeteoClient` is resolvable from the service provider with IPv4-forced socket handling

#### Scenario: Gateway is a supervised singleton
- **WHEN** the actor system starts
- **THEN** `OpenMeteoGateway` runs as a singleton resolvable through `IForecastGateway`

### Requirement: PipelineSetupContainer registers scheduling, pipeline and read-model actors
`PipelineSetupContainer` (src/Njord.Pipeline/Configuration) SHALL bind and validate `PollingOptions` and `ConsensusOptions`, register the startup `AnalysisSettings` built by `AnalysisSettingsFactory` from the validated options, and register an `IActorRegistration` that creates three cluster singletons: `PollScheduler` under `IPollScheduler` (inside a `BackoffSupervisor`, 3 s to 30 s, unlimited retries), `ForecastPipeline` under `IForecastPipeline` and `ForecastReadModel` under `IForecastReadModel`. It SHALL NOT register a CoordinatedShutdown stream-stop task.

#### Scenario: Pipeline actors are declared via IActorRegistration
- **WHEN** `PipelineSetupContainer.SetupServices` is called
- **THEN** it registers an `IActorRegistration` that configures `PollScheduler`, `ForecastPipeline` and `ForecastReadModel`

#### Scenario: Startup analysis settings are registered
- **WHEN** `ForecastPipeline` is created
- **THEN** it receives the `AnalysisSettings` built from the validated analysis options

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

## MODIFIED Requirements

### Requirement: CoreSetupContainer registers shared services
The host's `CoreSetupContainer` (src/Njord/Configuration/CoreSetupContainer.cs) SHALL bind and validate (`ValidateOnStart`) `NjordOptions`, `PersistenceOptions` and `StorageOptions` with their validators, and SHALL register the `ResolvedParameterSet`, `TimeProvider.System`, the `NjordHealthMonitor` (including the budget observable gauges), the health checks (`pipeline` always, `mqtt-connection` only when MQTT is enabled), `ConfigView`, the `IConfigWriter` that writes `data/appsettings.Override.json`, the Prometheus meter adapter limited to the `Njord.*` meters, and an `IActorRegistration` that creates the `ConfigManager` actor under the `IConfigManager` marker.

#### Scenario: Options are bound and validated
- **WHEN** `CoreSetupContainer.SetupServices` is called
- **THEN** `NjordOptions`, `PersistenceOptions` and `StorageOptions` are bound with `ValidateOnStart` enabled

#### Scenario: TimeProvider is registered exactly once
- **WHEN** the host's services are built
- **THEN** exactly one `TimeProvider` registration exists, `TimeProvider.System`, added by `CoreSetupContainer`

### Requirement: MqttSetupContainer registers MQTT services and actors conditionally
`MqttSetupContainer` SHALL bind and validate `MqttOptions` and, only when `Njord:Mqtt:Enabled` is `true`, register `MqttEgressTuning`, `MqttNetPublisher` (also as `IMqttConnection` and `IMqttTransport`) and an `IActorRegistration` that creates the `MqttConnection` (`IMqttConnectionActor`) and `MqttStatePublisher` (`IMqttStatePublisher`) singletons.

#### Scenario: MQTT actors are only registered when enabled
- **WHEN** `MqttSetupContainer.SetupServices` is called with MQTT disabled
- **THEN** no `IActorRegistration` for MQTT actors is registered

#### Scenario: MQTT health check is conditional
- **WHEN** the host starts with MQTT enabled
- **THEN** `CoreSetupContainer` registers the `mqtt-connection` health check (`MqttConnectionHealthCheck`), and it is absent when MQTT is disabled

## REMOVED Requirements

### Requirement: IngestSetupContainer registers ingest services
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "OpenMeteoSetupContainer registers the client and the gateway" in this capability.

### Requirement: PipelineSetupContainer registers pipeline services and actors
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "PipelineSetupContainer registers scheduling, pipeline and read-model actors" in this capability.

### Requirement: EgressSetupContainer registers egress actors
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "BudgetSetupContainer registers the budget tracker" in this capability.

### Requirement: EnrichmentSetupContainer registers enrichment services and actors
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "AnalysisSetupContainer registers the analysis libraries" in this capability.

### Requirement: GrpcSetupContainer registers gRPC services and actors
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "GrpcSetupContainer registers only gRPC framework services" in this capability.

### Requirement: SensorSetupContainer registers sensor services and actors
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "SensorSetupContainer registers sensor options and the SensorHub actor" in this capability.
