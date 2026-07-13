## ADDED Requirements

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
