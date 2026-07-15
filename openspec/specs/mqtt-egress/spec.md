# mqtt-egress Specification

## Purpose

MQTT egress to Home Assistant: an actor-owned broker connection lifecycle with a Last Will availability topic, device-based MQTT Discovery (gated by `DiscoveryEnabled`) for the static config-derived entity grid, per-horizon retained telemetry state topics with flat JSON payloads, and declarative mapping of missing values to `unavailable` so entities never disappear or go stale.

## Requirements

### Requirement: Device-based discovery for a static entity grid
For every configured (location, model) pair the system SHALL publish one retained
device-based discovery payload when `DiscoveryEnabled` is `true` (the default).
The payload contains the device block, origin block, shared state and availability
options, and one sensor component per **supported** (parameter, horizon) pair for
hourly parameters plus one per **supported** (parameter, day-offset) for daily parameters.
A parameter is supported when `ModelCapabilityLearned` reports it in `SupportedParameters`.
A horizon is supported when it appears in `ApplicableHorizons` (hourly) or `ApplicableDayOffsets` (daily).
Discovery SHALL be published after capability learning completes (not at startup)
and re-published when Home Assistant announces `online` on `<prefix>/status`.
When `DiscoveryEnabled` is `false`, no discovery payloads SHALL be published and
no HA status subscription SHALL be made.

#### Scenario: Grid size reflects model capabilities
- **WHEN** 1 location, model `icon_d2` with MaxForecastHours=48, 25 of 30 parameters supported, applicable horizons [3, 6, 12, 24, 48], 10 of 15 daily parameters supported, applicable day-offsets [0, 1]
- **THEN** discovery payload carries 125 hourly components (25 x 5) + 20 daily components (10 x 2) = 145 components

#### Scenario: Long-range model gets full grid
- **WHEN** model `ecmwf_ifs025` with MaxForecastHours=240, all 30 parameters supported, all 6 horizons applicable, all 15 daily parameters supported, 4 day-offsets applicable
- **THEN** discovery payload carries 180 hourly components (30 x 6) + 60 daily components (15 x 4) = 240 components

#### Scenario: HA birth triggers re-discovery with learned capabilities
- **WHEN** `homeassistant/status` receives `online` and `DiscoveryEnabled` is `true`
- **THEN** all discovery payloads are published again using the current learned capability state

#### Scenario: Discovery component references horizon topic
- **WHEN** the discovery payload for device njord_lucerne_icon_d2 is built and horizon h3 is applicable
- **THEN** the temperature +3h component carries `"state_topic": "njord/lucerne/icon_d2/h3"` and `"value_template": "{{ value_json.temperature }}"`

#### Scenario: Discovery component for daily parameter
- **WHEN** the discovery payload for device njord_lucerne_icon_d2 is built and day-offset d0 is applicable and sunrise is a supported parameter
- **THEN** the sunrise d0 component carries `"state_topic": "njord/lucerne/icon_d2/d0"` and `"value_template": "{{ value_json.sunrise }}"`

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
topic reads `offline`, or (b) its value is absent in the state JSON, or
(c) no state update arrived within twice the poll interval (`expire_after`).
Components for unsupported parameters or out-of-range horizons SHALL NOT
be registered at all — they SHALL NOT appear as unavailable entities in HA.

#### Scenario: Unsupported parameter has no sensor
- **WHEN** `icon_d2` does not support `precipitation_probability`
- **THEN** no sensor component for `precipitation_probability` exists in the discovery payload for icon_d2

#### Scenario: Out-of-range horizon has no sensor
- **WHEN** `icon_d2` has MaxForecastHours=48
- **THEN** no sensor components for horizon h72 exist in the discovery payload for icon_d2

#### Scenario: Silent model expires
- **WHEN** a device receives no state update for two poll intervals
- **THEN** its components become unavailable without any explicit publish

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
