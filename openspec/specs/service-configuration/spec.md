# service-configuration Specification

## Purpose

Configuration and startup validation for the service: Open-Meteo free-tier request budget defaults with an optional override, monthly budget projection guards, minimal viable configuration defaults, validated MQTT connection settings, and configurable forecast horizons.
## Requirements
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

### Requirement: LocationOptions supports per-location model list
The `LocationOptions` class SHALL have an optional `Models` property
(`IList<string>?`) that lists model IDs specific to this location. When
set, these models SHALL be merged with the global `Models` list to produce
the effective model set for this location.

#### Scenario: Location with Models in JSON config
- **WHEN** appsettings.json contains `{ "Name": "berlin", "Models": ["icon_d2"] }`
- **THEN** `LocationOptions.Models` SHALL contain `["icon_d2"]`

#### Scenario: Location without Models in JSON config
- **WHEN** appsettings.json contains `{ "Name": "amsterdam" }` with no
  Models property
- **THEN** `LocationOptions.Models` SHALL be null

### Requirement: Budget calculation accounts for per-location model counts
The startup budget validation SHALL compute projected API usage as the
sum of resolved model counts per location (not global
`locations.Count x models.Count`). Each location may have a different
number of effective models.

#### Scenario: Two locations with different model counts
- **WHEN** global Models has 3 entries, location A adds 1 model, and
  location B adds 2 models
- **THEN** projected requests per cycle SHALL be (3+1) + (3+2) = 9,
  not 2 x 3 = 6

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
The system SHALL accept an `Mqtt` options section with `Enabled` (default `false`),
`Host` (required when `Enabled` is `true`), `Port` (default 1883), optional
`Username`/`Password`, `DiscoveryPrefix` (default `homeassistant`), and `BaseTopic`
(default `njord`). Startup validation SHALL fail when `Enabled` is `true` and `Host`
is missing. Startup validation SHALL NOT fail on a missing `Host` when `Enabled` is
`false`. The password MUST NOT appear in logs or validation messages.

#### Scenario: MQTT is disabled by default
- **WHEN** no `Njord:Mqtt:Enabled` value is configured
- **THEN** the effective value is `false` and no MQTT services or actors are registered

#### Scenario: Missing host blocks startup when MQTT enabled
- **WHEN** the service starts with `Njord:Mqtt:Enabled` as `true` and without `Njord:Mqtt:Host`
- **THEN** startup validation fails naming the missing MQTT host

#### Scenario: Missing host is accepted when MQTT disabled
- **WHEN** the service starts with `Njord:Mqtt:Enabled` as `false` (or default) and without `Njord:Mqtt:Host`
- **THEN** startup validation succeeds

#### Scenario: Defaults apply when MQTT enabled
- **WHEN** `Enabled` is `true` and only the host is configured
- **THEN** the effective port is 1883, the discovery prefix is `homeassistant`, and the base topic is `njord`

### Requirement: Configuration layering follows ASP.NET conventions
The `appsettings.json` file SHALL contain only production-appropriate settings (logging levels). It SHALL NOT contain a `Njord:` section — all option defaults SHALL be defined in the Options classes. The `appsettings.Development.json` file SHALL contain dev-specific overrides (debug logging, test locations, MQTT disabled). It SHALL be loaded automatically when `ASPNETCORE_ENVIRONMENT=Development`.

#### Scenario: Docker image starts with no Njord config
- **WHEN** the service starts in Production environment with no `Njord:` configuration
- **THEN** all Options properties use their code defaults and startup validation rejects the empty locations list with a clear message

#### Scenario: Dev environment loads development overrides
- **WHEN** the service starts with `ASPNETCORE_ENVIRONMENT=Development`
- **THEN** `appsettings.Development.json` is loaded and overrides the production defaults with dev-specific values

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

### Requirement: IndexOptionsValidator does not mutate options

`IndexOptionsValidator` SHALL NOT modify option values during validation. If a sensitivity value is outside the range [0.0, 5.0], the validator SHALL return a validation failure. `PreferenceResolver.ClampSensitivity` handles clamping at resolution time.

#### Scenario: Out-of-range sensitivity fails validation

- **WHEN** `HeatSensitivity` is set to 8.0
- **THEN** validation SHALL fail with a message indicating the value is out of range [0.0, 5.0]

### Requirement: Options types are pure POCOs with no business logic

Options types SHALL contain only properties with defaults. Computed properties (`EffectiveBudget`) and methods (`ResolveModels`) SHALL NOT exist on options types. Budget resolution SHALL be a method on `BudgetCalculator`. Model resolution SHALL be inlined at the call-site.

#### Scenario: NjordOptions has no EffectiveBudget property

- **WHEN** `NjordOptions` is inspected
- **THEN** it SHALL NOT have an `EffectiveBudget` property or any methods

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

### Requirement: PersistenceOptions is a class

`PersistenceOptions` SHALL be a `sealed class`, not a `sealed record`, consistent with all other options types.

#### Scenario: PersistenceOptions consistency

- **WHEN** `PersistenceOptions` is inspected
- **THEN** it SHALL be declared as `sealed class`, not `sealed record`

### Requirement: The host is a WebApplication composed of setup containers
The service SHALL use `WebApplication.CreateBuilder` as its host builder, providing Kestrel and the ASP.NET middleware pipeline. DI registrations and Akka.NET actor system configuration SHALL be delegated to Servus setup containers chained in `Program.cs`. The health-check endpoint at `/healthz`, the liveness endpoint at `/alive`, the Prometheus metrics endpoint and the gRPC services SHALL be mapped in `NjordApplicationSetup`. Serilog SHALL be configured directly in `Program.cs`. There SHALL be no dependency on a separate `ServiceDefaults` project.

#### Scenario: Health middleware is registered
- **WHEN** the service starts
- **THEN** the middleware pipeline includes the health-check endpoint at `/healthz` and the liveness endpoint at `/alive`

#### Scenario: Actors are registered through setup containers
- **WHEN** the service starts
- **THEN** `PollManager`, the poll shard region of `PollWorker`s, `ForecastPipeline`, `ForecastReadModel`, `OpenMeteoGateway`, `BudgetTracker`, `SensorHub` and `ConfigManager` are created through the setup containers' `IActorRegistration`s, plus the MQTT actors when `Njord:Mqtt:Enabled` is true

#### Scenario: Serilog is configured without ServiceDefaults
- **WHEN** the service starts
- **THEN** Serilog is configured directly in `Program.cs` without referencing a `ServiceDefaults` project

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

### Requirement: Actors resolve peers through marker interfaces
Actors that need references to other actors SHALL resolve them through the Servus.Akka extensions keyed by the marker interfaces in `Njord.Core/Actors/ActorKeys.cs` — `Context.GetActor<TMarker>()` or `Context.TryGetActor<TMarker>(out ...)` synchronously, or `Context.GetActorAsync<TMarker>()` piped to `Self` — instead of injecting `ActorRegistry` or `IActorRef` constructor parameters. A registry lookup SHALL never be keyed by an actor class. `ForecastPipeline` SHALL resolve no peer: producers and consumers come to it.

#### Scenario: Poll workers resolve the gateway per poll
- **WHEN** a `PollWorker` polls its target
- **THEN** it calls `Context.TryGetActor<IForecastGateway>(out ...)` for that poll, and the `PollManager` resolves the poll shard region with `Context.TryGetActor<IPollRegion>(out ...)`

#### Scenario: MqttStatePublisher resolves the pipeline
- **WHEN** `MqttStatePublisher` starts
- **THEN** it calls `Context.GetActor<IForecastPipeline>()`

#### Scenario: The gateway resolves the pipeline asynchronously
- **WHEN** `OpenMeteoGateway` starts or loses its pipeline attachment
- **THEN** it calls `Context.GetActorAsync<IForecastPipeline>()` and pipes the result to itself

#### Scenario: The SensorHub pushes to the pipeline marker
- **WHEN** the SensorHub pushes a snapshot map
- **THEN** it resolves the pipeline through `IForecastPipeline` and skips the push while none is registered

