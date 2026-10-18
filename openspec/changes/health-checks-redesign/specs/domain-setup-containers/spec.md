## ADDED Requirements

### Requirement: HealthSetupContainer registers the health options and the host-level checks
`HealthSetupContainer` SHALL bind and validate `HealthOptions` (`Njord:Health`), add request timeouts, and register the `journal` check (failure status `Unhealthy`) and the `data-directory` check (failure status `Degraded`). It SHALL NOT register a shared health state or telemetry gauges; the `pipeline`, `budget` and `mqtt-connection` checks are registered by their feature containers.

#### Scenario: Checks are registered with their failure status
- **WHEN** the host's services are built with MQTT disabled
- **THEN** the registered checks are `pipeline`, `budget`, `journal` and `data-directory`, with `Unhealthy` as failure status except `data-directory`, which is `Degraded`

#### Scenario: Thresholds bind from the shipped defaults
- **WHEN** `HealthOptions` is resolved
- **THEN** it holds the defaults of the `Njord:Health` section in `appsettings.json`

## MODIFIED Requirements

### Requirement: CoreSetupContainer registers shared services
The host's `CoreSetupContainer` SHALL register `TimeProvider.System` and the Prometheus meter adapter limited to the meters declared by the feature libraries' `Telemetry` classes. Options, the config writer and the `ConfigManager` registration SHALL be done by `ConfigSetupContainer`; the health options and the host-level health checks by `HealthSetupContainer` (each feature registers its own check).

#### Scenario: TimeProvider is registered exactly once
- **WHEN** the host's services are built
- **THEN** exactly one `TimeProvider` registration exists, `TimeProvider.System`, added by `CoreSetupContainer`

#### Scenario: Options are bound and validated
- **WHEN** the host's services are built
- **THEN** `NjordOptions`, `PersistenceOptions` and `StorageOptions` are bound with `ValidateOnStart` enabled, by `ConfigSetupContainer`

#### Scenario: Meter allow-list follows the telemetry classes
- **WHEN** a feature library declares a new `Telemetry` meter name
- **THEN** it is exported without editing a hard-coded list in the host

### Requirement: BudgetSetupContainer registers the budget gauges
`BudgetSetupContainer` SHALL register the `BudgetUsageState`, the observable budget gauges (reading the usage state and the effective budget from `ConfigView`) and the `budget` health check.

#### Scenario: Gauge follows the usage and the configured budget
- **WHEN** the usage state receives new values and the budget override changes
- **THEN** the gauges report the new used values and the new effective limits


## REMOVED Requirements

### Requirement: HealthSetupContainer registers the monitor and the health checks
**Reason**: The shared `NjordHealthMonitor` is removed; the container now registers health options and host-level checks.
**Migration**: See "HealthSetupContainer registers the health options and the host-level checks".
