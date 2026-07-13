## REMOVED Requirements

### Requirement: Removed devices are tombstoned
**Reason**: Pre-release project has no deployed retained configs to clean up.
Tombstone logic is preventive migration code that adds complexity (extra queue,
wildcard subscription, topic matching) without current value.
**Migration**: If a future release changes the device set on a live deployment,
add targeted tombstone logic at that time. Manual cleanup is possible via
`mosquitto_pub -t <topic> -n -r`.

### Requirement: Old state topics are tombstoned on startup
**Reason**: The old `njord/{location}/{model}/state` monolithic topic scheme was
never deployed. There are no stale retained payloads to clean up.
**Migration**: None needed — the old scheme was never published to a live broker.

## MODIFIED Requirements

### Requirement: The connection actor owns the broker lifecycle
An actor SHALL own the MQTT connection: connect at startup, reconnect with
exponential backoff, register a Last Will that publishes `offline` (retained)
to the service availability topic. The actor SHALL materialize the egress stream
graph (MergeHub -> Publish Sink) using `Context.Materializer()` so the graph
lifecycle is bound to the actor. Egress failures MUST NOT crash the pipeline
actor.

#### Scenario: Last Will announces service death
- **WHEN** njord's connection dies without a clean disconnect
- **THEN** the broker publishes retained `offline` on the service availability topic

#### Scenario: Reconnect restores availability
- **WHEN** the broker becomes reachable again after an outage
- **THEN** the actor reconnects with backoff and offers retained `online` into the availability queue

#### Scenario: Actor-bound graph lifecycle
- **WHEN** the egress actor stops
- **THEN** the egress stream graph (MergeHub + Publish Sink) terminates

### Requirement: Device-based discovery for a static entity grid
For every configured (location, model) pair the system SHALL publish one retained
device-based discovery payload when `DiscoveryEnabled` is `true` (the default).
The payload contains the device block, origin block, shared state and availability
options, and one sensor component per configured (parameter, horizon) pair for
hourly parameters plus one per (parameter, day-offset) for daily parameters.
Discovery SHALL be published at startup and re-published when Home Assistant
announces `online` on `<prefix>/status`. When `DiscoveryEnabled` is `false`,
no discovery payloads SHALL be published and no HA status subscription SHALL be made.

#### Scenario: Grid size with Weather group and defaults
- **WHEN** 1 location, 8 models, ~30 hourly Weather parameters, 6 hourly horizons (3/6/12/24/48/72), ~15 daily Weather parameters, and 4 day offsets (d0-d3) are configured
- **THEN** exactly 8 retained discovery payloads are published, each carrying 180 hourly sensor components + 60 daily sensor components = 240 components

#### Scenario: HA birth triggers re-discovery
- **WHEN** `homeassistant/status` receives `online` and `DiscoveryEnabled` is `true`
- **THEN** all discovery payloads are published again with the current active parameter set

#### Scenario: Discovery component references horizon topic
- **WHEN** the discovery payload for device njord_lucerne_icon_d2 is built
- **THEN** the temperature +3h component carries `"state_topic": "njord/lucerne/icon_d2/h3"` and `"value_template": "{{ value_json.temperature }}"`

#### Scenario: Discovery component for daily parameter
- **WHEN** the discovery payload for device njord_lucerne_icon_d2 is built
- **THEN** the sunrise d0 component carries `"state_topic": "njord/lucerne/icon_d2/d0"` and `"value_template": "{{ value_json.sunrise }}"`
