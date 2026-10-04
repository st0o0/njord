## Purpose

Automatically forwards configured HA sensor state changes to njord's SensorService via gRPC Push, using the mappings configured in the Options Flow.

## Requirements

### Requirement: State listener registration
On integration setup, if `sensor_push` is present in options and contains at least one entity_id, the integration SHALL register a state change listener for all configured entity IDs. The listener SHALL be removed on unload.

#### Scenario: Listener registered on setup
- **WHEN** the integration sets up with `sensor_push` containing `{"home": {"indoor_temperature": ["sensor.wz_temp"]}}`
- **THEN** a state change listener is registered for `sensor.wz_temp`

#### Scenario: No listener when sensor_push absent
- **WHEN** the integration sets up without a `sensor_push` key in options
- **THEN** no state change listener is registered

#### Scenario: No listener when all lists empty
- **WHEN** `sensor_push` exists but all entity lists are empty
- **THEN** no state change listener is registered

#### Scenario: Listener removed on unload
- **WHEN** the integration is unloaded
- **THEN** the state change listener is removed

### Requirement: Push on state change
When a configured entity's state changes to a valid numeric value, the listener SHALL call `client.push_sensor(kind, location, value, source=entity_id)` with the new state value.

#### Scenario: Temperature sensor updates
- **WHEN** sensor.wz_temp changes from "22.0" to "22.5"
- **THEN** `client.push_sensor("indoor_temperature", "home", 22.5, source="sensor.wz_temp")` is called

#### Scenario: Humidity sensor updates
- **WHEN** sensor.bad_hum changes from "60" to "65"
- **THEN** `client.push_sensor("indoor_humidity", "home", 65.0, source="sensor.bad_hum")` is called

### Requirement: Non-numeric states are ignored
When a configured entity's state is not a valid float (e.g. `unavailable`, `unknown`, empty string), the listener SHALL skip the push without logging an error.

#### Scenario: Entity becomes unavailable
- **WHEN** sensor.wz_temp changes to "unavailable"
- **THEN** no push is made and no error is logged

#### Scenario: Entity state is unknown
- **WHEN** sensor.wz_temp changes to "unknown"
- **THEN** no push is made and no error is logged

### Requirement: Push errors are logged and dropped
If `client.push_sensor()` raises an exception, the listener SHALL log a warning and continue. No retry or queue SHALL be used.

#### Scenario: gRPC call fails
- **WHEN** `client.push_sensor()` raises a gRPC error
- **THEN** a warning is logged with the entity_id and error
- **AND** the listener continues processing future state changes

### Requirement: Reverse map for entity lookup
The listener SHALL maintain a reverse map from `entity_id` to `(location, kind)` built from the `sensor_push` options. This map SHALL be used to resolve the location and kind for each state change event.

#### Scenario: Multiple entities mapped
- **WHEN** sensor.wz_temp and sensor.sz_temp are both mapped to home/indoor_temperature
- **THEN** both entity_ids resolve to `("home", "indoor_temperature")` in the reverse map
