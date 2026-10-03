## MODIFIED Requirements

### Requirement: MQTT connection settings are configured and validated
The system SHALL accept an `Mqtt` options section with `Enabled` (default `true`),
`Host` (required when `Enabled` is `true`), `Port` (default 1883), optional
`Username`/`Password`, `DiscoveryPrefix` (default `homeassistant`), and `BaseTopic`
(default `njord`). Startup validation SHALL fail when `Enabled` is `true` and `Host`
is missing. Startup validation SHALL NOT fail on a missing `Host` when `Enabled` is
`false`. The password MUST NOT appear in logs or validation messages.

#### Scenario: Missing host blocks startup when MQTT enabled
- **WHEN** the service starts with `Njord:Mqtt:Enabled` as `true` (or default) and without `Njord:Mqtt:Host`
- **THEN** startup validation fails naming the missing MQTT host

#### Scenario: Missing host is accepted when MQTT disabled
- **WHEN** the service starts with `Njord:Mqtt:Enabled` as `false` and without `Njord:Mqtt:Host`
- **THEN** startup validation succeeds

#### Scenario: Defaults apply
- **WHEN** only the host is configured (and `Enabled` is default)
- **THEN** the effective port is 1883, the discovery prefix is
  `homeassistant`, the base topic is `njord`, and `Enabled` is `true`
