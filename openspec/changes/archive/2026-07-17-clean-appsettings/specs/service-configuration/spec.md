## MODIFIED Requirements

### Requirement: MQTT connection settings are configured and validated
The system SHALL accept an `Mqtt` options section with `Enabled` (default `false`),
`Host` (required when `Enabled` is `true`), `Port` (default 1883), optional
`Username`/`Password`, `DiscoveryPrefix` (default `homeassistant`), and `BaseTopic`
(default `njord`). Startup validation SHALL fail when `Enabled` is `true` and `Host`
is missing. Startup validation SHALL NOT fail on a missing `Host` when `Enabled` is
`false`. The password MUST NOT appear in logs or validation messages.

#### Scenario: MQTT is disabled by default
- **WHEN** no `Njord:Mqtt:Enabled` value is configured
- **THEN** the effective value is `false` and no MQTT services or actors are registered

#### Scenario: Missing host blocks startup when MQTT enabled
- **WHEN** the service starts with `Njord:Mqtt:Enabled` as `true` and without `Njord:Mqtt:Host`
- **THEN** startup validation fails naming the missing MQTT host

#### Scenario: Missing host is accepted when MQTT disabled
- **WHEN** the service starts with `Njord:Mqtt:Enabled` as `false` (or default) and without `Njord:Mqtt:Host`
- **THEN** startup validation succeeds

#### Scenario: Defaults apply when MQTT enabled
- **WHEN** `Enabled` is `true` and only the host is configured
- **THEN** the effective port is 1883, the discovery prefix is `homeassistant`, and the base topic is `njord`

### Requirement: Configuration layering follows ASP.NET conventions
The `appsettings.json` file SHALL contain only production-appropriate settings (logging levels). It SHALL NOT contain a `Njord:` section — all option defaults SHALL be defined in the Options classes. The `appsettings.Development.json` file SHALL contain dev-specific overrides (debug logging, test locations, MQTT disabled). It SHALL be loaded automatically when `ASPNETCORE_ENVIRONMENT=Development`.

#### Scenario: Docker image starts with no Njord config
- **WHEN** the service starts in Production environment with no `Njord:` configuration
- **THEN** all Options properties use their code defaults and startup validation rejects the empty locations list with a clear message

#### Scenario: Dev environment loads development overrides
- **WHEN** the service starts with `ASPNETCORE_ENVIRONMENT=Development`
- **THEN** `appsettings.Development.json` is loaded and overrides the production defaults with dev-specific values
