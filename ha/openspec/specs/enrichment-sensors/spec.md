## Purpose

Defines enrichment-derived sensor entities (activity indices, frost, trend, VPD) and their HA integration behaviour (icons, translations, disabled-by-default, dynamic addition).

## Requirements

### Requirement: Sensor entities have icons
Every sensor entity SHALL have an `_attr_icon` set to an appropriate MDI icon.

#### Scenario: Index sensors show activity icons
- **WHEN** the BBQ Index sensor is displayed in the HA dashboard
- **THEN** it shows the `mdi:grill` icon

### Requirement: Sensor entities have translation keys
Every sensor entity SHALL have a `_attr_translation_key` set, and corresponding entries in `strings.json` and `translations/de.json`.

#### Scenario: Sensor name is translatable
- **WHEN** HA language is set to German
- **THEN** the BBQ Index sensor shows as "Grillwetter-Index"

### Requirement: Frost hours sensor
The integration SHALL expose a Frost Hours sensor per location, sourced from `IndexData.frost.hours_until` (was `IndexData.frost_hours`). The sensor SHALL set `device_class = SensorDeviceClass.DURATION` and `native_unit_of_measurement = UnitOfTime.HOURS` with `suggested_display_precision = 0`.

#### Scenario: Frost hours sensor shows value
- **WHEN** enrichment data contains `frost = FrostData(hours_until=4, confidence=0.85)`
- **THEN** the sensor shows `4` with unit `h` and icon `mdi:snowflake-thermometer`

#### Scenario: Frost hours sensor shows None when not available
- **WHEN** enrichment data has indices but `frost` is None
- **THEN** the sensor shows unknown state

### Requirement: Frost confidence sensor
The integration SHALL expose a Frost Confidence sensor per location, sourced from `IndexData.frost.confidence` (was `IndexData.frost_confidence`), displayed as a percentage (0-100). The sensor SHALL use raw string unit `"%"` and `suggested_display_precision = 0`.

#### Scenario: Frost confidence sensor shows percentage
- **WHEN** enrichment data contains `frost = FrostData(hours_until=4, confidence=0.85)`
- **THEN** the sensor shows `85.0` with unit `%` and icon `mdi:snowflake-check`

#### Scenario: Frost confidence sensor shows None when not available
- **WHEN** enrichment data has indices but `frost` is None
- **THEN** the sensor shows unknown state

### Requirement: New sensors have translation keys
All new sensors SHALL have `_attr_translation_key` set and corresponding entries in `strings.json` and `translations/de.json`.

#### Scenario: German translations exist
- **WHEN** HA language is set to German
- **THEN** Frost Hours shows as "Froststunden", Frost Confidence as "Frostwahrscheinlichkeit"

### Requirement: Enrichment sensors are disabled by default
All enrichment sensor entities SHALL have `_attr_entity_registry_enabled_default = False`, so they appear in the entity registry but are disabled until the user explicitly enables them.

#### Scenario: Index sensor is disabled by default
- **WHEN** the integration is set up for the first time
- **THEN** index sensors (laundry, outdoor, cycling, etc.) are registered but disabled

#### Scenario: User enables a sensor
- **WHEN** a user enables a disabled sensor in the HA entity registry
- **THEN** the sensor becomes active and shows its current value

### Requirement: Trend sensor shows weather stability
The integration SHALL expose a Weather Trend sensor per location. The sensor's primary state SHALL be `weather_change_description` from `TrendData`. The `stability_label` SHALL be included as an extra state attribute alongside existing trend attributes (`precip_starts_in_hours`, `precip_ends_in_hours`, `temp_max_in_hours`, `temp_min_in_hours`, `reliable_hours`, `stability_ratio`, `decay_rate`, `parameter_trends`).

#### Scenario: Trend sensor shows weather description
- **WHEN** enrichment data contains `weather_change_description = "Rain starting in 3 hours, temperature dropping"`
- **THEN** the sensor state is `"Rain starting in 3 hours, temperature dropping"`

#### Scenario: Trend sensor shows stability label as attribute
- **WHEN** enrichment data contains `stability_label = "stable"` and `weather_change_description = "No significant changes expected"`
- **THEN** the sensor state is `"No significant changes expected"` and `extra_state_attributes` includes `{"stability_label": "stable"}`

#### Scenario: Trend sensor shows None when no description
- **WHEN** enrichment data has trends but `weather_change_description` is None
- **THEN** the sensor state is unknown

#### Scenario: Trend sensor unavailable without trend data
- **WHEN** enrichment data has no trends
- **THEN** the trend sensor is unavailable

### Requirement: Sensor entities support dynamic addition
The sensor and binary_sensor platforms SHALL store their `async_add_entities` callbacks and factory functions on the coordinator during setup, enabling entity creation for locations discovered after initial setup.

#### Scenario: Late sensor creation
- **WHEN** a new location "bern" is detected via config stream and enrichment data arrives for it
- **THEN** sensor and binary_sensor entities are created for "bern" matching the same patterns as initially created locations

#### Scenario: No duplicate sensors on repeated config events
- **WHEN** the config stream sends multiple events containing "bern" after it was already added
- **THEN** no duplicate sensor entities are created
