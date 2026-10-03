## MODIFIED Requirements

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
- **THEN** discovery payload carries 125 hourly components (25 × 5) + 20 daily components (10 × 2) = 145 components

#### Scenario: Long-range model gets full grid
- **WHEN** model `ecmwf_ifs025` with MaxForecastHours=240, all 30 parameters supported, all 6 horizons applicable, all 15 daily parameters supported, 4 day-offsets applicable
- **THEN** discovery payload carries 180 hourly components (30 × 6) + 60 daily components (15 × 4) = 240 components

#### Scenario: HA birth triggers re-discovery with learned capabilities
- **WHEN** `homeassistant/status` receives `online` and `DiscoveryEnabled` is `true`
- **THEN** all discovery payloads are published again using the current learned capability state

#### Scenario: Discovery component references horizon topic
- **WHEN** the discovery payload for device njord_lucerne_icon_d2 is built and horizon h3 is applicable
- **THEN** the temperature +3h component carries `"state_topic": "njord/lucerne/icon_d2/h3"` and `"value_template": "{{ value_json.temperature }}"`

#### Scenario: Discovery component for daily parameter
- **WHEN** the discovery payload for device njord_lucerne_icon_d2 is built and day-offset d0 is applicable and sunrise is a supported parameter
- **THEN** the sunrise d0 component carries `"state_topic": "njord/lucerne/icon_d2/d0"` and `"value_template": "{{ value_json.sunrise }}"`

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
