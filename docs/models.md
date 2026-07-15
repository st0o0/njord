# Model Catalog

njord can poll any weather model available through the [Open-Meteo API](https://open-meteo.com/en/docs). This page lists common models with their coverage and capabilities.

Choose models based on your location and how far ahead you need forecasts. Regional models offer higher resolution for shorter horizons; global models cover everywhere but at coarser resolution.

## Global models

| Model ID | Provider | Max Horizon | Resolution | Notes |
|----------|----------|-------------|------------|-------|
| `icon_global` | DWD | 180h | ~13 km | German Weather Service global |
| `ecmwf_ifs025` | ECMWF | 240h | ~25 km | European Centre high-res |
| `gfs_seamless` | NOAA | 384h | ~13 km | US GFS blended with HRRR |
| `ukmo_seamless` | UK Met Office | 168h | ~10 km | UK Met Office global |
| `arpege_world` | Meteo-France | 96h | ~40 km | French global model |
| `gem_seamless` | ECCC | 240h | ~15 km | Canadian blended model |
| `jma_seamless` | JMA | 264h | ~20 km | Japan Meteorological Agency |
| `kma_seamless` | KMA | 288h | ~12 km | Korean Meteorological Administration |
| `bom_access_global` | BoM | 240h | ~25 km | Australian Bureau of Meteorology |
| `cma_grapes_global` | CMA | 240h | ~15 km | China Meteorological Administration |

## European models

| Model ID | Provider | Max Horizon | Resolution | Notes |
|----------|----------|-------------|------------|-------|
| `icon_eu` | DWD | 120h | ~7 km | DWD European domain |
| `arpege_europe` | Meteo-France | 96h | ~11 km | French European domain |
| `knmi_harmonie_arome_europe` | KNMI | 60h | ~5.5 km | Dutch Met pan-European |
| `dmi_harmonie_arome_europe` | DMI | 60h | ~5.5 km | Danish Met pan-European |

## Regional models

| Model ID | Provider | Region | Max Horizon | Resolution |
|----------|----------|--------|-------------|------------|
| `icon_d2` | DWD | Germany, Switzerland, Austria | 48h | ~2 km |
| `knmi_harmonie_arome_netherlands` | KNMI | Netherlands, Belgium | 48h | ~2.5 km |
| `metno_nordic` | MET Norway | Scandinavia | 60h | ~2.5 km |
| `meteoswiss_icon_ch1` | MeteoSwiss | Switzerland | 33h | ~1 km |
| `meteoswiss_icon_ch2` | MeteoSwiss | Switzerland | 45h | ~2 km |
| `arome_france` | Meteo-France | France | 48h | ~1.3 km |
| `arome_france_hd` | Meteo-France | France | 48h | ~1.3 km |
| `geosphere_arome_austria` | GeoSphere | Austria | 48h | ~2.5 km |
| `arpae_2i` | ARPAE | Italy | 48h | ~2 km |
| `ukmo_uk_2km` | UK Met Office | United Kingdom, Ireland | 54h | ~2 km |
| `hrrr_us_conus` | NOAA | Continental US | 48h | ~3 km |
| `nbm_us_conus` | NOAA | Continental US | 192h | ~2.5 km |
| `jma_msm` | JMA | Japan, Korea | 78h | ~5 km |
| `kma_ldps` | KMA | Korea | 48h | ~1.5 km |
| `gem_hrdps_continental` | ECCC | Canada | 48h | ~2.5 km |

## Tips for model selection

1. **Start small.** Two or three models are enough to get useful forecasts and enable consensus. Adding more models increases API usage proportionally.

2. **Mix global and regional.** A global model provides the long-range outlook; a regional model gives higher-resolution short-term detail.

3. **Use per-location models for regional coverage.** Add regional models only to the locations they cover — see [per-location model configuration](/configuration/locations).

4. **Check horizon vs. your needs.** If you only display +24h forecasts, a regional model with a 48h horizon is plenty. Long-range planning needs global models.

5. **Consider the budget.** Each model-location pair is one API request per poll cycle. See [budget calculation](/configuration/budget) for details.
