## RENAMED Requirements

### FROM: domain-setup-containers
### TO: domain-setups

## MODIFIED Requirements

### Requirement: IActorRegistration interface for domain actor declarations
The system SHALL define an `IActorRegistration` interface with a method that accepts an `AkkaConfigurationBuilder` and `IServiceProvider`. Domain setups SHALL register `IActorRegistration` implementations in DI during their `Setup` call. The central `AkkaSetup` SHALL resolve all `IActorRegistration` instances from the service provider and invoke them during `BuildSystem`.

#### Scenario: Domain setup registers its actors via IActorRegistration
- **WHEN** a domain setup's `Setup` is called
- **THEN** it SHALL register an `IActorRegistration` implementation that configures its actors on `AkkaConfigurationBuilder`

#### Scenario: AkkaSetup resolves and executes all registrations
- **WHEN** `AkkaSetup.BuildSystem` is called
- **THEN** it SHALL resolve all `IActorRegistration` instances from `IServiceProvider` and invoke each one on the builder

#### Scenario: Registration order is deterministic
- **WHEN** multiple `IActorRegistration` implementations are registered
- **THEN** they SHALL be invoked in the order they were registered in DI (matching the setup chain order in `NjordHost.Configure`)

### Requirement: CoreSetup registers shared services
The host's `CoreSetup` SHALL register `TimeProvider.System`. `CoreSetup` SHALL NOT register the Prometheus meter adapter or meter filter. Options and the `ConfigManager` registration SHALL be done by `ConfigSetup`; the health options and the host-level health checks by `HealthSetup`.

#### Scenario: TimeProvider is registered exactly once
- **WHEN** the host's services are built
- **THEN** exactly one `TimeProvider` registration exists, `TimeProvider.System`, added by `CoreSetup`

#### Scenario: No meter configuration in CoreSetup
- **WHEN** `CoreSetup.Setup` is called
- **THEN** it SHALL NOT call `Metrics.ConfigureMeterAdapter` or reference any `Telemetry.MeterName`

#### Scenario: FeatureRequirementsOptions is registered by DomainSetup
- **WHEN** the host's services are built
- **THEN** exactly one `FeatureRequirementsOptions` is registered via `AddOptions<FeatureRequirementsOptions>().Configure<>().ValidateOnStart()`, by `DomainSetup`

#### Scenario: No FetchPlan or forwarding registrations
- **WHEN** the host's services are built
- **THEN** no `FetchPlan`, `EffectiveWeatherModels`, `IReadOnlySet<DomainKind>`, or `ServiceInfo` singleton registrations SHALL exist

#### Scenario: Options are bound and validated
- **WHEN** the host's services are built
- **THEN** `NjordOptions` and `PersistenceOptions` are bound with `ValidateOnStart` enabled, by `ConfigSetup`

#### Scenario: ConfigSetup has no ServiceInfo lambda
- **WHEN** the host's services are built
- **THEN** `ConfigSetup` SHALL register only options classes, not factory lambdas for derived types

### Requirement: AkkaSetup configures only infrastructure
The system SHALL provide an `AkkaSetup` extending `Terp.Akka.ActorSystemSetup` that configures logging via `AddLoggerFactory()`, persistence (SQLite/PostgreSQL) with health checks on journal and snapshot stores, remoting, clustering, actor system liveness, and cluster readiness. It SHALL resolve all `IActorRegistration` instances from DI and invoke them. It SHALL NOT directly register any domain actors.

#### Scenario: No domain actors in AkkaSetup
- **WHEN** `AkkaSetup.BuildSystem` is called
- **THEN** it SHALL contain no direct `WithClusterSingleton`, `WithShardRegion`, or `WithResolvableActors` calls for domain actors -- only `IActorRegistration` invocations

#### Scenario: Persistence stores have health checks
- **WHEN** `AkkaSetup.BuildSystem` is called
- **THEN** `WithSqlPersistence` is invoked with `journalBuilder` calling `WithHealthCheck()` and `snapshotBuilder` calling `WithHealthCheck()`

#### Scenario: Actor system liveness check is registered
- **WHEN** the actor system starts
- **THEN** `WithActorSystemLivenessCheck()` is registered on the builder

#### Scenario: Cluster readiness check is registered
- **WHEN** the actor system starts
- **THEN** `WithAkkaClusterReadinessCheck()` is registered on the builder

#### Scenario: Akka logs use AddLoggerFactory
- **WHEN** `AkkaSetup.BuildSystem` configures logging
- **THEN** it SHALL call `ClearLoggers()` and `AddLoggerFactory()`, not a custom logger class

### Requirement: OpenMeteoSetup registers the client and the gateway
`OpenMeteoSetup` (src/Njord.OpenMeteo/Configuration) SHALL bind and validate domain options, register `IBudgetProvider` (`OptionsBudgetProvider`), register the fetch timeouts unless already registered, register each domain client as a typed `HttpClient` with the domain-specific base address, IPv4-only socket connect callback, HTTP fetch timeout and standard resilience handler, collect them into an `IReadOnlyDictionary<DomainKind, IDomainClient>`, and register an `IActorRegistration` that creates `OpenMeteoGateway` as a cluster singleton under `IForecastGateway`, wrapped in a backoff-on-failure supervisor (3 s to 30 s, unlimited retries).

#### Scenario: Clients depend on FeatureRequirementsOptions
- **WHEN** a domain client is constructed
- **THEN** it SHALL receive `IOptions<FeatureRequirementsOptions>` and extract its domain's requirements from it

#### Scenario: OpenMeteo clients are resolvable
- **WHEN** `OpenMeteoSetup.Setup` is called
- **THEN** all six `IDomainClient` implementations are resolvable from the service provider

#### Scenario: Gateway is a supervised singleton
- **WHEN** the actor system starts
- **THEN** `OpenMeteoGateway` runs as a singleton resolvable through `IForecastGateway`

### Requirement: BudgetSetup registers the budget tracker
`BudgetSetup` (src/Njord.Budget/Configuration) SHALL register an `IActorRegistration` that creates `BudgetTracker` as a cluster singleton under `IBudgetTracker`, wrapped in a backoff-on-failure supervisor (3 s to 30 s, unlimited retries).

#### Scenario: Budget tracker is a supervised singleton
- **WHEN** the actor system starts
- **THEN** `BudgetTracker` runs as a singleton resolvable through `IBudgetTracker`

### Requirement: GrpcSetup registers only gRPC framework services
`GrpcSetup` (src/Njord.Grpc/Configuration) SHALL call `AddGrpc()` and SHALL register no actors; the gRPC services resolve actors through the `ActorRegistry` markers.

#### Scenario: No actor registration in the gRPC library
- **WHEN** `GrpcSetup.Setup` is called
- **THEN** it registers no `IActorRegistration`

### Requirement: HealthSetup registers the health options and the host-level checks
`HealthSetup` SHALL bind and validate `HealthOptions` (`Njord:Health`), add request timeouts, and register the `journal` check (failure status `Unhealthy`) and the `data-directory` check (failure status `Degraded`).

#### Scenario: Checks are registered with their failure status
- **WHEN** the host's services are built
- **THEN** the registered checks include `pipeline`, `budget`, `journal` and `data-directory`, with `Unhealthy` as failure status except `data-directory`, which is `Degraded`

#### Scenario: Thresholds bind from the shipped defaults
- **WHEN** `HealthOptions` is resolved
- **THEN** it holds the defaults of the `Njord:Health` section in `appsettings.json`

### Requirement: NjordApplicationSetup configures the HTTP pipeline
`NjordApplicationSetup` SHALL implement `ISetup<WebApplication>`. Its `Setup` method SHALL call the static `Configure(WebApplication)` method that maps health endpoints, gRPC services, and the Prometheus metrics endpoint. The static `Configure` and `ExposeHealthEndpoints` methods SHALL remain `internal static` for testability.

#### Scenario: Health and gRPC endpoints are mapped
- **WHEN** `NjordApplicationSetup.Setup` is called
- **THEN** `/healthz`, `/alive`, gRPC services, and the Prometheus metrics endpoint are mapped

## REMOVED Requirements

### Requirement: MqttSetupContainer registers MQTT services and actors conditionally
**Reason**: MQTT was removed from the project; no `MqttSetupContainer` exists.
**Migration**: No action needed.

### Requirement: SupervisorDefaults centralizes backoff parameters
**Reason**: Never implemented; backoff constants remain local per setup class as in FunkArr convention.
**Migration**: No action needed.
