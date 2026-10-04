## Purpose

Defines derived weather sensors (Beaufort, Wind Chill, Dewpoint Comfort) sourced from the enrichment stream's `DerivedData.by_horizon` entries.

## Requirements

### Requirement: Beaufort sensor
The integration SHALL expose a Beaufort sensor per location, sourced from `DerivedData.by_horizon`. The sensor SHALL display the current Beaufort scale value (0-12) using the horizon-offset helper to select the correct horizon entry. The sensor SHALL use `suggested_display_precision = 0` and icon `mdi:windsock`.

#### Scenario: Beaufort sensor shows current value
- **WHEN** derived data contains `by_horizon` with `h3` having `beaufort = 6` and the current horizon offset is 3
- **THEN** the sensor shows `6` with icon `mdi:windsock`

#### Scenario: Beaufort sensor unavailable without derived data
- **WHEN** enrichment data has no derived data for the location
- **THEN** the Beaufort sensor is unavailable

#### Scenario: Beaufort sensor advances with time
- **WHEN** 2 hours pass since the derived data was computed
- **THEN** the sensor reads from a horizon 2 steps ahead of the initial offset

### Requirement: Wind Chill sensor
The integration SHALL expose a Wind Chill sensor per location, sourced from `DerivedData.by_horizon`. The sensor SHALL use `device_class = SensorDeviceClass.TEMPERATURE`, `native_unit_of_measurement = UnitOfTemperature.CELSIUS`, `suggested_display_precision = 1`, and icon `mdi:snowflake-thermometer`.

#### Scenario: Wind Chill sensor shows current value
- **WHEN** derived data contains `by_horizon` with the current horizon having `wind_chill = -2.3`
- **THEN** the sensor shows `-2.3` with unit `°C`

#### Scenario: Wind Chill sensor shows None when horizon has no wind chill
- **WHEN** the current horizon entry exists but `wind_chill` is None
- **THEN** the sensor shows unknown state

### Requirement: Dewpoint Comfort sensor
The integration SHALL expose a Dewpoint Comfort sensor per location, sourced from `DerivedData.by_horizon`. The sensor SHALL display the comfort category string (e.g. "comfortable", "humid", "oppressive") and use icon `mdi:water-thermometer`.

#### Scenario: Dewpoint Comfort sensor shows category
- **WHEN** derived data contains the current horizon with `dewpoint_comfort = "comfortable"`
- **THEN** the sensor shows `comfortable`

#### Scenario: Dewpoint Comfort sensor shows None when not available
- **WHEN** the current horizon entry exists but `dewpoint_comfort` is None
- **THEN** the sensor shows unknown state

### Requirement: Derived sensors are disabled by default
All derived sensors SHALL have `_attr_entity_registry_enabled_default = False`.

#### Scenario: Derived sensors disabled on first setup
- **WHEN** the integration is set up for the first time
- **THEN** Beaufort, Wind Chill, and Dewpoint Comfort sensors are registered but disabled

### Requirement: Derived sensors support dynamic addition
The derived sensor factory SHALL be registered on the coordinator, enabling entity creation for locations discovered after initial setup.

#### Scenario: Late derived sensor creation
- **WHEN** a new location "bern" is detected via config stream and enrichment data with derived data arrives
- **THEN** Beaufort, Wind Chill, and Dewpoint Comfort sensors are created for "bern"

### Requirement: Derived sensors have translation keys
All derived sensors SHALL have `_attr_translation_key` set and corresponding entries in `strings.json` and `translations/de.json`.

#### Scenario: German translations exist
- **WHEN** HA language is set to German
- **THEN** Beaufort shows as "Beaufort", Wind Chill as "Windchill", Dewpoint Comfort as "Taupunkt-Komfort"
