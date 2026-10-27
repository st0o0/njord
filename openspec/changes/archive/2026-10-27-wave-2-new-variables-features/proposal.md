## Why

The Forecast API, Air Quality API, and Marine API each provide variables that njord does not yet consume. These untapped variables enable new features (shower alerts, soil moisture, smog risk, granular AQI, ocean currents) and improve existing ones (thunderstorm prediction via CIN/lifted index, fog detection via low cloud cover). With the ForecastClient now serving deterministic data, several variables that were unavailable or unreliable on the Ensemble API are now usable. This wave adds the variable infrastructure across three domains and builds seven new features on top.

## What Changes

### New Weather variables (WeatherParams + WeatherPoint + WeatherApiVariable + WeatherHourlyData)
- `showers` -- convective precipitation amount, separate from frontal rain
- `soil_moisture_0_to_1cm` -- topsoil volumetric moisture (m3/m3)
- `soil_moisture_1_to_3cm` -- subsoil volumetric moisture (m3/m3)
- `et0_fao_evapotranspiration` -- reference evapotranspiration (mm/h)
- `vapour_pressure_deficit` -- plant water stress indicator (kPa)
- `boundary_layer_height` -- atmospheric mixing layer depth (m)
- `cloud_cover_low` -- low cloud cover (%)
- `cloud_cover_mid` -- mid-level cloud cover (%)
- `cloud_cover_high` -- high cloud cover (%)
- `convective_inhibition` -- CIN, energy needed to trigger convection (J/kg)
- `lifted_index` -- atmospheric stability index

### New Air Quality variables (AirQualityParams + AirQualityPoint + AirQualityApiVariable)
- `ragweed_pollen` -- 6th pollen type
- `european_aqi_pm2_5` -- PM2.5 sub-index
- `european_aqi_pm10` -- PM10 sub-index
- `european_aqi_nitrogen_dioxide` -- NO2 sub-index
- `european_aqi_ozone` -- ozone sub-index
- `european_aqi_sulphur_dioxide` -- SO2 sub-index
- `aerosol_optical_depth` -- atmospheric turbidity

### New Marine variables (MarineParams + MarinePoint + MarineApiVariable)
- `ocean_current_velocity` -- current speed (km/h)
- `ocean_current_direction` -- current direction (degrees)

### New features
- **ShowerAlert** (`Weather:Alerts`): convective shower warning when showers exceed 5 mm/h. Requires Ensemble for confidence.
- **SoilMoisture** (`Lifestyle:Conditions`): soil moisture sensor reporting dry/optimal/wet status. No Ensemble.
- **Evapotranspiration** (`Lifestyle:Conditions`): daily water loss in mm for irrigation guidance. No Ensemble.
- **SmogRisk** (`AirQuality:Alerts`): boundary layer height <500 m combined with PM2.5 >25 ug/m3 triggers smog warning. Cross-domain Weather + AirQuality. No Ensemble (local phenomenon).
- **RagweedPollen** sensor: 6th pollen type matching existing pollen sensor pattern.
- **GranularAqi** sensors: 5 per-pollutant European AQI sub-index sensors.
- **OceanCurrents** (`Marine:Conditions`): current speed and direction condition for maritime safety.

## Capabilities

### New Capabilities
- `shower-alert`: Convective shower alert based on the `showers` weather variable
- `soil-moisture`: Soil moisture condition sensor with dry/optimal/wet thresholds
- `evapotranspiration`: Evapotranspiration condition sensor for irrigation guidance
- `smog-risk`: Cross-domain smog alert combining boundary layer height with PM2.5
- `ragweed-pollen`: Ragweed pollen sensor extending the existing pollen pattern
- `granular-aqi`: Per-pollutant European AQI sub-index sensors
- `ocean-currents`: Ocean current velocity and direction condition

### Modified Capabilities
- `domain-point-types`: WeatherPoint gains 11 fields, AirQualityPoint gains 7 fields, MarinePoint gains 2 fields

## Impact

- **Njord.Forecast**: `WeatherPoint` gains 11 fields, `WeatherParams` gains 11 flags. `AirQualityPoint` gains 7 fields, `AirQualityParams` gains 7 flags. `MarinePoint` gains 2 fields, `MarineParams` gains 2 flags.
- **Njord.OpenMeteo**: `WeatherApiVariable` gains 11 entries, `WeatherHourlyData` gains 11 fields, mapping updated. Same pattern for AQ and Marine clients.
- **Njord.Features**: 7 new feature rules with config classes, registered in `AddFeatures`.
- **Njord.Messages**: New result records for each feature.
- **Njord.Grpc**: Proto messages updated for new point fields, new enrichment event oneof cases.
- **protos/**: `weather.proto` updated with new fields on samples and new enrichment events.
- **docs/data/features.json**: New feature entries.
- **Config Builder**: Updated for new features and their domain requirements.

## Budget Impact

Per location, per poll cycle:

| Domain | Current variables | New variables | Call weight change |
|---|---|---|---|
| Weather | ~31 | +11 = ~42 | ceil(42/10) = 5 (was 4), **+1 call** |
| AirQuality | ~17 | +7 = ~24 | Still 1 call (AQ has no variable-based weight) |
| Marine | ~13 | +2 = ~15 | Still 1 call |

Total impact: +1 call/cycle/location for weather. Negligible against 300k/month budget.

## Non-goals

- Pressure-level variables (temperature/wind/humidity at altitude).
- Individual ensemble member data.
- Daily aggregation variables (min/max/sum).
- US AQI sub-indices (only European AQI sub-indices in this wave).
