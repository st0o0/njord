## MODIFIED Requirements

### Requirement: Device-based discovery for a static entity grid
For every configured (location, model) pair the system SHALL publish one retained
device-based discovery payload when `DiscoveryEnabled` is `true` (the default).
The payload contains the device block, origin block, shared state and availability
options, and one sensor component per **supported** (parameter, horizon) pair for
hourly parameters plus one per **supported** (parameter, day-offset) for daily parameters.
A parameter is supported when `EgressEvent.CapabilityLearned` reports it in `SupportedParameters`.
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

### Requirement: TopicScheme provides derived topic helpers
`TopicScheme` SHALL expose `EnrichmentDeviceId(string location, string typeName)` returning `njord_{slug(location)}_{typeName}` and `EnrichmentSubTopic(string baseTopic, string location, string typeName, string sub)` returning `{baseTopic}/{slug(location)}/{typeName}/{sub}`; the derived feature uses typeName `derived` with sub `h3` for a horizon topic and `meta` for the meta topic.

#### Scenario: Derived device id
- **WHEN** location is "lucerne"
- **THEN** `EnrichmentDeviceId(location, "derived")` returns "njord_lucerne_derived"

#### Scenario: Derived horizon topic
- **WHEN** baseTopic is "njord", location is "lucerne", horizon is "h3"
- **THEN** `EnrichmentSubTopic(baseTopic, location, "derived", horizon)` returns "njord/lucerne/derived/h3"

#### Scenario: Derived meta topic
- **WHEN** baseTopic is "njord", location is "lucerne"
- **THEN** `EnrichmentSubTopic(baseTopic, location, "derived", "meta")` returns "njord/lucerne/derived/meta"

### Requirement: TopicScheme provides trend topic helpers
`TopicScheme.EnrichmentDeviceId(location, "trends")` SHALL return `njord_{slug(location)}_trends` and `TopicScheme.EnrichmentTopic(baseTopic, location, "trends")` SHALL return `{baseTopic}/{slug(location)}/trends`.

#### Scenario: Trend device id
- **WHEN** location is "lucerne"
- **THEN** `EnrichmentDeviceId(location, "trends")` returns "njord_lucerne_trends"

#### Scenario: Trend topic
- **WHEN** baseTopic is "njord", location is "lucerne"
- **THEN** `EnrichmentTopic(baseTopic, location, "trends")` returns "njord/lucerne/trends"

### Requirement: TopicScheme provides index topic helpers
`TopicScheme.EnrichmentDeviceId(location, "indices")` SHALL return `njord_{slug(location)}_indices` and `TopicScheme.EnrichmentTopic(baseTopic, location, "indices")` SHALL return `{baseTopic}/{slug(location)}/indices`.

#### Scenario: Index device id
- **WHEN** location is "lucerne"
- **THEN** `EnrichmentDeviceId(location, "indices")` returns "njord_lucerne_indices"

#### Scenario: Index topic
- **WHEN** baseTopic is "njord", location is "lucerne"
- **THEN** `EnrichmentTopic(baseTopic, location, "indices")` returns "njord/lucerne/indices"

### Requirement: TopicScheme provides history topic helpers
`TopicScheme.EnrichmentDeviceId(location, "history")` SHALL return `njord_{slug(location)}_history` and `TopicScheme.EnrichmentTopic(baseTopic, location, "history")` SHALL return `{baseTopic}/{slug(location)}/history`.

#### Scenario: History device id
- **WHEN** location is "lucerne"
- **THEN** `EnrichmentDeviceId(location, "history")` returns "njord_lucerne_history"

#### Scenario: History topic
- **WHEN** baseTopic is "njord", location is "lucerne"
- **THEN** `EnrichmentTopic(baseTopic, location, "history")` returns "njord/lucerne/history"
