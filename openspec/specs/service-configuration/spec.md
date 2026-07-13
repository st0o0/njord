# service-configuration Specification

## Purpose

Configuration and startup validation for the service: Open-Meteo free-tier request budget defaults with an optional override, monthly budget projection guards, minimal viable configuration defaults, validated MQTT connection settings, and configurable forecast horizons.

## Requirements

### Requirement: The host is a WebApplication
The service SHALL use `WebApplication.CreateBuilder` as its host builder,
providing Kestrel and the ASP.NET middleware pipeline. DI registrations and
Akka.NET actor system configuration SHALL be delegated to Servus
`IServiceSetupContainer` implementations called from `Program.cs`. The
health-check endpoint at `/healthz` SHALL remain inline.

#### Scenario: Health middleware is registered
- **WHEN** the service starts
- **THEN** the middleware pipeline includes the health-check endpoint at
  `/healthz`

#### Scenario: Actor registration uses WithResolvableActors
- **WHEN** the service starts
- **THEN** `MqttEgressActor`, `PipelineActor`, `SchedulerActor`, and
  `EnrichmentActor` are registered in the Akka actor system via
  `WithResolvableActors`

### Requirement: Actors resolve peers via typed extensions
Actors that need references to other actors SHALL use
`Context.GetActor<T>()` from Servus.Akka instead of injecting and
querying `ActorRegistry` directly. The `ActorRegistry` constructor
parameter SHALL be removed from all actors.

#### Scenario: PipelineActor resolves SchedulerActor
- **WHEN** `PipelineActor` needs the scheduler actor reference
- **THEN** it calls `Context.GetActor<SchedulerActor>()`

#### Scenario: MqttEgressActor resolves PipelineActor
- **WHEN** `MqttEgressActor` needs the pipeline actor reference
- **THEN** it calls `Context.GetActor<PipelineActor>()`

#### Scenario: EnrichmentActor resolves peers
- **WHEN** `EnrichmentActor` needs pipeline and egress actor references
- **THEN** it calls `Context.GetActor<PipelineActor>()` and
  `Context.GetActor<MqttEgressActor>()`

### Requirement: Child actors use DI-aware creation
Child actors created by other actors SHALL use
`Context.ResolveChildActor<T>(name, args)` from Servus.Akka instead of
`Props.Create(() => new T(...))`, so that DI services are available to the
child actor.

#### Scenario: ForecastHistoryActor created via ResolveChildActor
- **WHEN** `EnrichmentActor` creates a `ForecastHistoryActor` for a location
- **THEN** it uses `Context.ResolveChildActor<ForecastHistoryActor>(name, location)`
  and the actor receives DI-resolved services plus the location argument

### Requirement: Request budget defaults to the Open-Meteo free tier
The system SHALL resolve a request budget of 300,000 requests/month and
600 requests/minute (Open-Meteo free-tier soft limits) when no explicit
budget is configured. All throttling and validation SHALL consume the
resolved budget.

#### Scenario: Default budget without configuration
- **WHEN** no budget is configured
- **THEN** the resolved budget is 300,000 requests/month and
  600 requests/minute

### Requirement: Budget override supersedes the preset
The system SHALL accept an optional budget override (requests/month,
requests/minute) that replaces the default free-tier values entirely, so users
can self-throttle below the soft limits.

#### Scenario: Override wins over default
- **WHEN** an override of 50,000 requests/month and 60 requests/minute is
  configured
- **THEN** the resolved budget is 50,000 requests/month and
  60 requests/minute

### Requirement: Parameter groups are configured
The system SHALL accept a `Parameters` options section with `Groups` (list of group names, default `["Weather"]`), `Extra` (list of individual variable API names to add, default empty), and `Exclude` (list of individual variable API names to remove, default empty). The resolved parameter set SHALL be computed at startup and remain fixed for the process lifetime.

#### Scenario: Default parameter configuration
- **WHEN** no `Parameters` section is configured
- **THEN** the effective configuration is `Groups: ["Weather"], Extra: [], Exclude: []`

#### Scenario: Unknown group name is rejected
- **WHEN** configuration specifies `Groups: ["InvalidGroup"]`
- **THEN** startup validation fails naming the unknown group

#### Scenario: Unknown variable in Extra is rejected
- **WHEN** configuration specifies `Extra: ["not_a_real_variable"]`
- **THEN** startup validation fails naming the unknown variable

### Requirement: Startup validation enforces the budget projection
The system SHALL project monthly usage as `locations × models × cycles-per-month × call-weight` where call-weight is `ceil(active-hourly-variable-count / 10)`, and SHALL refuse to start when the projection exceeds 80% of the resolved monthly request budget, reporting the projection, the weight, and the limit in the error.

#### Scenario: Default Weather group passes with weight 3
- **WHEN** 1 location, 8 models, 60-minute poll interval, and the Weather group (~30 hourly variables, weight 3) are configured with the default budget
- **THEN** the projection is ≈ 17,280 effective requests/month and startup proceeds

#### Scenario: All groups active still passes on default budget
- **WHEN** 1 location, 8 models, 60-minute poll interval, and all groups (~50 hourly variables, weight 5) are configured with the default budget
- **THEN** the projection is ≈ 28,800 effective requests/month (within 80% of 300k) and startup proceeds

#### Scenario: Over-budget with high weight is rejected
- **WHEN** 3 locations, 8 models, 30-minute poll interval, and all groups (weight 5) are configured with the default budget
- **THEN** the projection is ≈ 172,800 effective requests/month, exceeding 80% of 300k, and startup fails reporting the projection, weight 5, and the 240,000 guard

### Requirement: Minimal viable configuration is enforced
The system SHALL require at least one location (name, latitude, longitude) and
at least one non-empty model id, and SHALL default the poll interval to
60 minutes when unspecified.

#### Scenario: Empty model list is rejected
- **WHEN** the configuration contains a location but no models
- **THEN** startup validation fails naming the empty model list

#### Scenario: Poll interval defaults
- **WHEN** no poll interval is configured
- **THEN** the effective poll interval is 60 minutes

### Requirement: MQTT connection settings are configured and validated
The system SHALL accept an `Mqtt` options section with `Host` (required),
`Port` (default 1883), optional `Username`/`Password`, `DiscoveryPrefix`
(default `homeassistant`), and `BaseTopic` (default `njord`). Startup
validation SHALL fail when `Host` is missing. The password MUST NOT appear in
logs or validation messages.

#### Scenario: Missing host blocks startup
- **WHEN** the service starts without `Njord:Mqtt:Host`
- **THEN** startup validation fails naming the missing MQTT host

#### Scenario: Defaults apply
- **WHEN** only the host is configured
- **THEN** the effective port is 1883, the discovery prefix is
  `homeassistant`, and the base topic is `njord`

### Requirement: Forecast horizons are configuration
The system SHALL accept a list of forecast horizons in hours (default
`3, 6, 12, 24, 48, 72`) from which the entity grid is derived. Validation
SHALL reject an empty list, non-positive values, and horizons beyond the
fetched forecast window (96 h).

#### Scenario: Horizons default to the six-step ladder
- **WHEN** no horizons are configured
- **THEN** the effective horizons are 3, 6, 12, 24, 48, and 72 hours

#### Scenario: Out-of-window horizon is rejected
- **WHEN** a horizon of 120 hours is configured
- **THEN** startup validation fails naming the 96 h fetch window

### Requirement: Persistence options section is part of NjordOptions
`NjordOptions` SHALL include a `Persistence` property of type `PersistenceOptions` with defaults (`Provider = Sqlite`, `ConnectionString = null`). The existing `PersistencePath` property SHALL remain as the convenience default for SQLite file path.

#### Scenario: Default persistence options
- **WHEN** no `Persistence` section is configured
- **THEN** `NjordOptions.Persistence.Provider` is `Sqlite` and `Persistence.ConnectionString` is null

#### Scenario: PersistencePath coexists with Persistence section
- **WHEN** both `PersistencePath` and `Persistence:Provider` are configured
- **THEN** both values are available; `PersistencePath` is used as fallback only when provider is `Sqlite` and no explicit `ConnectionString` is set

### Requirement: Startup validation covers persistence configuration
The `NjordOptionsValidator` SHALL validate the persistence configuration: `Provider` must be a valid `PersistenceProvider` enum value, and `PostgreSql` provider SHALL require a non-empty `ConnectionString`. Validation failure messages SHALL name the specific problem and suggest corrective action.

#### Scenario: Valid SQLite config passes validation
- **WHEN** provider is `Sqlite` with default settings
- **THEN** validation succeeds

#### Scenario: PostgreSQL without connection string fails validation
- **WHEN** provider is `PostgreSql` and `ConnectionString` is null or empty
- **THEN** validation fails with message indicating PostgreSQL requires `Njord:Persistence:ConnectionString`

#### Scenario: Valid PostgreSQL config passes validation
- **WHEN** provider is `PostgreSql` and `ConnectionString` is non-empty
- **THEN** validation succeeds
