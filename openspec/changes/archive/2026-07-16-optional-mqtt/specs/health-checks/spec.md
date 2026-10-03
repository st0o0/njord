## MODIFIED Requirements

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
- **WHEN** MQTT is enabled and `NjordHealthState.IsMqttConnected` is `false` and the disconnect duration is 90 seconds
- **THEN** the health check returns `Degraded`

#### Scenario: Long disconnect reports Unhealthy
- **WHEN** MQTT is enabled and `NjordHealthState.IsMqttConnected` is `false` and the disconnect duration is 3 minutes
- **THEN** the health check returns `Unhealthy`

#### Scenario: No MQTT health check when disabled
- **WHEN** `Mqtt.Enabled` is `false`
- **THEN** no `MqttConnectionHealthCheck` is registered and `/healthz` does not report MQTT status
