# mqtt-egress Specification

## Purpose

MQTT egress to Home Assistant: an actor-owned broker connection lifecycle with a Last Will availability topic, device-based MQTT Discovery for the static config-derived entity grid, per-horizon retained telemetry state topics with flat JSON payloads, legacy topic tombstoning on startup, and declarative mapping of missing values to `unavailable` so entities never disappear or go stale.

## Requirements

### Requirement: The connection actor owns the broker lifecycle
An actor SHALL own the MQTT connection: connect at startup, reconnect with exponential backoff, register a Last Will that publishes `offline` (retained) to the service availability topic. The actor SHALL materialize the egress stream graph (MergeHub -> Publish Sink) using `Context.Materializer()` so the graph lifecycle is bound to the actor. Egress failures MUST NOT crash the pipeline actor.

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
For every configured (location, model) pair the system SHALL publish one retained device-based discovery payload (`<prefix>/device/njord_<location>_<model>/config`) containing the device block, an origin block, shared state and availability options, and one sensor component per configured (parameter, horizon) pair for hourly parameters plus one sensor component per configured (parameter, day-offset) pair for daily parameters, each with a stable `unique_id`. Hourly unique_ids follow `njord_{location}_{model}_{param}_h{horizon}`. Daily unique_ids follow `njord_{location}_{model}_{param}_d{offset}`. Each sensor component SHALL reference the horizon-specific state topic and a simplified value template. Hourly components SHALL use `state_topic: "njord/{location}/{model}/h{horizon}"` with `value_template: "{{ value_json.{json_key} }}"`. Daily components SHALL use `state_topic: "njord/{location}/{model}/d{offset}"` with `value_template: "{{ value_json.{json_key} }}"`. The grid is derived from configuration only — never from fetched data. Discovery SHALL be published at startup and re-published when Home Assistant announces `online` on `homeassistant/status`.

#### Scenario: Grid size with Weather group and defaults
- **WHEN** 1 location, 8 models, ~30 hourly Weather parameters, 6 hourly horizons (3/6/12/24/48/72), ~15 daily Weather parameters, and 4 day offsets (d0-d3) are configured
- **THEN** exactly 8 retained discovery payloads are published, each carrying 180 hourly sensor components + 60 daily sensor components = 240 components

#### Scenario: HA birth triggers re-discovery
- **WHEN** `homeassistant/status` receives `online`
- **THEN** all discovery payloads are published again with the current active parameter set

#### Scenario: Discovery component references horizon topic
- **WHEN** the discovery payload for device njord_lucerne_icon_d2 is built
- **THEN** the temperature +3h component carries `"state_topic": "njord/lucerne/icon_d2/h3"` and `"value_template": "{{ value_json.temperature }}"`

#### Scenario: Discovery component for daily parameter
- **WHEN** the discovery payload for device njord_lucerne_icon_d2 is built
- **THEN** the sunrise d0 component carries `"state_topic": "njord/lucerne/icon_d2/d0"` and `"value_template": "{{ value_json.sunrise }}"`

#### Scenario: Removed devices are tombstoned
- **WHEN** a model is removed from the configuration and njord restarts
- **THEN** an empty retained payload is published to that device's config topic

### Requirement: Telemetry publishes one retained state per horizon per cycle
For every successful fetch the pipeline SHALL produce one `MqttMessage` per changed horizon for that device and deliver them to the egress via StreamRef. Each message SHALL be published retained on topic `njord/{location}/{model}/{horizon}` (e.g. `njord/lucerne/icon_d2/h3`). The payload SHALL be a flat JSON object with parameter keys and their values for that time-slice. The `/state` suffix is NOT used — the horizon segment identifies the state. Devices whose fetch failed SHALL NOT be published that cycle.

#### Scenario: State topic per horizon
- **WHEN** a fetch for (lucerne, icon_d2) succeeds with changed h3 and h6 values
- **THEN** retained publishes occur on `njord/lucerne/icon_d2/h3` and `njord/lucerne/icon_d2/h6`

#### Scenario: Hourly horizon payload is flat
- **WHEN** `njord/lucerne/icon_d2/h3` is published
- **THEN** the payload is `{"temperature": 22.5, "humidity": 68, ...}` (flat, no nesting)

#### Scenario: Daily horizon payload is flat
- **WHEN** `njord/lucerne/icon_d2/d0` is published
- **THEN** the payload is `{"temperature_max": 25.1, "sunrise": "05:42", ...}` (flat, no nesting)

#### Scenario: Failed models produce no state message
- **WHEN** a model's fetch fails
- **THEN** no MqttMessage for that device enters the StreamRef

### Requirement: Old state topics are tombstoned on startup
On first broker connection, the system SHALL publish an empty retained message to `njord/{location}/{model}/state` for every configured device. This removes stale monolithic state payloads from users upgrading from the old topic scheme.

#### Scenario: Legacy state topic is cleared
- **WHEN** the egress actor connects to a broker that has retained `njord/lucerne/icon_d2/state`
- **THEN** an empty retained publish to `njord/lucerne/icon_d2/state` clears it

### Requirement: Discovery component metadata adapts to registry
Each sensor component in the discovery payload SHALL derive its `unit_of_measurement`, `device_class`, and `name` from the parameter registry entry. Parameters with `device_class: null` SHALL omit the `device_class` field. Parameters with value type `TimeString` SHALL use `device_class: "timestamp"` when appropriate or omit it.

#### Scenario: Registry-driven unit in discovery
- **WHEN** `shortwave_radiation` (unit `W/m²`, device_class `irradiance`) is in the active set
- **THEN** its discovery component carries `"unit_of_measurement": "W/m²"` and `"device_class": "irradiance"`

#### Scenario: Parameter without device class
- **WHEN** `weather_code` (no device_class) is in the active set
- **THEN** its discovery component carries `"unit_of_measurement": "wmo code"` and no `device_class` field

### Requirement: Missing values surface as unavailable, never as stale data
Every component SHALL become unavailable when (a) the service availability
topic reads `offline`, or (b) its value is absent in the state JSON (e.g.
beyond the model's horizon), or (c) no state update arrived within twice the
poll interval (`expire_after`). Entities MUST NOT disappear from HA because a
value is missing.

#### Scenario: Beyond-horizon component is unavailable
- **WHEN** `meteoswiss_icon_ch1` provides no +72 h values
- **THEN** its `+72 h` components are unavailable while its near-horizon
  components stay available

#### Scenario: Silent model expires
- **WHEN** a device receives no state update for two poll intervals
- **THEN** its components become unavailable without any explicit publish
