## Purpose

Defines how new HA entities are created at runtime when the config stream delivers previously unknown locations, using factory callbacks registered during platform setup.

## Requirements

### Requirement: Platform setups register entity factories
Each platform setup (weather, sensor, binary_sensor) SHALL register an entity factory callback on the coordinator so that new entities can be created later at runtime.

#### Scenario: Weather platform registers factory
- **WHEN** `weather.async_setup_entry` runs
- **THEN** it stores its `async_add_entities` callback and a factory function on the coordinator

#### Scenario: Sensor platform registers factory
- **WHEN** `sensor.async_setup_entry` runs
- **THEN** it stores its `async_add_entities` callback and a factory function on the coordinator

### Requirement: New locations trigger entity creation
When the config stream delivers a location not previously known, the coordinator SHALL invoke all registered entity factories for that location.

#### Scenario: New location detected
- **WHEN** `StreamConfig` delivers a config with location "bern" that was not in the initial config
- **THEN** weather entities (one per model) and sensor/binary_sensor entities are created for "bern" via the registered factories

#### Scenario: Existing location is not duplicated
- **WHEN** `StreamConfig` delivers a config containing only already-known locations
- **THEN** no new entities are created

### Requirement: Known locations are tracked
The coordinator SHALL maintain a set of known location names, initialized from the first refresh config data.

#### Scenario: Initial locations are tracked
- **WHEN** `async_config_entry_first_refresh()` completes with locations ["lucerne", "zurich"]
- **THEN** `_known_locations` contains {"lucerne", "zurich"}

#### Scenario: Dynamically added location is tracked
- **WHEN** a new location "bern" triggers entity creation
- **THEN** "bern" is added to `_known_locations` and subsequent config events with "bern" do not trigger creation again

### Requirement: Removed locations are ignored
When the config stream delivers a config that no longer contains a previously known location, the coordinator SHALL NOT remove entities or modify `_known_locations`.

#### Scenario: Location removed in njord
- **WHEN** `StreamConfig` delivers a config without location "zurich" that was previously known
- **THEN** no entities are removed and "zurich" remains in `_known_locations`
