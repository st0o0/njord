### Requirement: Alert sensor entity with trigger value as state

For each alert type, the integration SHALL create a `sensor` entity with `native_value` set to `alert.trigger_value`.

The entity ID pattern SHALL be `sensor.njord_{location}_{alert_type}_alert`.

#### Scenario: Active UV alert shows trigger value

- **WHEN** njord reports a UV alert with `trigger_value=8.5`
- **THEN** `sensor.njord_home_uv_alert` SHALL have `native_value=8.5`

#### Scenario: Inactive alert shows zero

- **WHEN** njord reports a frost alert with `severity="none"` and `trigger_value=0.0`
- **THEN** `sensor.njord_home_frost_alert` SHALL have `native_value=0.0`

#### Scenario: No enrichment data available

- **WHEN** enrichment data is not yet loaded
- **THEN** the alert sensor SHALL be unavailable

### Requirement: Unit of measurement per alert type

Each alert sensor SHALL have a `native_unit_of_measurement` based on its type:
- frost: `°C`
- heat: `°C`
- storm: `km/h`
- heavy_rain: `mm`
- uv: `UV`
- fog: `m`
- snow: `cm`
- pressure_drop: `hPa`
- thunderstorm: `J/kg`
- ice: `°C`
- wind_chill: `°C`
- visibility: `m`
- tropical_night: `°C`
- humidity: `%`

#### Scenario: UV alert shows UV unit

- **WHEN** the UV alert sensor is registered
- **THEN** its `native_unit_of_measurement` SHALL be `"UV"`

#### Scenario: Frost alert shows temperature unit

- **WHEN** the frost alert sensor is registered
- **THEN** its `native_unit_of_measurement` SHALL be `"°C"`

#### Scenario: Ice alert shows temperature unit

- **WHEN** the ice alert sensor is registered
- **THEN** its `native_unit_of_measurement` SHALL be `"°C"`

#### Scenario: Wind chill alert shows temperature unit

- **WHEN** the wind_chill alert sensor is registered
- **THEN** its `native_unit_of_measurement` SHALL be `"°C"`

#### Scenario: Visibility alert shows distance unit

- **WHEN** the visibility alert sensor is registered
- **THEN** its `native_unit_of_measurement` SHALL be `"m"`

#### Scenario: Tropical night alert shows temperature unit

- **WHEN** the tropical_night alert sensor is registered
- **THEN** its `native_unit_of_measurement` SHALL be `"°C"`

#### Scenario: Humidity alert shows percentage unit

- **WHEN** the humidity alert sensor is registered
- **THEN** its `native_unit_of_measurement` SHALL be `"%"`

### Requirement: Alert sensor attributes

Each alert sensor SHALL expose the following as `extra_state_attributes`:
- `severity` (always)
- `confidence` (always)
- `threshold` (always)
- `peak_value` (only when not None)
- `hours_until` (only when not None)
- `duration_hours` (only when not None)

#### Scenario: Full attributes on active alert

- **WHEN** the alert has `severity="orange", confidence=0.95, threshold=6.0, peak_value=9.2, hours_until=2, duration_hours=4`
- **THEN** `extra_state_attributes` SHALL contain all of these fields

#### Scenario: Minimal attributes on inactive alert

- **WHEN** the alert has `severity="none", confidence=0.0, threshold=0.0` and no optional fields
- **THEN** `extra_state_attributes` SHALL be `{"severity": "none", "confidence": 0.0, "threshold": 0.0}`

### Requirement: Alert sensors enabled by default

Alert sensor entities SHALL have `_attr_entity_registry_enabled_default = True` (HA default).

#### Scenario: Alert sensors active after setup

- **WHEN** the integration is set up
- **THEN** all 14 alert sensor entities SHALL be registered and enabled
