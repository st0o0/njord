# health-checks Specification

## Purpose

Health check implementations for the njord service: shared health state
written by actors and read by ASP.NET health checks, MQTT connection check,
and pipeline freshness check. All checks use `TimeProvider` for testability.
## Requirements
### Requirement: Health checks use TimeProvider
All health checks SHALL use `TimeProvider` (from DI) for time comparisons,
never `DateTime.UtcNow` directly.

#### Scenario: TimeProvider is used for elapsed time
- **WHEN** a health check computes elapsed time since last poll
- **THEN** it uses `TimeProvider.GetUtcNow()` as the reference time

### Requirement: Health checks are feature-owned probes with an explicit failure status
Each health check SHALL be a sealed `IHealthCheck` declared in the project that owns the dependency, registered by that project's setup container with an explicit `failureStatus`, and SHALL delegate its decision to an `internal static` method over plain values (state record, `TimeProvider` reading, options) so that it is testable without hosting. Essential dependencies (journal, pipeline freshness, exhausted budget) SHALL fail as `Unhealthy`; optional ones (MQTT within the grace period, data directory, low budget) SHALL fail as `Degraded`. A check SHALL NOT send actor messages.

#### Scenario: Evaluate without hosting
- **WHEN** a spec calls the static evaluate method of a check with a state record and a fake time
- **THEN** it receives the expected status and description without a host or actor system

#### Scenario: Check registration
- **WHEN** the host's services are built with MQTT disabled
- **THEN** the registered checks are `pipeline`, `journal`, `data-directory` and `budget`, and no MQTT check

### Requirement: Journal health check
A `journal` check SHALL report `Unhealthy` when the SQLite database directory is not writable or does not exist, or when, for PostgreSQL, a connection cannot be opened within 3 seconds, and `Healthy` otherwise, with the resolved path or host in the description.

#### Scenario: Unwritable SQLite directory
- **WHEN** the configured persistence path points to a directory that does not exist
- **THEN** the check reports `Unhealthy` naming the full path

#### Scenario: Writable SQLite directory
- **WHEN** the directory is writable
- **THEN** the check reports `Healthy`

### Requirement: Data directory health check
A `data-directory` check SHALL report `Degraded` when the directory that holds the override file is not writable, because configuration changes could not be persisted, and `Healthy` otherwise.

#### Scenario: Read-only data directory
- **WHEN** the override file directory is read-only
- **THEN** the check reports `Degraded` and `/healthz` still answers 200

### Requirement: Budget health check
A `budget` check SHALL compute the effective budget from the current configuration at check time and compare it with the usage reported by the budget state: `Healthy` when more than the configured percentage remains, `Degraded` below `Njord:Health:BudgetDegradedBelowPercent` (default 5 %), `Unhealthy` when the monthly or daily allowance is exhausted.

#### Scenario: Low budget
- **WHEN** 96 % of the monthly allowance is used
- **THEN** the check reports `Degraded`

#### Scenario: Hot budget change
- **WHEN** the budget override is raised through the config API
- **THEN** the next check evaluation uses the new limit without any restart

### Requirement: Health thresholds are options
The thresholds of the pipeline and MQTT checks and the budget percentage SHALL come from `Njord:Health` (`PollOverdueFactor`, `PollStalledFactor`, `MqttUnhealthyAfter`, `BudgetDegradedBelowPercent`) with defaults in `appsettings.json` and validation (factors greater than 1, factors ordered, durations positive).

#### Scenario: Custom stalled factor
- **WHEN** `PollStalledFactor` is set to 4 and the last poll was 3.5 intervals ago
- **THEN** the pipeline check reports `Degraded`, not `Unhealthy`

### Requirement: Endpoints
`/healthz` SHALL answer 200 for Healthy and Degraded and 503 for Unhealthy within a 5-second request timeout; `/alive` SHALL answer 200 without evaluating any check. No other health endpoint SHALL be served.

#### Scenario: Liveness stays unconditional
- **WHEN** the journal check is Unhealthy
- **THEN** `/alive` still answers 200 and `/healthz` answers 503

### Requirement: MQTT connection health check (feature-owned)
An `MqttConnectionHealthCheck` SHALL be registered (as `mqtt-connection`) only when `Njord:Mqtt:Enabled` is `true`. It SHALL read `MqttConnectionState` and report `Healthy` when connected, `Degraded` when not yet connected or disconnected for less than `Njord:Health:MqttUnhealthyAfter` (default 2 minutes), and `Unhealthy` at or beyond that duration. When MQTT is disabled, no MQTT health check SHALL be registered.

#### Scenario: Connected reports Healthy
- **WHEN** MQTT is enabled and the connection state is connected
- **THEN** the health check returns `Healthy`

#### Scenario: Recently disconnected reports Degraded
- **WHEN** MQTT is enabled and the connection has been down for 90 seconds
- **THEN** the health check returns `Degraded`

#### Scenario: Long disconnect reports Unhealthy
- **WHEN** MQTT is enabled and the connection has been down for 3 minutes
- **THEN** the health check returns `Unhealthy`

#### Scenario: No MQTT health check when disabled
- **WHEN** `Njord:Mqtt:Enabled` is `false`
- **THEN** no `MqttConnectionHealthCheck` is registered and `/healthz` does not report MQTT status

### Requirement: Pipeline health check (feature-owned)
A `PipelineHealthCheck` in `Njord.Pipeline` SHALL read `PollHealthState` and the poll interval from the current configuration and report: `Healthy` when the time since the last successful poll is below `PollOverdueFactor` (default 2) times the interval, `Degraded` between that and `PollStalledFactor` (default 3) times the interval, `Unhealthy` beyond it. It SHALL report `Healthy` when no poll has completed and the service has been running for less than `PollOverdueFactor` times the interval.

#### Scenario: Recent poll reports Healthy
- **WHEN** the poll interval is 60 minutes and the last successful poll was 45 minutes ago
- **THEN** the health check returns `Healthy`

#### Scenario: Overdue poll reports Degraded
- **WHEN** the poll interval is 60 minutes and the last successful poll was 150 minutes ago
- **THEN** the health check returns `Degraded`

#### Scenario: Stalled pipeline reports Unhealthy
- **WHEN** the poll interval is 60 minutes and the last successful poll was 200 minutes ago
- **THEN** the health check returns `Unhealthy`

#### Scenario: Startup grace period
- **WHEN** no poll has ever completed and the service started 30 minutes ago with a 60-minute poll interval
- **THEN** the health check returns `Healthy`

#### Scenario: No poll after the grace period
- **WHEN** no poll has ever completed and the service started 2 hours ago with a 60-minute poll interval
- **THEN** the health check returns `Unhealthy`

