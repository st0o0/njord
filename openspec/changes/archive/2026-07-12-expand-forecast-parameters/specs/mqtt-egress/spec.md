## MODIFIED Requirements

### Requirement: Device-based discovery for a static entity grid
For every configured (location, model) pair the system SHALL publish one retained device-based discovery payload (`<prefix>/device/njord_<location>_<model>/config`) containing the device block, an origin block, shared state and availability options, and one sensor component per configured (parameter, horizon) pair for hourly parameters plus one sensor component per configured (parameter, day-offset) pair for daily parameters, each with a stable `unique_id`. Hourly unique_ids follow `njord_{location}_{model}_{param}_h{horizon}`. Daily unique_ids follow `njord_{location}_{model}_{param}_d{offset}`. The grid is derived from configuration only — never from fetched data. Discovery SHALL be published at startup and re-published when Home Assistant announces `online` on `homeassistant/status`.

#### Scenario: Grid size with Weather group and defaults
- **WHEN** 1 location, 8 models, ~30 hourly Weather parameters, 6 hourly horizons (3/6/12/24/48/72), ~15 daily Weather parameters, and 4 day offsets (d0-d3) are configured
- **THEN** exactly 8 retained discovery payloads are published, each carrying 180 hourly sensor components + 60 daily sensor components = 240 components

#### Scenario: HA birth triggers re-discovery
- **WHEN** `homeassistant/status` receives `online`
- **THEN** all discovery payloads are published again with the current active parameter set

#### Scenario: Removed devices are tombstoned
- **WHEN** a model is removed from the configuration and njord restarts
- **THEN** an empty retained payload is published to that device's config topic

### Requirement: Telemetry publishes one retained state per device per cycle
For every cycle the system SHALL publish one retained state JSON per (location, model) device that delivered a forecast. The JSON SHALL contain one key per hourly horizon (`h3`, `h6`, ...) with an object of parameter values, plus one key per day offset (`d0`, `d1`, ...) with an object of daily parameter values. Devices whose fetch failed SHALL NOT be re-published that cycle.

#### Scenario: State JSON includes both hourly and daily
- **WHEN** a cycle completes with both hourly and daily data for a model
- **THEN** the state JSON contains `h3`, `h6`, `h12`, `h24`, `h48`, `h72` keys with hourly values and `d0`, `d1`, `d2`, `d3` keys with daily values

#### Scenario: Time-string daily values are published as strings
- **WHEN** a daily parameter `sunrise` has value `"05:31"` for d0
- **THEN** the state JSON `d0` object contains `"sunrise": "05:31"` as a string value

## ADDED Requirements

### Requirement: Discovery component metadata adapts to registry
Each sensor component in the discovery payload SHALL derive its `unit_of_measurement`, `device_class`, and `name` from the parameter registry entry. Parameters with `device_class: null` SHALL omit the `device_class` field. Parameters with value type `TimeString` SHALL use `device_class: "timestamp"` when appropriate or omit it.

#### Scenario: Registry-driven unit in discovery
- **WHEN** `shortwave_radiation` (unit `W/m²`, device_class `irradiance`) is in the active set
- **THEN** its discovery component carries `"unit_of_measurement": "W/m²"` and `"device_class": "irradiance"`

#### Scenario: Parameter without device class
- **WHEN** `weather_code` (no device_class) is in the active set
- **THEN** its discovery component carries `"unit_of_measurement": "wmo code"` and no `device_class` field
