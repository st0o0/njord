## MODIFIED Requirements

### Requirement: CoreSetupContainer registers shared services
The host's `CoreSetupContainer` SHALL register `TimeProvider.System` and configure Kestrel's `MinResponseDataRate` to `null` (for long-lived gRPC streams). CoreSetupContainer SHALL NOT register the Prometheus meter adapter or meter filter -- that responsibility moves to `TelemetrySetupContainer`.

#### Scenario: TimeProvider is registered exactly once
- **WHEN** the host's services are built
- **THEN** exactly one `TimeProvider` registration exists, `TimeProvider.System`, added by `CoreSetupContainer`

#### Scenario: Kestrel MinResponseDataRate is configured
- **WHEN** the host's services are built
- **THEN** Kestrel's `MinResponseDataRate` is set to `null` by `CoreSetupContainer`

#### Scenario: No meter configuration in CoreSetupContainer
- **WHEN** `CoreSetupContainer.SetupServices` is called
- **THEN** it SHALL NOT call `Metrics.ConfigureMeterAdapter` or reference any `Telemetry.MeterName`

### Requirement: AkkaSetupContainer configures only infrastructure
The system SHALL provide an `AkkaSetupContainer` extending `ActorSystemSetupContainer` that configures logging via `AddLoggerFactory()`, persistence (SQLite/PostgreSQL) with health checks on journal and snapshot stores, remoting, clustering, actor system liveness, and cluster readiness. It SHALL resolve all `IActorRegistration` instances from DI and invoke them. It SHALL NOT directly register any domain actors.

#### Scenario: No domain actors in AkkaSetupContainer
- **WHEN** `AkkaSetupContainer.BuildSystem` is called
- **THEN** it SHALL contain no direct `WithClusterSingleton`, `WithShardRegion`, or `WithResolvableActors` calls for domain actors -- only `IActorRegistration` invocations

#### Scenario: Persistence stores have health checks
- **WHEN** `AkkaSetupContainer.BuildSystem` is called
- **THEN** `WithSqlPersistence` is invoked with `journalBuilder` calling `WithHealthCheck()` and `snapshotBuilder` calling `WithHealthCheck()`

#### Scenario: Actor system liveness check is registered
- **WHEN** the actor system starts
- **THEN** `WithActorSystemLivenessCheck()` is registered on the builder

#### Scenario: Cluster readiness check is registered
- **WHEN** the actor system starts
- **THEN** `WithAkkaClusterReadinessCheck()` is registered on the builder

#### Scenario: Akka logs use AddLoggerFactory
- **WHEN** `AkkaSetupContainer.BuildSystem` configures logging
- **THEN** it SHALL call `ClearLoggers()` and `AddLoggerFactory()`, not a custom logger class

## ADDED Requirements

### Requirement: LoggingSetupContainer configures Serilog
A `LoggingSetupContainer : IServiceSetupContainer` in the host SHALL configure Serilog as the `ILogger` provider: `ReadFrom.Configuration`, machine name and thread id enrichers, `FromLogContext`, and the console sink with the `[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}` template. It SHALL clear the default logging providers. `NjordHost.Create` SHALL NOT contain any Serilog or logging configuration inline.

#### Scenario: Serilog is configured by LoggingSetupContainer
- **WHEN** `LoggingSetupContainer.SetupServices` is called
- **THEN** Serilog is registered via `AddSerilog` with ReadFrom.Configuration, enrichers, and the console sink

#### Scenario: NjordHost has no inline logging config
- **WHEN** `NjordHost.Create` is called
- **THEN** it SHALL NOT call `AddSerilog`, `builder.Logging.ClearProviders()`, or configure any logging -- that is done by `LoggingSetupContainer`

### Requirement: TelemetrySetupContainer configures metrics export
A `TelemetrySetupContainer : IServiceSetupContainer` in the host SHALL configure the Prometheus meter adapter with an instrument filter limited to the meters declared by the feature libraries' `Telemetry` classes. The meter name set SHALL be a public static field on `TelemetrySetupContainer` (moved from `CoreSetupContainer`).

#### Scenario: Meter adapter is configured by TelemetrySetupContainer
- **WHEN** `TelemetrySetupContainer.SetupServices` is called
- **THEN** `Metrics.ConfigureMeterAdapter` is called with a filter predicate that accepts only the njord meter names

#### Scenario: Meter allow-list follows the telemetry classes
- **WHEN** a feature library declares a new `Telemetry` meter name
- **THEN** it is exported by adding it to `TelemetrySetupContainer.NjordMeters`

### Requirement: SupervisorDefaults centralizes backoff parameters
A `SupervisorDefaults` static class in `Njord.Core/Actors/` SHALL declare `MinBackoff` (3 seconds), `MaxBackoff` (30 seconds), and `RandomFactor` (0.2) as public static readonly fields. All `IActorRegistration` implementations that create BackoffOnFailure supervisors SHALL use these shared constants instead of local copies.

#### Scenario: All supervised actors use the same defaults
- **WHEN** any `IActorRegistration` creates a BackoffOnFailure supervisor
- **THEN** it SHALL reference `SupervisorDefaults.MinBackoff`, `SupervisorDefaults.MaxBackoff`, and `SupervisorDefaults.RandomFactor`

#### Scenario: No local backoff constants in library containers
- **WHEN** scanning all `IActorRegistration` implementations
- **THEN** none SHALL declare local `MinBackoff`, `MaxBackoff`, or `RandomFactor` fields

### Requirement: NjordHost.Create is a pure composition chain
`NjordHost.Create` SHALL contain only the `AppBuilder.Create` call and the `WithSetup<>` chain. All service configuration (logging, Kestrel, telemetry, options) SHALL be in setup containers. The `build` delegate parameter for integration tests SHALL be preserved.

#### Scenario: NjordHost has no inline service configuration
- **WHEN** reading `NjordHost.Create`
- **THEN** it SHALL contain no `builder.Services.*`, `builder.Logging.*`, `builder.WebHost.*`, or `builder.Host.*` calls -- only `AppBuilder.Create(...).WithSetup<>()...Build()`

#### Scenario: Integration test hook is preserved
- **WHEN** integration tests call `NjordHost.Create`
- **THEN** the optional `build` delegate parameter is available and works as before

## REMOVED Requirements

### Requirement: Meter allow-list follows the telemetry classes
**Reason**: Moved from `CoreSetupContainer` to the new `TelemetrySetupContainer`. The behavior is identical; only the container that hosts it changes. See the ADDED requirement on `TelemetrySetupContainer`.
**Migration**: No code migration needed; the meter names set moves from `CoreSetupContainer.NjordMeters` to `TelemetrySetupContainer.NjordMeters`.
