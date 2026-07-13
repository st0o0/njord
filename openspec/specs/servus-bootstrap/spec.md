# servus-bootstrap Specification

## Purpose

Structured startup using Servus setup containers: service DI registrations and Akka.NET actor system configuration are encapsulated in dedicated container classes, keeping Program.cs minimal.

## Requirements

### Requirement: Service setup container registers all non-actor DI services
The system SHALL provide a `NjordServiceSetup` implementing
`IServiceSetupContainer` that registers options binding (`NjordOptions`,
`EnrichmentOptions`), options validation, `ParameterRegistry` resolution,
`TimeProvider`, ingest services, and egress services when
`SetupServices(IServiceCollection, IConfiguration)` is called.

#### Scenario: Options are bound through the setup container
- **WHEN** `NjordServiceSetup.SetupServices` is called with a valid configuration
- **THEN** `NjordOptions` and `EnrichmentOptions` are bound and available via DI
  with `ValidateOnStart` enabled

#### Scenario: Ingest and egress registrations are present
- **WHEN** `NjordServiceSetup.SetupServices` is called
- **THEN** `IOpenMeteoClient`, `IMqttConnection`, and `IMqttTransport` are
  resolvable from the service provider

### Requirement: Actor system setup container configures Akka.NET
The system SHALL provide a `NjordActorSystemSetup` extending
`ActorSystemSetupContainer` that configures persistence HOCON and registers
all top-level actors via `WithResolvableActors`.

#### Scenario: Actor system name is "njord"
- **WHEN** the actor system setup container builds the system
- **THEN** the actor system is named `njord`

#### Scenario: All top-level actors are registered
- **WHEN** the actor system is started
- **THEN** `MqttEgressActor`, `PipelineActor`, `SchedulerActor`, and
  `EnrichmentActor` are resolvable from `IActorRegistry`

#### Scenario: Persistence HOCON is applied
- **WHEN** the actor system setup container builds the system
- **THEN** the journal and snapshot-store plugins are configured with the
  persistence path from `NjordOptions`

### Requirement: Program.cs delegates to setup containers
The `Program.cs` entry point SHALL instantiate `NjordServiceSetup` and
`NjordActorSystemSetup`, call their `SetupServices` methods on the
`WebApplicationBuilder.Services`, and retain inline only the health-check
middleware registration.

#### Scenario: Minimal Program.cs
- **WHEN** the application is started
- **THEN** `Program.cs` contains no inline options binding, no HOCON strings,
  and no `.WithActors` callbacks

### Requirement: Top-level actors use WithResolvableActors
All top-level actors (`MqttEgressActor`, `PipelineActor`, `SchedulerActor`,
`EnrichmentActor`) SHALL be registered via `WithResolvableActors` with their
conventional actor names, replacing the manual
`registry.Register<T>(system.ActorOf(resolver.Props<T>(), name))` pattern.

#### Scenario: Actor names are preserved
- **WHEN** actors are registered via `WithResolvableActors`
- **THEN** the actor paths remain `/user/mqtt-egress`, `/user/pipeline`,
  `/user/scheduler`, `/user/enrichment`
