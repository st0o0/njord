## MODIFIED Requirements

### Requirement: A static registry catalogs all known Open-Meteo forecast variables
The system SHALL maintain a static registry of all known Open-Meteo `/v1/forecast`
hourly and daily variables. Each entry SHALL carry: the API name (e.g.
`temperature_2m`), the unit string, the HA device class (or null), a short JSON
key for egress, a parameter group assignment, and the granularity (hourly or
daily). The `FriendlyName` field is removed — it was never consumed by production
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
