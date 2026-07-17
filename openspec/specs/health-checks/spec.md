# health-checks Specification

## Purpose

Health check implementations for the njord service: shared health state
written by actors and read by ASP.NET health checks, MQTT connection check,
and pipeline freshness check. All checks use `TimeProvider` for testability.

## Requirements

### Requirement: Shared health state is writable by actors
A `NjordHealthState` class SHALL be registered as a DI singleton. Actors
SHALL write to it using thread-safe operations. Health check classes SHALL
read from it. No health check SHALL send messages to actors.

#### Scenario: Actor updates health state
- **WHEN** MqttConnectionActor connects successfully
- **THEN** `NjordHealthState.IsMqttConnected` is set to `true` and
  `MqttConnectedSince` is set to the current UTC time

#### Scenario: Health check reads shared state
- **WHEN** a health check executes
- **THEN** it reads from `NjordHealthState` without sending any actor message

### Requirement: MQTT connection health check
An `MqttConnectionHealthCheck` SHALL be registered only when `Mqtt.Enabled` is
`true`. When registered, it SHALL report health based on the MQTT connection state
and disconnect duration:
- `Healthy` when connected.
- `Degraded` when disconnected for less than 2 minutes.
- `Unhealthy` when disconnected for 2 minutes or more.

When `Mqtt.Enabled` is `false`, no MQTT health check SHALL be registered.

#### Scenario: Connected reports Healthy
- **WHEN** MQTT is enabled and `NjordHealthState.IsMqttConnected` is `true`
- **THEN** the health check returns `Healthy`

#### Scenario: Recently disconnected reports Degraded
- **WHEN** MQTT is enabled and `NjordHealthState.IsMqttConnected` is `false` and the disconnect
  duration is 90 seconds
- **THEN** the health check returns `Degraded`

#### Scenario: Long disconnect reports Unhealthy
- **WHEN** MQTT is enabled and `NjordHealthState.IsMqttConnected` is `false` and the disconnect
  duration is 3 minutes
- **THEN** the health check returns `Unhealthy`

#### Scenario: No MQTT health check when disabled
- **WHEN** `Mqtt.Enabled` is `false`
- **THEN** no `MqttConnectionHealthCheck` is registered and `/healthz` does not report MQTT status

### Requirement: Pipeline health check
A `PipelineHealthCheck` SHALL report health based on the time elapsed since
the last successful poll relative to the configured poll interval:
- `Healthy` when the elapsed time is less than 2x the poll interval.
- `Degraded` when the elapsed time is between 2x and 3x the poll interval.
- `Unhealthy` when the elapsed time exceeds 3x the poll interval.

The check SHALL also report `Healthy` when no poll has ever completed and
the service has been running for less than 2x the poll interval (grace
period for initial startup).

#### Scenario: Recent poll reports Healthy
- **WHEN** the poll interval is 60 minutes and the last successful poll was
  45 minutes ago
- **THEN** the health check returns `Healthy`

#### Scenario: Overdue poll reports Degraded
- **WHEN** the poll interval is 60 minutes and the last successful poll was
  150 minutes ago
- **THEN** the health check returns `Degraded`

#### Scenario: Stalled pipeline reports Unhealthy
- **WHEN** the poll interval is 60 minutes and the last successful poll was
  200 minutes ago
- **THEN** the health check returns `Unhealthy`

#### Scenario: Startup grace period
- **WHEN** no poll has ever completed and the service started 30 minutes ago
  with a 60-minute poll interval
- **THEN** the health check returns `Healthy`

### Requirement: Health checks use TimeProvider
All health checks SHALL use `TimeProvider` (from DI) for time comparisons,
never `DateTime.UtcNow` directly.

#### Scenario: TimeProvider is used for elapsed time
- **WHEN** a health check computes elapsed time since last poll
- **THEN** it uses `TimeProvider.GetUtcNow()` as the reference time
