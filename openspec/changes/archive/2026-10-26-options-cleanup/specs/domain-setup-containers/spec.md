## MODIFIED Requirements

### Requirement: CoreSetupContainer registers shared services
The host's `CoreSetupContainer` SHALL register `TimeProvider.System` and the Prometheus meter
adapter limited to the meters declared by the feature libraries' `Telemetry` classes.
`CoreSetupContainer` SHALL NOT register `FetchPlan`; the resolved feature requirements are
registered by `DomainSetupContainer` as `FeatureRequirementsOptions` through the OptionsBuilder
pattern. Options and the `ConfigManager` registration SHALL be done by `ConfigSetupContainer`;
the health options and the host-level health checks by `HealthSetupContainer`.

#### Scenario: TimeProvider is registered exactly once
- **WHEN** the host's services are built
- **THEN** exactly one `TimeProvider` registration exists, `TimeProvider.System`, added by `CoreSetupContainer`

#### Scenario: FeatureRequirementsOptions is registered by DomainSetupContainer
- **WHEN** the host's services are built
- **THEN** exactly one `FeatureRequirementsOptions` is registered via `AddOptions<FeatureRequirementsOptions>().Configure<>().ValidateOnStart()`, by `DomainSetupContainer`

#### Scenario: No FetchPlan or forwarding registrations
- **WHEN** the host's services are built
- **THEN** no `FetchPlan`, `EffectiveWeatherModels`, `IReadOnlySet<DomainKind>`, or `ServiceInfo` singleton registrations SHALL exist

#### Scenario: Options are bound and validated
- **WHEN** the host's services are built
- **THEN** `NjordOptions` and `PersistenceOptions` are bound with `ValidateOnStart` enabled, by `ConfigSetupContainer`

#### Scenario: ConfigSetupContainer has no ServiceInfo lambda
- **WHEN** the host's services are built
- **THEN** `ConfigSetupContainer` SHALL register only options classes, not factory lambdas for derived types

#### Scenario: Meter allow-list follows the telemetry classes
- **WHEN** a feature library declares a new `Telemetry` meter name
- **THEN** it is exported without editing a hard-coded list in the host

### Requirement: OpenMeteoSetupContainer registers the client and the gateway
`OpenMeteoSetupContainer` SHALL bind and validate domain options,
register `IBudgetProvider`, fetch timeouts, each domain client as a typed `HttpClient`,
collect them into an `IReadOnlyDictionary<DomainKind, IDomainClient>`, and register an
`IActorRegistration` for the gateway singleton. Clients SHALL receive their API variable
lists from `IOptions<FeatureRequirementsOptions>` instead of `FetchPlan`.

#### Scenario: Clients depend on FeatureRequirementsOptions
- **WHEN** a domain client is constructed
- **THEN** it SHALL receive `IOptions<FeatureRequirementsOptions>` and extract its domain's requirements from it

#### Scenario: OpenMeteo clients are resolvable
- **WHEN** `OpenMeteoSetupContainer.SetupServices` is called
- **THEN** all six `IDomainClient` implementations are resolvable from the service provider

#### Scenario: Gateway is a supervised singleton
- **WHEN** the actor system starts
- **THEN** `OpenMeteoGateway` runs as a singleton resolvable through `IForecastGateway`
