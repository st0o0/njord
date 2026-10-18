## MODIFIED Requirements

### Requirement: CoreSetupContainer registers shared services
The host's `CoreSetupContainer` SHALL register `TimeProvider.System` and the Prometheus meter adapter limited to the meters declared by the feature libraries' `Telemetry` classes. Options, the config writer and the `ConfigManager` registration SHALL be done by `ConfigSetupContainer`; the health monitor and the health checks by `HealthSetupContainer`.

#### Scenario: TimeProvider is registered exactly once
- **WHEN** the host's services are built
- **THEN** exactly one `TimeProvider` registration exists, `TimeProvider.System`, added by `CoreSetupContainer`

#### Scenario: Meter allow-list follows the telemetry classes
- **WHEN** a feature library declares a new `Telemetry` meter name
- **THEN** it is exported without editing a hard-coded list in the host

## ADDED Requirements

### Requirement: ConfigSetupContainer registers options, the config writer and the ConfigManager
`ConfigSetupContainer` SHALL bind and validate (`ValidateOnStart`) `NjordOptions`, `PersistenceOptions` and `StorageOptions` with their validators, register the `ResolvedParameterSet` through `ParametersOptions.Resolve()`, `ConfigView`, the `IConfigWriter` for `data/appsettings.Override.json` and an `IActorRegistration` creating the `ConfigManager` under `IConfigManager`.

#### Scenario: Options are bound and validated
- **WHEN** `ConfigSetupContainer.SetupServices` is called
- **THEN** `NjordOptions`, `PersistenceOptions` and `StorageOptions` are bound with `ValidateOnStart` enabled

### Requirement: HealthSetupContainer registers the monitor and the health checks
`HealthSetupContainer` SHALL register the `NjordHealthMonitor` created with the service start time and the initial budget snapshot from `PollingOptions.ToInitialBudgetSnapshot()`, and the `pipeline` health check. It SHALL NOT register telemetry gauges.

#### Scenario: Monitor starts with the configured budget
- **WHEN** the monitor is resolved
- **THEN** its budget limits equal the effective budget of the configured polling options and its used counters are zero

### Requirement: BudgetSetupContainer registers the budget gauges
`BudgetSetupContainer` SHALL register the observable budget gauges, reading their values from the `NjordHealthMonitor` it resolves from DI.

#### Scenario: Gauge follows the monitor
- **WHEN** the monitor receives a new budget snapshot
- **THEN** the gauges report the snapshot's daily and monthly used and limit values
