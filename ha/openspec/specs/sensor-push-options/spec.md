## Purpose

Provides per-location sensor entity configuration in the Options Flow, allowing users to select which HA sensors forward readings to njord's SensorService.

## Requirements

### Requirement: Sensor push step in Options Flow
The OptionsFlow SHALL include a second step (`sensors`) after the existing `init` step. This step SHALL display entity selectors for each configured njord location and sensor kind.

#### Scenario: User opens options and navigates to sensor step
- **WHEN** the user completes the init step and proceeds
- **THEN** a sensor push configuration form is shown with per-location entity selectors

#### Scenario: Single location shows two selectors
- **WHEN** the catalog contains one location "home"
- **THEN** the sensor step shows two entity selectors: "home Indoor Temperature" and "home Indoor Humidity"

#### Scenario: Multiple locations show selectors per location
- **WHEN** the catalog contains locations "home" and "büro"
- **THEN** the sensor step shows four entity selectors: temperature and humidity for each location

### Requirement: Entity selectors filtered by device class
Each entity selector SHALL be filtered to the appropriate HA `device_class`. Temperature selectors SHALL filter to `device_class: temperature`. Humidity selectors SHALL filter to `device_class: humidity`. Selectors SHALL allow multiple entities (`multiple=True`).

#### Scenario: Temperature selector shows only temperature sensors
- **WHEN** the user opens the sensor push step
- **THEN** the temperature entity picker only offers entities with `device_class: temperature`

#### Scenario: Multiple entities can be selected
- **WHEN** the user selects sensor.wz_temp and sensor.sz_temp for home indoor temperature
- **THEN** both entities are stored in the configuration

### Requirement: Sensor push configuration storage
Selected entities SHALL be stored in `entry.options["sensor_push"]` as a nested dict: `{location: {kind: [entity_id, ...]}}`. Empty selections SHALL be stored as empty lists. If no sensor_push key exists in options, the feature is completely off.

#### Scenario: Configuration is persisted
- **WHEN** the user selects sensor.wz_temp for home indoor_temperature and submits
- **THEN** `entry.options["sensor_push"]` equals `{"home": {"indoor_temperature": ["sensor.wz_temp"], "indoor_humidity": []}}`

#### Scenario: Empty configuration
- **WHEN** the user selects no entities for any location and submits
- **THEN** `entry.options["sensor_push"]` contains empty lists for all kinds

### Requirement: Sensor push config change triggers reload
Changing the sensor push configuration SHALL trigger a config entry reload to update the state listener. If the sensor push configuration is unchanged, no reload SHALL be triggered for that reason.

#### Scenario: Sensor mapping changed
- **WHEN** the user adds or removes a sensor entity from the mapping
- **THEN** `async_reload` is called on the config entry

#### Scenario: Sensor mapping unchanged
- **WHEN** the user submits the sensor step without changing any entity selections
- **THEN** no reload is triggered due to sensor push changes

### Requirement: Sensor push step UI strings
`strings.json` and `translations/de.json` SHALL include labels for the sensor push step and all entity selector fields.

#### Scenario: English strings present
- **WHEN** the options flow sensor step is rendered in English
- **THEN** all field labels are present from `strings.json`

#### Scenario: German strings present
- **WHEN** the options flow sensor step is rendered in German
- **THEN** all field labels are present from `translations/de.json`
