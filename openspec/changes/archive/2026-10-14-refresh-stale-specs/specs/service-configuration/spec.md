## ADDED Requirements

### Requirement: The host is a WebApplication composed of setup containers
The service SHALL use `WebApplication.CreateBuilder` as its host builder, providing Kestrel and the ASP.NET middleware pipeline. DI registrations and Akka.NET actor system configuration SHALL be delegated to Servus setup containers chained in `Program.cs`. The health-check endpoint at `/healthz`, the liveness endpoint at `/alive`, the Prometheus metrics endpoint and the gRPC services SHALL be mapped in `NjordApplicationSetup`. Serilog SHALL be configured directly in `Program.cs`. There SHALL be no dependency on a separate `ServiceDefaults` project.

#### Scenario: Health middleware is registered
- **WHEN** the service starts
- **THEN** the middleware pipeline includes the health-check endpoint at `/healthz` and the liveness endpoint at `/alive`

#### Scenario: Actors are registered through setup containers
- **WHEN** the service starts
- **THEN** `PollScheduler`, `ForecastPipeline`, `ForecastReadModel`, `OpenMeteoGateway`, `BudgetTracker`, `SensorHub` and `ConfigManager` are created through the setup containers' `IActorRegistration`s, plus the MQTT actors when `Njord:Mqtt:Enabled` is true

#### Scenario: Serilog is configured without ServiceDefaults
- **WHEN** the service starts
- **THEN** Serilog is configured directly in `Program.cs` without referencing a `ServiceDefaults` project

### Requirement: Actors resolve peers via marker interfaces
Actors that need references to other actors SHALL resolve them through the Servus.Akka extensions keyed by the marker interfaces in `Njord.Core/Actors/ActorKeys.cs` — `Context.GetActor<TMarker>()` synchronously or `Context.GetActorAsync<TMarker>()` piped to `Self` — instead of injecting `ActorRegistry` or `IActorRef` constructor parameters. A registry lookup SHALL never be keyed by an actor class.

#### Scenario: PollScheduler resolves the gateway
- **WHEN** `PollScheduler` is constructed
- **THEN** it calls `Context.GetActor<IForecastGateway>()`

#### Scenario: MqttStatePublisher resolves the pipeline
- **WHEN** `MqttStatePublisher` starts
- **THEN** it calls `Context.GetActor<IForecastPipeline>()`

#### Scenario: ForecastPipeline resolves the gateway asynchronously
- **WHEN** `ForecastPipeline` starts or loses the gateway
- **THEN** it calls `Context.GetActorAsync<IForecastGateway>()` and pipes the result to itself

### Requirement: Persistence options are their own configuration sections
Persistence settings SHALL NOT be part of `NjordOptions`. `PersistenceOptions` SHALL bind the `Njord:Persistence` section (`Provider` default `Sqlite`, `ConnectionString` default null) and `StorageOptions` SHALL bind `PersistencePath` (default `data/njord.db`) from the `Njord` section; both are registered by `CoreSetupContainer`.

#### Scenario: Default persistence options
- **WHEN** no `Persistence` section is configured
- **THEN** `PersistenceOptions.Provider` is `Sqlite` and `PersistenceOptions.ConnectionString` is null

#### Scenario: PersistencePath coexists with Persistence section
- **WHEN** both `Njord:PersistencePath` and `Njord:Persistence:Provider` are configured
- **THEN** both values are available; `PersistencePath` is used only when the provider is `Sqlite` and no explicit `ConnectionString` is set

### Requirement: Each options type has its own validator
Every options type SHALL have exactly one validator implementing `IValidateOptions<T>` for that type, in the same folder: `NjordOptionsValidator` (forecast scope), `PollingOptionsValidator`, `PersistenceOptionsValidator`, `StorageOptionsValidator`, `OpenMeteoOptionsValidator`, `MqttOptionsValidator`, `SensorOptionsValidator`, `ConsensusOptionsValidator`, `AlertOptionsValidator`, `DerivedOptionsValidator`, `TrendOptionsValidator` and `IndexOptionsValidator`. No validator SHALL validate another type's options through `NjordOptions`.

#### Scenario: IndexOptionsValidator validates IndexOptions
- **WHEN** `IndexOptionsValidator` validates index preferences
- **THEN** it implements `IValidateOptions<IndexOptions>` and receives `IndexOptions` directly

#### Scenario: SensorOptionsValidator validates through SensorOptions
- **WHEN** `SensorOptionsValidator` validates sensor configuration
- **THEN** it receives `SensorOptions` directly, not `NjordOptions`

### Requirement: Each options type and its validator live in their own file
Each `*Options` type and each `*OptionsValidator` in `Njord.Core/Configuration/` SHALL live in its own file named after the type.

#### Scenario: AlertOptions in own file
- **WHEN** `Alerts/AlertOptions.cs` is inspected
- **THEN** it contains only the `AlertOptions` class, and `AlertOptionsValidator` lives in `Alerts/AlertOptionsValidator.cs`

## MODIFIED Requirements

### Requirement: Startup validates configuration
`NjordOptionsValidator` SHALL validate the forecast scope at startup: at least one location, a non-empty `Name` for every location, at least one non-empty model id, horizons within 1–96 hours, `ForecastDays` within 1–16, resolvable parameter groups and variables, and model coverage per location. Timezone validation SHALL NOT be performed — all timestamps are UTC and `LocationOptions` has no timezone property.

#### Scenario: Valid location without timezone passes validation
- **WHEN** a location has `Name: "Lucerne"`, `Latitude: 47.05`, `Longitude: 8.31` and no `Timezone` property
- **THEN** validation SHALL succeed

#### Scenario: Location with leftover Timezone property passes validation
- **WHEN** a location config still contains a `Timezone` property from a previous configuration format
- **THEN** validation SHALL succeed — the property is ignored by the binder since it does not exist on `LocationOptions`

### Requirement: Startup validation covers persistence configuration
`PersistenceOptionsValidator` SHALL validate the persistence configuration at startup: the `PostgreSql` provider SHALL require a non-empty `ConnectionString`. The failure message SHALL name the problem and the setting to fix.

#### Scenario: Valid SQLite config passes validation
- **WHEN** provider is `Sqlite` with default settings
- **THEN** validation succeeds

#### Scenario: PostgreSQL without connection string fails validation
- **WHEN** provider is `PostgreSql` and `ConnectionString` is null or empty
- **THEN** validation fails with message indicating PostgreSQL requires `Njord:Persistence:ConnectionString`

#### Scenario: Valid PostgreSQL config passes validation
- **WHEN** provider is `PostgreSql` and `ConnectionString` is non-empty
- **THEN** validation succeeds

### Requirement: AlertThresholdOptions renamed to AlertOptions
The alert options type SHALL be named `AlertOptions` (not `AlertThresholdOptions`) and SHALL bind the `Njord:Enrichment:Alerts` section.

#### Scenario: AlertOptions type name

- **WHEN** alert configuration is accessed
- **THEN** the type SHALL be `AlertOptions`, not `AlertThresholdOptions`

### Requirement: BudgetValidator renamed to BudgetCalculator
The static class that projects and resolves the request budget SHALL be named `BudgetCalculator` (not `BudgetValidator`). It SHALL expose `GetEffectiveBudget(PollingOptions)` returning the resolved `RequestBudget` (`BudgetOverride` ?? free-tier default).

#### Scenario: BudgetCalculator provides effective budget

- **WHEN** the system needs the effective request budget
- **THEN** it SHALL call `BudgetCalculator.GetEffectiveBudget(pollingOptions)`; no options type exposes an `EffectiveBudget` property

## REMOVED Requirements

### Requirement: The host is a WebApplication
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "The host is a WebApplication composed of setup containers" in this capability.

### Requirement: Actors resolve peers via typed extensions
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Actors resolve peers via marker interfaces" in this capability.

### Requirement: Child actors use DI-aware creation
**Reason**: No actor creates child actors any more; `EnrichmentActor` and `ForecastHistoryActor` were removed and nothing calls `ResolveChildActor`.
**Migration**: None.

### Requirement: Persistence options section is part of NjordOptions
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Persistence options are their own configuration sections" in this capability.

### Requirement: Validators implement IValidateOptions of their own sub-option, except cross-cutting and Mqtt
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Each options type has its own validator" in this capability.

### Requirement: One type per configuration file
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Each options type and its validator live in their own file" in this capability.
