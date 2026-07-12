# service-configuration Specification (Delta)

## ADDED Requirements

### Requirement: MQTT connection settings are configured and validated
The system SHALL accept an `Mqtt` options section with `Host` (required),
`Port` (default 1883), optional `Username`/`Password`, `DiscoveryPrefix`
(default `homeassistant`), and `BaseTopic` (default `njord`). Startup
validation SHALL fail when `Host` is missing. The password MUST NOT appear in
logs or validation messages.

#### Scenario: Missing host blocks startup
- **WHEN** the service starts without `Njord:Mqtt:Host`
- **THEN** startup validation fails naming the missing MQTT host

#### Scenario: Defaults apply
- **WHEN** only the host is configured
- **THEN** the effective port is 1883, the discovery prefix is
  `homeassistant`, and the base topic is `njord`

### Requirement: Forecast horizons are configuration
The system SHALL accept a list of forecast horizons in hours (default
`3, 6, 12, 24, 48, 72`) from which the entity grid is derived. Validation
SHALL reject an empty list, non-positive values, and horizons beyond the
fetched forecast window (96 h).

#### Scenario: Horizons default to the six-step ladder
- **WHEN** no horizons are configured
- **THEN** the effective horizons are 3, 6, 12, 24, 48, and 72 hours

#### Scenario: Out-of-window horizon is rejected
- **WHEN** a horizon of 120 hours is configured
- **THEN** startup validation fails naming the 96 h fetch window
