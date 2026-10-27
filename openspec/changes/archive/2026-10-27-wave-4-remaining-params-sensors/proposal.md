## Why

The Open-Meteo APIs expose many more variables than njord currently maps. Adding the remaining useful parameters to the point types now means future features can declare them in requirements without infrastructure work. This wave is pure completeness -- no new alert or score features, just filling out the data model across all three variable-carrying domains (Weather, AirQuality, Marine) and adding standalone HA sensors for elevated wind data (useful for wind energy monitoring).

## What Changes

### Weather domain -- new variables
- `surface_pressure` -- ground-level pressure (complements the existing `pressure_msl`)
- `wind_speed_80m`, `wind_direction_80m` -- 80 m hub-height wind for wind energy
- `wind_speed_120m`, `wind_direction_120m` -- 120 m hub-height wind
- `temperature_80m`, `temperature_120m` -- elevated temperature (inversion detection)

Each gets a `WeatherParams` flag, `WeatherPoint` field, `WeatherApiVariable` enum value, and `WeatherHourlyData` property.

### AirQuality domain -- new variables
- `carbon_dioxide` -- CO2 concentration (ppm)
- `methane` -- CH4 concentration (ug/m3)
- `us_aqi_pm2_5`, `us_aqi_pm10`, `us_aqi_nitrogen_dioxide`, `us_aqi_ozone`, `us_aqi_sulphur_dioxide`, `us_aqi_carbon_monoxide` -- US AQI sub-indices (parallel to the European AQI sub-indices from Wave 2)

Each gets an `AirQualityParams` flag, `AirQualityPoint` field, `AirQualityApiVariable` enum value, and `AirQualityHourlyData` property.

### Marine domain -- new variables
- `secondary_swell_wave_height`, `secondary_swell_wave_direction`, `secondary_swell_wave_period` -- secondary swell component
- `tertiary_swell_wave_height`, `tertiary_swell_wave_direction`, `tertiary_swell_wave_period` -- tertiary swell (GFS wave models only)
- `invert_barometer_height` -- inverse barometer effect on sea level

Each gets a `MarineParams` flag, `MarinePoint` field, `MarineApiVariable` enum value, and `MarineHourlyData` property.

### Elevated wind sensors
New HA sensor entities exposing 80 m and 120 m wind speed/direction for locations where wind energy monitoring is useful. Delivered as a feature (off by default) so the variables are only fetched when enabled.

## Capabilities

### New Capabilities
- `elevated-wind-sensors`: HA sensor entities for 80 m and 120 m wind speed and direction, useful for wind energy monitoring

### Modified Capabilities
- `domain-point-types`: WeatherPoint gains 6 fields, AirQualityPoint gains 8 fields, MarinePoint gains 7 fields

## Impact

- **Njord.Forecast**: `WeatherPoint` (+6 fields, +6 `WeatherParams` flags), `AirQualityPoint` (+8 fields, +8 `AirQualityParams` flags), `MarinePoint` (+7 fields, +7 `MarineParams` flags)
- **Njord.OpenMeteo**: `WeatherApiVariable` (+6), `AirQualityApiVariable` (+8), `MarineApiVariable` (+7), mapping extensions, hourly data types, response mapping point construction
- **Njord.Features**: new `ElevatedWind` feature in `Conditions/`
- **Njord.Grpc**: proto messages gain new fields, mapping extensions updated
- **protos/**: `weather.proto` updated for new weather/AQ/marine sample fields
- **docs/data/features.json**: elevated-wind-sensors entry
- **ha-njord**: proto stubs regenerated, new sensor entities for elevated wind

## Budget Impact

Per location, per poll cycle:
- Weather: +6 variables. Current ~31 variables = 4 calls. After: ~37 variables = 4 calls. No increase (still within `ceil(37/10) = 4`).
- AirQuality: +8 variables. Depends on current count; may add 0-1 calls.
- Marine: +7 variables. Marine is 1 call regardless of variable count.
- Total: +0-1 calls/cycle/location. Negligible.

## Non-goals

- Pressure-level variables (13 levels x 7 variables = 91 params -- too many, not useful for consumer features).
- Individual ensemble members.
- Historical/archive API endpoints.
- Daily aggregation variables (sunrise, sunset, daylight_duration -- these need a separate daily-data pipeline).
