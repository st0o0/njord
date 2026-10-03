# service-configuration Delta Specification (admin-ui)

## MODIFIED Requirements

### Requirement: The host is a WebApplication
The service SHALL use `WebApplication.CreateBuilder` as its host builder,
providing Kestrel and the ASP.NET middleware pipeline. DI registrations and
Akka.NET actor system configuration SHALL be delegated to Servus
`IServiceSetupContainer` implementations called from `Program.cs`. The
health-check endpoint at `/healthz` SHALL remain inline. The middleware pipeline
SHALL additionally include `UseStaticFiles()` for serving the admin SPA from
`wwwroot/`, a SPA fallback route for client-side routing, and minimal API
endpoint mappings under `/api/*` for the admin API.

#### Scenario: Health middleware is registered
- **WHEN** the service starts
- **THEN** the middleware pipeline includes the health-check endpoint at
  `/healthz`

#### Scenario: Actor registration uses WithResolvableActors
- **WHEN** the service starts
- **THEN** `MqttEgressActor`, `PipelineActor`, `SchedulerActor`, and
  `EnrichmentActor` are registered in the Akka actor system via
  `WithResolvableActors`

#### Scenario: Static files served from wwwroot
- **WHEN** the service starts
- **THEN** requests for files in `wwwroot/` are served via `UseStaticFiles()`

#### Scenario: SPA fallback for client-side routes
- **WHEN** a request arrives for a path not matching a static file or `/api/*` or `/healthz` or `/alive`
- **THEN** the response is `wwwroot/index.html`

#### Scenario: Admin API endpoints mapped
- **WHEN** the service starts
- **THEN** minimal API endpoints under `/api/config`, `/api/stats`, and `/api/health` are mapped

### Requirement: Parameter groups are configured
The system SHALL accept a `Parameters` options section with `Groups` (list of group names, default `["Weather"]`), `Extra` (list of individual variable API names to add, default empty), and `Exclude` (list of individual variable API names to remove, default empty). The resolved parameter set SHALL be computed at startup and SHALL be refreshable at runtime when options change. The `ResolvedParameterSet` SHALL be accessible via a factory or `IOptionsMonitor`-based service rather than a fixed singleton.

#### Scenario: Default parameter configuration
- **WHEN** no `Parameters` section is configured
- **THEN** the effective configuration is `Groups: ["Weather"], Extra: [], Exclude: []`

#### Scenario: Unknown group name is rejected
- **WHEN** configuration specifies `Groups: ["InvalidGroup"]`
- **THEN** startup validation fails naming the unknown group

#### Scenario: Unknown variable in Extra is rejected
- **WHEN** configuration specifies `Extra: ["not_a_real_variable"]`
- **THEN** startup validation fails naming the unknown variable

#### Scenario: Parameter set refreshed on config change
- **WHEN** the `Parameters` section is updated via the admin API
- **THEN** the resolved parameter set is recomputed and consumers see the updated set

## ADDED Requirements

### Requirement: WritableMemoryConfigurationSource is registered in the provider chain
The service SHALL register a `WritableMemoryConfigurationSource` as the last configuration source via `builder.Configuration.Add()` before building the application. The resulting `WritableMemoryConfigurationProvider` instance SHALL be registered as a singleton in DI.

#### Scenario: Writable provider added last
- **WHEN** the service starts
- **THEN** the `WritableMemoryConfigurationProvider` is the last provider in `IConfigurationRoot.Providers`

#### Scenario: Provider available via DI
- **WHEN** a service requests `WritableMemoryConfigurationProvider` from DI
- **THEN** the same instance used in the configuration chain is returned

### Requirement: NjordOptionsValidator is reusable at runtime
The validation logic in `NjordOptionsValidator` SHALL be extractable into a static or instance method that can be called from the `ConfigChangeCoordinator` and admin API endpoints at runtime, not only during startup. The `IValidateOptions<NjordOptions>` implementation SHALL delegate to this shared logic.

#### Scenario: Runtime validation before config apply
- **WHEN** the admin API receives a config update
- **THEN** it can call the same validation logic used at startup to check the new config

#### Scenario: Startup validation unchanged
- **WHEN** the service starts
- **THEN** `ValidateOnStart` still runs the same validation rules as before
