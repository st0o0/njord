## ADDED Requirements

### Requirement: Catalog includes resolved domain schemas per location
`GetCatalogResponse` SHALL include per-location domain schemas that list each active domain and the proto field names of the variables njord actually fetches for that domain. The schema SHALL be derived from the resolved `DomainRequirements` (the OR of all enabled features' requirements after `WithBaseParams`).

#### Scenario: Location with AQ domain requesting only pm25 and ozone
- **WHEN** the enabled features for a location require only `AirQualityParams.Pm25` and `AirQualityParams.Ozone`
- **THEN** the catalog SHALL include a domain schema entry for `DOMAIN_AIR_QUALITY` with fields `["pm25", "ozone"]`
- **AND** the field `"pm10"` SHALL NOT appear in the schema

#### Scenario: Location with Marine domain requesting wave_height but not wave_period
- **WHEN** the enabled features require `MarineParams.WaveHeight` but no feature requires `MarineParams.WavePeriod`
- **THEN** the catalog SHALL include a domain schema for `DOMAIN_MARINE` with `"wave_height"` in the fields
- **AND** `"wave_period"` SHALL NOT appear in the fields

#### Scenario: Domain not required by any feature
- **WHEN** no enabled feature requires Flood parameters for a location
- **THEN** the catalog SHALL NOT include a domain schema entry for `DOMAIN_FLOOD` for that location

#### Scenario: Optional parameters are included in the schema
- **WHEN** a feature declares `OptionalMarine: MarineParams.WaterTemperature`
- **THEN** the catalog domain schema for `DOMAIN_MARINE` SHALL include `"water_temperature"` in its fields (optional variables are fetched and produce data)

### Requirement: Field names in the schema match proto field names
The field names in the domain schema SHALL be the proto field names from `weather.proto` sample messages (e.g. `"pm25"`, `"wave_height"`, `"water_temperature"`), not the API variable names or C# property names. This allows ha-njord to match them directly against `DomainSensorDescription.field`.

#### Scenario: Mapping from params to proto field names
- **WHEN** `AirQualityParams.BirchPollen` is in the resolved requirements
- **THEN** the schema field name SHALL be `"birch_pollen"` (the proto field name), not `"birch_pollen_concentration"` (the API name) or `"BirchPollen"` (the C# name)

### Requirement: ha-njord registers domain sensors only for schema fields
ha-njord SHALL read the domain schemas from the catalog response and SHALL create domain sensors only for fields present in the schema. A sensor for a field not in the schema SHALL NOT be registered.

#### Scenario: PM10 not in schema
- **WHEN** the catalog for a location has `DOMAIN_AIR_QUALITY` with fields `["pm25", "ozone", "birch_pollen"]`
- **THEN** ha-njord SHALL NOT create a `sensor.<location>_pm10` entity

#### Scenario: wave_period not in schema
- **WHEN** the catalog for a location has `DOMAIN_MARINE` with fields `["wave_height", "water_temperature"]`
- **THEN** ha-njord SHALL NOT create a `sensor.<location>_wave_period` entity

#### Scenario: New field appears after feature enable
- **WHEN** a feature requiring wave_period is enabled, njord restarts, and ha-njord reconnects
- **THEN** the updated catalog SHALL include `"wave_period"` and ha-njord SHALL create the sensor dynamically

### Requirement: Domain sample proto fields are optional
All numeric fields in domain sample messages (`ForecastSample`, `AirQualitySample`, `MarineSample`, `FloodSample`) that may be absent because no feature requests them SHALL be declared `optional` in `weather.proto`. Proto3 `HasField` SHALL distinguish "value is 0.0" from "value was not fetched".

#### Scenario: pm10 not requested
- **WHEN** njord does not request `pm10` from the API
- **THEN** the `AirQualitySample.pm10` field SHALL NOT be set on the wire
- **AND** a client calling `HasField("pm10")` SHALL get false

#### Scenario: pm25 is 0.0
- **WHEN** the API returns pm25 = 0.0 (a real measurement)
- **THEN** the `AirQualitySample.pm25` field SHALL be set with value 0.0
- **AND** a client calling `HasField("pm25")` SHALL get true

### Requirement: ha-njord handles absent proto fields as unavailable
ha-njord SHALL use `HasField` (via `_opt()`) for all domain sample fields that are `optional` in the proto. When a field is absent, the corresponding model attribute SHALL be `None` and the sensor SHALL report unavailable.

#### Scenario: wave_period absent in proto
- **WHEN** `MarineSample.wave_period` is not set (no `HasField`)
- **THEN** `MarineSampleData.wave_period` SHALL be `None`
- **AND** the wave_period sensor SHALL report `unavailable` (not 0.0)
