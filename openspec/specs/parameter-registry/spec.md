# parameter-registry Specification

## Purpose

Static catalog of all known Open-Meteo forecast variables with metadata (API name, unit, HA device class, JSON key, group, granularity, value type) and group-based configuration resolution logic.

## Requirements

### Requirement: A static registry catalogs all known Open-Meteo forecast variables
The system SHALL maintain a static registry of all known Open-Meteo `/v1/forecast`
hourly and daily variables. Each entry SHALL carry: the API name (e.g.
`temperature_2m`), the unit string, the HA device class (or null), a short JSON
key for egress, a parameter group assignment, and the granularity (hourly or
daily). The `FriendlyName` field is removed -- it was never consumed by production
code.

#### Scenario: Registry contains all hourly weather variables
- **WHEN** the registry is queried for group `Weather` and granularity `Hourly`
- **THEN** it returns entries for at minimum: `temperature_2m`, `apparent_temperature`, `relative_humidity_2m`, `dew_point_2m`, `precipitation`, `rain`, `showers`, `snowfall`, `snow_depth`, `weather_code`, `cloud_cover`, `cloud_cover_low`, `cloud_cover_mid`, `cloud_cover_high`, `pressure_msl`, `surface_pressure`, `visibility`, `is_day`, `precipitation_probability`, `wind_speed_10m`, `wind_speed_80m`, `wind_speed_120m`, `wind_speed_180m`, `wind_direction_10m`, `wind_direction_80m`, `wind_direction_120m`, `wind_direction_180m`, `wind_gusts_10m`, `cape`, `freezing_level_height`, `vapour_pressure_deficit`

#### Scenario: Registry contains solar group
- **WHEN** the registry is queried for group `Solar`
- **THEN** it returns entries including: `shortwave_radiation`, `direct_radiation`, `diffuse_radiation`, `direct_normal_irradiance`, `global_tilted_irradiance`, `terrestrial_radiation`, `sunshine_duration`, `uv_index`, `uv_index_clear_sky` (hourly) and `shortwave_radiation_sum`, `sunshine_duration`, `uv_index_max`, `uv_index_clear_sky_max` (daily)

#### Scenario: Registry contains soil group
- **WHEN** the registry is queried for group `Soil`
- **THEN** it returns entries including: `soil_temperature_0cm`, `soil_temperature_6cm`, `soil_temperature_18cm`, `soil_temperature_54cm`, `soil_moisture_0_to_1cm`, `soil_moisture_1_to_3cm`, `soil_moisture_3_to_9cm`, `soil_moisture_9_to_27cm`, `soil_moisture_27_to_81cm`, `evapotranspiration`, `et0_fao_evapotranspiration` (hourly) and `et0_fao_evapotranspiration` (daily)

#### Scenario: Registry entry has no FriendlyName field
- **WHEN** a `ParameterDef` record is inspected
- **THEN** it has no `FriendlyName` property

### Requirement: Each registry entry carries correct HA metadata
Every registry entry SHALL carry the correct `unit_of_measurement` and `device_class` for HA sensor discovery. Entries without a standard HA device class SHALL carry `null`. The unit SHALL match what Open-Meteo returns when the request uses `wind_speed_unit=ms` and `timeformat=unixtime`.

#### Scenario: Wind parameters have device class wind_speed
- **WHEN** the registry entry for `wind_speed_10m` is queried
- **THEN** it reports unit `m/s` and device class `wind_speed`

#### Scenario: Cloud cover has no device class
- **WHEN** the registry entry for `cloud_cover` is queried
- **THEN** it reports unit `%` and device class `null`

#### Scenario: UV index has no device class
- **WHEN** the registry entry for `uv_index` is queried
- **THEN** it reports unit empty string and device class `null`

### Requirement: Parameter resolution from configuration
The registry SHALL resolve the active parameter set from a configuration of group names, extra variable names, and excluded variable names using the formula: `enabled = union(groups) ∪ extra \ exclude`. The resolved set SHALL be partitioned into hourly and daily subsets.

#### Scenario: Default configuration resolves Weather group
- **WHEN** configuration specifies `Groups: ["Weather"]` with no extras or excludes
- **THEN** the resolved set contains all Weather-group hourly and daily parameters

#### Scenario: Extra adds individual parameters from other groups
- **WHEN** configuration specifies `Groups: ["Weather"], Extra: ["uv_index"]`
- **THEN** the resolved set contains all Weather parameters plus `uv_index`

#### Scenario: Exclude removes parameters from groups
- **WHEN** configuration specifies `Groups: ["Weather"], Exclude: ["cape", "vapour_pressure_deficit"]`
- **THEN** the resolved set contains all Weather parameters except `cape` and `vapour_pressure_deficit`

#### Scenario: Unknown variable names are rejected
- **WHEN** configuration specifies `Extra: ["nonexistent_variable"]`
- **THEN** resolution fails with a validation error naming the unknown variable

#### Scenario: Empty resolved set is rejected
- **WHEN** configuration excludes all parameters from all enabled groups
- **THEN** resolution fails with a validation error

### Requirement: Parameters support a value-type discriminator
Each registry entry SHALL declare whether its values are numeric (`double?`) or time-string (`string?`). The discriminator SHALL be used by the domain and egress layers to handle sunrise/sunset and similar ISO-time daily values.

#### Scenario: Sunrise is a time-string parameter
- **WHEN** the registry entry for `sunrise` is queried
- **THEN** it reports value type `TimeString`

#### Scenario: Temperature is numeric
- **WHEN** the registry entry for `temperature_2m` is queried
- **THEN** it reports value type `Numeric`
