# mqtt-egress Specification

## Purpose

MQTT egress to Home Assistant: an actor-owned broker connection lifecycle with a Last Will availability topic, device-based MQTT Discovery (gated by `DiscoveryEnabled`) for the static config-derived entity grid, per-horizon retained telemetry state topics with flat JSON payloads, and declarative mapping of missing values to `unavailable` so entities never disappear or go stale.

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

### Requirement: The egress actor vends a SinkRef for external message producers
The `MqttEgressActor` SHALL respond to a `RequestMqttSink` message with a `MqttSinkResponse` containing a `SinkRef<MqttMessage>` connected to the existing MergeHub. Multiple SinkRefs MAY be vended — each connects as an independent producer to the MergeHub. The SinkRef SHALL be vended only after the egress graph is materialized.

#### Scenario: EnrichmentActor requests and receives a SinkRef
- **WHEN** the EnrichmentActor sends `RequestMqttSink` to the MqttEgressActor after the egress graph is materialized
- **THEN** the MqttEgressActor responds with a `MqttSinkResponse` containing a `SinkRef<MqttMessage>`

#### Scenario: SinkRef messages flow through the same publish path
- **WHEN** the EnrichmentActor sends an `MqttMessage` via the SinkRef
- **THEN** the message flows through the MergeHub and is published by the same Publish Sink as state and discovery messages

#### Scenario: Request before materialization is stashed
- **WHEN** a `RequestMqttSink` arrives before the egress graph is materialized
- **THEN** the message is stashed and processed after materialization

### Requirement: The egress actor exposes a SinkRef for internal use only
The egress actor SHALL materialize a `SinkRef<MqttMessage>` connected to the MergeHub for internal use by its consumer graph. The egress actor SHALL also vend SinkRefs to external actors on request via the `RequestMqttSink` / `MqttSinkResponse` protocol. External SinkRefs connect as additional producers to the same MergeHub.

#### Scenario: Internal consumer graph feeds into MergeHub
- **WHEN** the egress consumer graph processes a `FetchOutcome.Success`
- **THEN** the resulting `MqttMessage`(s) flow into the MergeHub through the internal consumer connection

#### Scenario: External SinkRef feeds into same MergeHub
- **WHEN** the EnrichmentActor sends messages via a vended SinkRef
- **THEN** the messages merge into the same MergeHub alongside internal messages

### Requirement: Discovery covers the consensus pseudo-model device
When `DiscoveryEnabled` is `true` and the consensus enrichment is enabled, the egress actor SHALL publish a retained device-based discovery payload for each configured location's consensus device (`njord_{location}_consensus`). The payload SHALL carry the same horizons and parameters as model devices, plus diagnostic attributes. Discovery SHALL be published alongside model device discovery on startup and HA birth.

#### Scenario: Consensus device discovery at startup
- **WHEN** the broker connects and consensus enrichment is enabled
- **THEN** one discovery payload per location is published for the consensus device alongside model device payloads

#### Scenario: Consensus discovery on HA birth
- **WHEN** `homeassistant/status` receives `online` and consensus is enabled
- **THEN** the consensus device discovery payloads are re-published

#### Scenario: Consensus disabled skips discovery
- **WHEN** `EnrichmentOptions.Consensus.Enabled` is `false`
- **THEN** no consensus device discovery payloads are published

### Requirement: TopicScheme provides derived topic helpers
`TopicScheme` SHALL expose `DerivedDeviceId(string location)` returning `njord_{slug(location)}_derived`, `DerivedHorizonTopic(string baseTopic, string location, string horizon)` returning `{baseTopic}/{slug(location)}/derived/{horizon}`, and `DerivedMetaTopic(string baseTopic, string location)` returning `{baseTopic}/{slug(location)}/derived/meta`.

#### Scenario: Derived device id
- **WHEN** location is "lucerne"
- **THEN** `DerivedDeviceId` returns "njord_lucerne_derived"

#### Scenario: Derived horizon topic
- **WHEN** baseTopic is "njord", location is "lucerne", horizon is "h3"
- **THEN** `DerivedHorizonTopic` returns "njord/lucerne/derived/h3"

#### Scenario: Derived meta topic
- **WHEN** baseTopic is "njord", location is "lucerne"
- **THEN** `DerivedMetaTopic` returns "njord/lucerne/derived/meta"

### Requirement: DiscoveryPayloadBuilder builds a derived device
`DiscoveryPayloadBuilder.BuildDerived` SHALL produce a device-based discovery payload for location with device id `njord_{location}_derived`, model `derived`, and sensor components for: each horizon-based derived value (beaufort, wind_chill, dewpoint_comfort, wmo_description) at each configured horizon, plus scalar sensors (diurnal_amplitude, sunshine_pct, inversion). Numeric sensors SHALL have `unit_of_measurement` and `device_class` where applicable. String sensors (dewpoint_comfort, wmo_description) SHALL use platform `sensor` with no unit. The boolean sensor (inversion) SHALL use platform `binary_sensor`.

#### Scenario: Derived device payload structure
- **WHEN** `BuildDerived` is called for location "lucerne" with horizons [3, 6, 12, 24, 48, 72]
- **THEN** the payload contains device id "njord_lucerne_derived", model "derived", and sensor components

#### Scenario: Beaufort sensor component per horizon
- **WHEN** the derived device is built with horizon 3
- **THEN** a sensor component exists with unique_id "njord_lucerne_derived_beaufort_h3", value_template extracting `beaufort` from the horizon topic JSON, and no unit_of_measurement (Beaufort is dimensionless)

#### Scenario: Wind chill sensor component per horizon
- **WHEN** the derived device is built with horizon 3
- **THEN** a sensor component exists with unique_id "njord_lucerne_derived_wind_chill_h3", unit "°C", and device_class "temperature"

#### Scenario: Dew-point comfort sensor component per horizon
- **WHEN** the derived device is built with horizon 3
- **THEN** a sensor component exists with unique_id "njord_lucerne_derived_dewpoint_comfort_h3", no unit, platform "sensor"

#### Scenario: WMO description sensor component per horizon
- **WHEN** the derived device is built with horizon 3
- **THEN** a sensor component exists with unique_id "njord_lucerne_derived_wmo_description_h3", no unit, platform "sensor"

#### Scenario: Scalar sensors on meta topic
- **WHEN** the derived device is built
- **THEN** sensor components exist for diurnal_amplitude (unit "°C", device_class "temperature"), sunshine_pct (unit "%"), and inversion (platform "binary_sensor")

### Requirement: TopicScheme provides trend topic helpers
`TopicScheme` SHALL expose `TrendDeviceId(string location)` returning `njord_{slug(location)}_trends` and `TrendTopic(string baseTopic, string location)` returning `{baseTopic}/{slug(location)}/trends`.

#### Scenario: Trend device id
- **WHEN** location is "lucerne"
- **THEN** `TrendDeviceId` returns "njord_lucerne_trends"

#### Scenario: Trend topic
- **WHEN** baseTopic is "njord", location is "lucerne"
- **THEN** `TrendTopic` returns "njord/lucerne/trends"

### Requirement: DiscoveryPayloadBuilder builds a trend device
`DiscoveryPayloadBuilder.BuildTrends` SHALL produce a device-based discovery payload for location with device id `njord_{location}_trends`, model `trends`, and sensor components for: trend direction sensors per primary parameter (temperature, wind_speed, precipitation, cloud_cover) as text sensors, weather_change as a text sensor, precip_starts and precip_ends as numeric sensors (unit "h"), temp_max_in and temp_min_in as numeric sensors (unit "h"), stability as a text sensor, decay_rate as a numeric sensor (unit "°C/h"), and reliable_hours as a numeric sensor (unit "h").

#### Scenario: Trend device payload structure
- **WHEN** `BuildTrends` is called for location "lucerne"
- **THEN** the payload contains device id "njord_lucerne_trends", model "trends"

#### Scenario: Trend direction sensors
- **WHEN** the trend device is built
- **THEN** sensor components exist for trend_temperature, trend_wind_speed, trend_precipitation, trend_cloud_cover -- each a text sensor with no unit

#### Scenario: Timing sensors are numeric
- **WHEN** the trend device is built
- **THEN** precip_starts, precip_ends, temp_max_in, temp_min_in have unit "h"

#### Scenario: Decay rate sensor
- **WHEN** the trend device is built
- **THEN** decay_rate has unit "°C/h" and reliable_hours has unit "h"

### Requirement: TopicScheme provides index topic helpers
`TopicScheme` SHALL expose `IndexDeviceId(string location)` returning `njord_{slug(location)}_indices` and `IndexTopic(string baseTopic, string location)` returning `{baseTopic}/{slug(location)}/indices`.

#### Scenario: Index device id
- **WHEN** location is "lucerne"
- **THEN** `IndexDeviceId` returns "njord_lucerne_indices"

#### Scenario: Index topic
- **WHEN** baseTopic is "njord", location is "lucerne"
- **THEN** `IndexTopic` returns "njord/lucerne/indices"

### Requirement: DiscoveryPayloadBuilder builds an index device
`DiscoveryPayloadBuilder.BuildIndices` SHALL produce a device-based discovery payload for location with device id `njord_{location}_indices`, model `indices`. Score sensors (laundry, outdoor, running, cycling, bbq, irrigation, solar, ventilation) SHALL be numeric sensors with no unit. Degree day sensors (hdd, cdd) SHALL have unit "°Cd". Frost protection sensors (frost_hours, frost_confidence) SHALL be numeric. VPD sensor SHALL be a text sensor. Weather change, stability — text sensors.

#### Scenario: Index device payload structure
- **WHEN** `BuildIndices` is called for location "lucerne"
- **THEN** the payload contains device id "njord_lucerne_indices", model "indices"

#### Scenario: Score sensors are numeric without unit
- **WHEN** the index device is built
- **THEN** laundry, outdoor, running, cycling, bbq, irrigation, solar, ventilation sensors have no unit_of_measurement

#### Scenario: Degree day sensors
- **WHEN** the index device is built
- **THEN** hdd and cdd sensors have unit "°Cd"

#### Scenario: VPD sensor is text
- **WHEN** the index device is built
- **THEN** vpd sensor has no unit_of_measurement (text category)

### Requirement: TopicScheme provides energy topic helpers
`TopicScheme` SHALL expose `EnergyDeviceId(string location)` returning `njord_{slug(location)}_energy` and `EnergyTopic(string baseTopic, string location)` returning `{baseTopic}/{slug(location)}/energy`.

#### Scenario: Energy device id
- **WHEN** location is "lucerne"
- **THEN** `EnergyDeviceId` returns "njord_lucerne_energy"

#### Scenario: Energy topic
- **WHEN** baseTopic is "njord", location is "lucerne"
- **THEN** `EnergyTopic` returns "njord/lucerne/energy"

### Requirement: DiscoveryPayloadBuilder builds an energy device
`DiscoveryPayloadBuilder.BuildEnergy` SHALL produce a device-based discovery payload for location with device id `njord_{location}_energy`, model `energy`. Sensors: heating_demand (numeric, no unit), cop_estimate (numeric, no unit), shading (numeric, no unit), night_cooling (numeric, no unit), battery_strategy (text sensor), cop_optimal (sensor with JSON attributes).

#### Scenario: Energy device payload structure
- **WHEN** `BuildEnergy` is called for location "lucerne"
- **THEN** the payload contains device id "njord_lucerne_energy", model "energy"

#### Scenario: Score sensors are numeric
- **WHEN** the energy device is built
- **THEN** heating_demand, cop_estimate, shading, night_cooling sensors exist

#### Scenario: Battery strategy is text
- **WHEN** the energy device is built
- **THEN** battery_strategy sensor has no unit_of_measurement

### Requirement: TopicScheme provides history topic helpers
`TopicScheme` SHALL expose `HistoryDeviceId(string location)` returning `njord_{slug(location)}_history` and `HistoryTopic(string baseTopic, string location)` returning `{baseTopic}/{slug(location)}/history`.

#### Scenario: History device id
- **WHEN** location is "lucerne"
- **THEN** `HistoryDeviceId` returns "njord_lucerne_history"

#### Scenario: History topic
- **WHEN** baseTopic is "njord", location is "lucerne"
- **THEN** `HistoryTopic` returns "njord/lucerne/history"

### Requirement: DiscoveryPayloadBuilder builds a history device
`DiscoveryPayloadBuilder.BuildHistory` SHALL produce a device-based discovery payload for location with device id `njord_{location}_history`, model `history`. Sensors SHALL include: per-model MAE sensors (numeric), per-model weight sensors (numeric), per-model drift sensors (numeric), seasonal best-model sensor (text), anomaly sensor (binary_sensor), anomaly deviation sensor (numeric), and weighted consensus sensors (numeric per parameter).

#### Scenario: History device payload structure
- **WHEN** `BuildHistory` is called for location "lucerne" with models ["icon_d2", "ecmwf_ifs025"]
- **THEN** the payload contains device id "njord_lucerne_history", model "history"

#### Scenario: Per-model sensors
- **WHEN** models are ["icon_d2", "ecmwf_ifs025"]
- **THEN** sensors exist for mae_7d_icon_d2, mae_30d_icon_d2, weight_icon_d2, drift_icon_d2, and similarly for ecmwf_ifs025

#### Scenario: Anomaly is binary sensor
- **WHEN** the history device is built
- **THEN** an anomaly sensor exists with platform "binary_sensor"
