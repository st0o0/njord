## ADDED Requirements

### Requirement: HA Discovery is configurable via DiscoveryEnabled
`MqttOptions` SHALL expose a `DiscoveryEnabled` property (default `true`).
When `false`:

- No discovery config payloads SHALL be published.
- No subscription to the HA status topic (`<prefix>/status`) SHALL be made.
- No HA birth re-publish SHALL occur.
- Availability topics (online/offline) SHALL still be published.
- State topics (per-horizon telemetry) SHALL still be published.

#### Scenario: Discovery enabled by default
- **WHEN** no `DiscoveryEnabled` value is configured
- **THEN** discovery payloads are published at startup and on HA birth

#### Scenario: Discovery disabled suppresses config payloads
- **WHEN** `Njord__Mqtt__DiscoveryEnabled=false` is set
- **THEN** no retained discovery payloads are published to `<prefix>/device/*/config`

#### Scenario: Discovery disabled skips HA status subscription
- **WHEN** `DiscoveryEnabled` is `false`
- **THEN** the egress actor does not subscribe to `<prefix>/status`

#### Scenario: Availability still works when discovery is disabled
- **WHEN** `DiscoveryEnabled` is `false` and the egress actor connects
- **THEN** retained `online` is published to the availability topic

#### Scenario: State topics still publish when discovery is disabled
- **WHEN** `DiscoveryEnabled` is `false` and a fetch succeeds
- **THEN** retained state payloads are published to `njord/{location}/{model}/{horizon}`
