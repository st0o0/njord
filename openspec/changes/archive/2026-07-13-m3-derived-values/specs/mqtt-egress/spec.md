## ADDED Requirements

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
