## MODIFIED Requirements

### Requirement: OpenMeteoSetupContainer registers the client and the gateway
`OpenMeteoSetupContainer` (src/Njord.OpenMeteo/Configuration) SHALL bind and validate
`OpenMeteoOptions`, register `IBudgetProvider` (`OptionsBudgetProvider`), register the fetch
timeouts (admission 60 s, HTTP 30 s, reply 120 s) unless already registered, register each
domain client as a typed `HttpClient` with the domain-specific base address, IPv4-only socket
connect callback, HTTP fetch timeout (30 seconds) and standard resilience handler, collect
them into an `IReadOnlyDictionary<DomainKind, IDomainClient>`, and register an
`IActorRegistration` that creates `OpenMeteoGateway` as a cluster singleton under
`IForecastGateway`, wrapped in a backoff-on-failure supervisor (3 s to 30 s, unlimited
retries). Clients SHALL receive their API variable lists from `FetchPlan` instead of
`ResolvedRequirements`.

#### Scenario: OpenMeteo clients are resolvable
- **WHEN** `OpenMeteoSetupContainer.SetupServices` is called
- **THEN** all six `IDomainClient` implementations are resolvable from the service provider
  with IPv4-forced socket handling

#### Scenario: Gateway is a supervised singleton
- **WHEN** the actor system starts
- **THEN** `OpenMeteoGateway` runs as a singleton resolvable through `IForecastGateway`

#### Scenario: Clients depend on FetchPlan
- **WHEN** a domain client is constructed
- **THEN** it SHALL receive `FetchPlan` and extract its domain's requirements from it

### Requirement: CoreSetupContainer registers shared services
The host's `CoreSetupContainer` SHALL register `TimeProvider.System`, the Prometheus meter
adapter limited to the meters declared by the feature libraries' `Telemetry` classes, and
`FetchPlan` as a singleton built from the enabled feature rules and the relevant options.
Options, the config writer and the `ConfigManager` registration SHALL be done by
`ConfigSetupContainer`; the health options and the host-level health checks by
`HealthSetupContainer` (each feature registers its own check).

#### Scenario: TimeProvider is registered exactly once
- **WHEN** the host's services are built
- **THEN** exactly one `TimeProvider` registration exists, `TimeProvider.System`, added by `CoreSetupContainer`

#### Scenario: FetchPlan is registered as a singleton
- **WHEN** the host's services are built
- **THEN** exactly one `FetchPlan` is registered, computed from enabled features and options

#### Scenario: No separate derived registrations
- **WHEN** the host's services are built
- **THEN** no `IReadOnlySet<DomainKind>`, `EffectiveWeatherModels` or `ResolvedRequirements`
  singleton registrations SHALL exist

#### Scenario: Options are bound and validated
- **WHEN** the host's services are built
- **THEN** `NjordOptions`, `PersistenceOptions` and `StorageOptions` are bound with `ValidateOnStart` enabled, by `ConfigSetupContainer`

#### Scenario: Meter allow-list follows the telemetry classes
- **WHEN** a feature library declares a new `Telemetry` meter name
- **THEN** it is exported without editing a hard-coded list in the host
