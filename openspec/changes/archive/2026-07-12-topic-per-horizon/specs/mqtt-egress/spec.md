## MODIFIED Requirements

### Requirement: Telemetry publishes one retained state per device per cycle
For every successful fetch the pipeline SHALL produce one `MqttMessage` per changed horizon for that device and deliver them to the egress via StreamRef. Each message SHALL be published retained on topic `njord/{location}/{model}/{horizon}` (e.g. `njord/lucerne/icon_d2/h3`). The payload SHALL be a flat JSON object with parameter keys and their values for that time-slice. The `/state` suffix is NOT used — the horizon segment identifies the state.

#### Scenario: State topic per horizon
- **WHEN** a fetch for (lucerne, icon_d2) succeeds with changed h3 and h6 values
- **THEN** retained publishes occur on `njord/lucerne/icon_d2/h3` and `njord/lucerne/icon_d2/h6`

#### Scenario: Hourly horizon payload is flat
- **WHEN** `njord/lucerne/icon_d2/h3` is published
- **THEN** the payload is `{"temperature": 22.5, "humidity": 68, ...}` (flat, no nesting)

#### Scenario: Daily horizon payload is flat
- **WHEN** `njord/lucerne/icon_d2/d0` is published
- **THEN** the payload is `{"temperature_max": 25.1, "sunrise": "05:42", ...}` (flat, no nesting)

### Requirement: Device-based discovery for a static entity grid
For every configured (location, model) pair the system SHALL publish one retained device-based discovery payload. Each sensor component SHALL reference the horizon-specific state topic and a simplified value template. Hourly components SHALL use `state_topic: "njord/{location}/{model}/h{horizon}"` with `value_template: "{{ value_json.{json_key} }}"`. Daily components SHALL use `state_topic: "njord/{location}/{model}/d{offset}"` with `value_template: "{{ value_json.{json_key} }}"`.

#### Scenario: Discovery component references horizon topic
- **WHEN** the discovery payload for device njord_lucerne_icon_d2 is built
- **THEN** the temperature +3h component carries `"state_topic": "njord/lucerne/icon_d2/h3"` and `"value_template": "{{ value_json.temperature }}"`

#### Scenario: Discovery component for daily parameter
- **WHEN** the discovery payload for device njord_lucerne_icon_d2 is built
- **THEN** the sunrise d0 component carries `"state_topic": "njord/lucerne/icon_d2/d0"` and `"value_template": "{{ value_json.sunrise }}"`

### Requirement: Old state topics are tombstoned on startup
On first broker connection, the system SHALL publish an empty retained message to `njord/{location}/{model}/state` for every configured device. This removes stale monolithic state payloads from users upgrading from the old topic scheme.

#### Scenario: Legacy state topic is cleared
- **WHEN** the egress actor connects to a broker that has retained `njord/lucerne/icon_d2/state`
- **THEN** an empty retained publish to `njord/lucerne/icon_d2/state` clears it

## REMOVED Requirements

### Requirement: Telemetry publishes one retained state per device per cycle
**Reason:** Replaced by per-horizon publishing. The old requirement specified one JSON per device containing all horizons nested under h3/h6/.../d0/d1/... keys. The new scheme publishes one flat JSON per horizon on a separate topic, with delta-publishing to skip unchanged horizons.
**Migration:** Remove `StatePayloadBuilder.Build` (single-JSON variant). Replace with `BuildPerHorizon` returning a dictionary. Update `TopicScheme.StateTopic` to include the horizon segment and drop `/state`.
