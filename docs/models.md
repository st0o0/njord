# Models

njord uses the Open-Meteo **Ensemble Mean** API. One call per location returns the ensemble-mean forecast, including `ensemble_std_dev`, the spread that drives alert confidence. By default `Njord:Ensemble:Models` is `ecmwf_ifs025_ensemble_mean`, `ncep_gefs025_ensemble_mean` and `google_weathernext2_ensemble_mean`; `Njord:Ensemble:Models` selects others (`ecmwf_aifs025_ensemble_mean`, `ukmo_global_ensemble_mean_20km`). Every listed model is fetched; the first stays the primary one for all features, and [EnsembleModelComparison](/configuration/model-comparison) passively compares the rest.

The spread (`ensemble_std_dev`) is computed for 15 variables: temperature_2m, precipitation, wind_speed_10m, pressure_msl, cloud_cover, wind_gusts_10m, cape, snowfall, rain, dew_point_2m, relative_humidity_2m, surface_pressure, wind_direction_10m, wet_bulb_temperature_2m and wind_speed_100m. Which of them are requested follows the enabled features.

The default model `ecmwf_ifs025_ensemble_mean` delivers neither `visibility` nor `uv_index`, so the `Forecast:Visibility` and `Forecast:Uv` alerts stay silent with it.

## Default ensemble models

| Model ID | Provider | Notes |
|---|---|---|
| `ecmwf_ifs025_ensemble_mean` | ECMWF | Primary model, 51 members, broad variable coverage |
| `ncep_gefs025_ensemble_mean` | NOAA/NCEP | GEFS, 31 members |
| `google_weathernext2_ensemble_mean` | Google | WeatherNext 2, see below |

### WeatherNext

`google_weathernext2_ensemble_mean` is an AI-based model (Google WeatherNext 2) with 64 ensemble members. Its native time step is 6-hourly, so hourly values are interpolated, and it delivers only a limited set of derived variables. Variables it does not provide stay empty for this model; the comparison features simply show what is available.

## Ensemble vs individual models

| | Ensemble Mean (used) | Individual models |
|---|---|---|
| API cost per location and cycle | `ceil(vars / 10) * (days / 14) * modelCount` effective calls | 1 per model |
| Uncertainty information | `ensemble_std_dev` per point | none |
| Resolution | Ensemble grid | Up to ~1 km for regional models |

What the Forecast request contains is set by [Weather](/configuration/weather) (`ForecastDays`).

## Open-Meteo model reference

The tables below list the models Open-Meteo offers. They are background reference only; njord accepts any ensemble-mean id Open-Meteo offers; the ids listed above are the known ensemble-mean models.

### Global models

| Model ID | Name | Provider | Max Horizon | Resolution |
|----------|------|----------|-------------|------------|
| `icon_global` | ICON Global | DWD | 180h | ~13 km |
| `gfs_global` | GFS Global | NOAA/NCEP | 384h | ~25 km |
| `gfs_seamless` | GFS Seamless | NOAA/NCEP | 384h | ~25 km |
| `ecmwf_ifs025` | ECMWF IFS 0.25° | ECMWF | 362h | ~25 km |
| `ifs` | ECMWF IFS | ECMWF | 362h | ~25 km |
| `ifs_seamless` | IFS Seamless | ECMWF | 362h | ~25 km |
| `aifs` | ECMWF AIFS | ECMWF | 360h | ~25 km |
| `ukmo_global_10km` | UKMO Global 10km | Met Office | 168h | ~10 km |
| `ukmo_global_deterministic_10km` | UKMO Global Deterministic | Met Office | 168h | ~10 km |
| `ukmo_seamless` | UKMO Seamless | Met Office | 168h | ~10 km |
| `arpege_world` | ARPEGE World | Météo-France | 108h | ~40 km |
| `arpege_seamless` | ARPEGE Seamless | Météo-France | 108h | ~40 km |
| `gem_global` | GEM Global | ECCC | 240h | ~25 km |
| `gem_seamless` | GEM Seamless | ECCC | 240h | ~25 km |
| `jma_gsm` | JMA GSM | JMA | 269h | ~20 km |
| `jma_seamless` | JMA Seamless | JMA | 269h | ~20 km |
| `kma_gdps` | KMA GDPS | KMA | 288h | ~12 km |
| `kma_seamless` | KMA Seamless | KMA | 288h | ~12 km |
| `bom_access_global` | BOM ACCESS Global | BOM | 240h | ~25 km |
| `cma_grapes_global` | CMA GRAPES Global | CMA | 128h | ~25 km |

### Continental models

Continental models cover Europe (latitude 34 to 72, longitude -12 to 45).

| Model ID | Name | Provider | Region | Max Horizon | Resolution |
|----------|------|----------|--------|-------------|------------|
| `icon_eu` | ICON-EU | DWD | Europe | 126h | ~7 km |
| `icon_seamless` | ICON Seamless | DWD | Europe | 180h | ~7 km |
| `arpege_europe` | ARPEGE Europe | Météo-France | Europe | 108h | ~11 km |
| `knmi_harmonie_arome_europe` | HARMONIE-AROME Europe (KNMI) | KNMI | Central & Northern Europe | 70h | ~5.5 km |
| `harmonie_arome_europe` | HARMONIE-AROME Europe | KNMI | Central & Northern Europe | 70h | ~5.5 km |
| `dmi_harmonie_arome_europe` | HARMONIE-AROME Europe (DMI) | DMI | Central & Northern Europe | 68h | ~5.5 km |
| `harmonie_arome_europe_dmi` | HARMONIE-AROME Europe (DMI) | DMI | Central & Northern Europe | 68h | ~5.5 km |
| `dmi_seamless` | DMI Seamless | DMI | Central & Northern Europe | 68h | ~5.5 km |
| `knmi_seamless` | KNMI Seamless | KNMI | Europe & Netherlands | 70h | ~5.5 km |

### Regional models

| Model ID | Name | Provider | Region | Max Horizon | Resolution |
|----------|------|----------|--------|-------------|------------|
| `icon_d2` | ICON-D2 | DWD | DE, CH, AT | 60h | ~2.2 km |
| `knmi_harmonie_arome_netherlands` | HARMONIE-AROME Netherlands | KNMI | NL, BE | 70h | ~2.5 km |
| `harmonie_arome_netherlands` | HARMONIE-AROME Netherlands | KNMI | NL, BE | 70h | ~2.5 km |
| `metno_nordic` | MetNo Nordic | MET Norway | NO, DK, SE, FI | 70h | ~2.5 km |
| `metno_nordic_seamless` | MetNo Nordic Seamless | MET Norway | NO, DK, SE, FI | 70h | ~2.5 km |
| `arome_france` | AROME France | Météo-France | France | 60h | ~1.3 km |
| `arome_france_hd` | AROME France HD | Météo-France | France | 60h | ~1.3 km |
| `arome_seamless` | AROME Seamless | Météo-France | France | 108h | ~1.3 km |
| `meteoswiss_icon_ch1` | MeteoSwiss ICON-CH1 | MeteoSwiss | CH & Central Europe | 42h | ~1.1 km |
| `meteoswiss_icon_ch2` | MeteoSwiss ICON-CH2 | MeteoSwiss | CH & Central Europe | 126h | ~2.2 km |
| `meteoswiss_seamless` | MeteoSwiss Seamless | MeteoSwiss | CH & Central Europe | 126h | ~2.2 km |
| `geosphere_arome_austria` | GeoSphere AROME | GeoSphere Austria | Austria | 69h | ~2.5 km |
| `geosphere_seamless` | GeoSphere Seamless | GeoSphere Austria | Austria | 69h | ~2.5 km |
| `ukmo_uk_2km` | UKMO UK 2km | Met Office | UK, Ireland | 63h | ~2 km |
| `ukmo_uk_deterministic_2km` | UKMO UK Deterministic 2km | Met Office | UK, Ireland | 63h | ~2 km |
| `hrrr_us_conus` | HRRR CONUS | NOAA/NCEP | US, Canada | 54h | ~3 km |
| `ncep_hrrr_conus` | NCEP HRRR CONUS | NOAA/NCEP | US, Canada | 54h | ~3 km |
| `gfs_hrrr` | GFS-HRRR Blend | NOAA/NCEP | US, Canada | 384h | ~3 km |
| `nbm_us_conus` | NBM CONUS | NOAA/NCEP | US | 276h | ~2.5 km |
| `ncep_nbm_conus` | NCEP NBM CONUS | NOAA/NCEP | US | 276h | ~2.5 km |
| `nam_us_conus` | NAM CONUS | NOAA/NCEP | US, Canada | 66h | ~12 km |
| `ncep_nam_conus` | NCEP NAM CONUS | NOAA/NCEP | US, Canada | 66h | ~12 km |
| `jma_msm` | JMA MSM | JMA | Japan, Korea | 78h | ~5 km |
| `kma_ldps` | KMA LDPS | KMA | Korea | 48h | ~1.5 km |
| `gem_regional` | GEM Regional | ECCC | North America | 90h | ~10 km |
| `gem_hrdps_continental` | GEM HRDPS Continental | ECCC | Canada, Northern US | 54h | ~2.5 km |
| `gem_hrdps_west` | GEM HRDPS West | ECCC | Western Canada | 48h | ~2.5 km |
