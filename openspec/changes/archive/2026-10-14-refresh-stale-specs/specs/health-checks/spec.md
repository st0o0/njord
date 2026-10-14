## MODIFIED Requirements

### Requirement: Shared health state is writable by actors
A `NjordHealthMonitor` (src/Njord.Core/Health/NjordHealthMonitor.cs) SHALL be registered as a DI singleton. Actors SHALL write to it by replacing whole immutable snapshots (`UpdateMqtt`, `UpdatePoll`, `UpdateBudget`); health check classes SHALL read from it. No health check SHALL send messages to actors.

`NjordHealthMonitor` SHALL expose `Mqtt` (`MqttHealthSnapshot(IsConnected, DisconnectedSince)`), `Poll` (`PollHealthSnapshot(LastSuccessfulPollUtc)`), `Budget` (`BudgetHealthSnapshot(UsedDaily, UsedMonthly, LimitDaily, LimitMonthly)`) and `ServiceStartedUtc`.

On connect, `MqttConnection` SHALL write `MqttHealthSnapshot(true, null)`. There SHALL be no connected-since timestamp.

#### Scenario: Actor updates health state
- **WHEN** `MqttConnection` connects successfully
- **THEN** `NjordHealthMonitor.Mqtt.IsConnected` is `true` and `DisconnectedSince` is null

#### Scenario: Health check reads shared state
- **WHEN** a health check executes
- **THEN** it reads from `NjordHealthMonitor` without sending any actor message

#### Scenario: No MqttConnectedSince property exists
- **WHEN** the `NjordHealthMonitor` API is inspected
- **THEN** there SHALL be no connected-since property or field

### Requirement: MQTT connection health check
An `MqttConnectionHealthCheck` SHALL be registered (as `mqtt-connection`) only when `Njord:Mqtt:Enabled` is `true`. When registered, it SHALL report health based on `NjordHealthMonitor.Mqtt`:
- `Healthy` when connected.
- `Degraded` when not connected yet (no disconnect timestamp) or disconnected for less than 2 minutes.
- `Unhealthy` when disconnected for 2 minutes or more.

When MQTT is disabled, no MQTT health check SHALL be registered.

#### Scenario: Connected reports Healthy
- **WHEN** MQTT is enabled and `NjordHealthMonitor.Mqtt.IsConnected` is `true`
- **THEN** the health check returns `Healthy`

#### Scenario: Recently disconnected reports Degraded
- **WHEN** MQTT is enabled, `IsConnected` is `false` and the disconnect duration is 90 seconds
- **THEN** the health check returns `Degraded`

#### Scenario: Long disconnect reports Unhealthy
- **WHEN** MQTT is enabled, `IsConnected` is `false` and the disconnect duration is 3 minutes
- **THEN** the health check returns `Unhealthy`

#### Scenario: No MQTT health check when disabled
- **WHEN** `Njord:Mqtt:Enabled` is `false`
- **THEN** no `MqttConnectionHealthCheck` is registered and `/healthz` does not report MQTT status
