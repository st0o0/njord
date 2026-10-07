# servus-bootstrap Specification

## Purpose

Structured startup using Servus setup containers: service DI registrations and Akka.NET actor system configuration are encapsulated in domain-specific container classes, keeping Program.cs minimal.

## Requirements

### Requirement: Service setup container registers all non-actor DI services
The system SHALL distribute DI registrations across domain-specific `IServiceSetupContainer` implementations instead of a single `NjordServiceSetup`. Each domain container SHALL own its options binding, service registrations, and health checks. The monolithic `NjordServiceSetup` SHALL be removed.

#### Scenario: Options are bound through domain containers
- **WHEN** the application starts
- **THEN** `NjordOptions` and `SensorOptions` are bound via `CoreSetupContainer`, and `EnrichmentOptions` via `EnrichmentSetupContainer`, each with `ValidateOnStart` enabled

#### Scenario: Ingest and egress registrations are present
- **WHEN** the application starts
- **THEN** `IOpenMeteoClient` is resolvable (via `IngestSetupContainer`), and `IMqttConnection`/`IMqttTransport` are resolvable when MQTT is enabled (via `MqttSetupContainer`)

### Requirement: Actor system setup container configures Akka.NET
The system SHALL provide an `AkkaSetupContainer` extending `ActorSystemSetupContainer` that configures persistence, cluster formation, remoting, and logging infrastructure. Domain actors SHALL be registered via `IActorRegistration` instances resolved from DI, not directly in this container.

#### Scenario: Actor system name is "njord"
- **WHEN** the actor system setup container builds the system
- **THEN** the actor system is named `njord`

#### Scenario: All top-level actors are registered via IActorRegistration
- **WHEN** the actor system is started
- **THEN** all actors previously registered in `NjordActorSystemSetup` SHALL be registered through `IActorRegistration` implementations from domain containers

#### Scenario: Persistence HOCON is applied
- **WHEN** the actor system setup container builds the system
- **THEN** the journal and snapshot-store plugins are configured with the persistence path from `NjordOptions`

#### Scenario: Cluster forms via SeedNodes before singletons start
- **WHEN** the actor system setup container builds the system
- **THEN** `WithClustering` is called with `ClusterOptions.SeedNodes` containing the node's own address, so the cluster begins forming during actor system startup — before singleton proxies start their lookup timers

#### Scenario: Remoting uses a fixed port
- **WHEN** the actor system setup container configures remoting
- **THEN** `WithRemoting` uses a fixed port (2552) instead of an ephemeral port (0), because `SeedNodes` requires a known address

#### Scenario: No manual cluster join
- **WHEN** the actor system is started
- **THEN** there is no `cluster.Join(selfAddress)` call in a `WithActors` callback — the `SeedNodes` configuration handles cluster formation

#### Scenario: Stream shutdown task is registered independently
- **WHEN** the actor system setup container registers actors
- **THEN** `AddStreamShutdownTask` is registered via the `PipelineSetupContainer`'s `IActorRegistration`, not directly in the `AkkaSetupContainer`

### Requirement: Program.cs delegates to setup containers
The `Program.cs` entry point SHALL chain domain-specific setup containers via `AppBuilder.WithSetup<T>()`. It SHALL NOT contain a monolithic `NjordServiceSetup`.

#### Scenario: Domain containers in Program.cs
- **WHEN** the application is started
- **THEN** `Program.cs` chains `CoreSetupContainer`, `IngestSetupContainer`, `SensorSetupContainer`, `PipelineSetupContainer`, `EgressSetupContainer`, `EnrichmentSetupContainer`, `MqttSetupContainer`, `GrpcSetupContainer`, `AkkaSetupContainer`, and `NjordApplicationSetup`

### Requirement: Top-level actors use WithResolvableActors
All singleton actors SHALL be registered via `WithResolvableActors` or `WithClusterSingleton` within their domain's `IActorRegistration`. ShardRegion actors SHALL be registered via `WithShardRegion`.

#### Scenario: Actor names are preserved
- **WHEN** actors are registered via domain `IActorRegistration` implementations
- **THEN** the actor paths SHALL remain unchanged from their current values
