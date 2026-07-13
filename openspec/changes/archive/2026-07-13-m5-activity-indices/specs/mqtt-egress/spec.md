## ADDED Requirements

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
