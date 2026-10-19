# Model Catalog

njord can poll any weather model available through the [Open-Meteo API](https://open-meteo.com/en/docs). This page lists the models njord knows coverage data for (`ModelCoverageRegistry`): their tier, region, maximum forecast horizon and resolution. Models not listed here still work; njord just cannot cap their horizon or check their coverage.

Choose models based on your location and how far ahead you need forecasts. Regional models offer higher resolution for shorter horizons; global models cover everywhere but at coarser resolution.

## Global models

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

## Continental models

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

## Regional models

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

## Tips for model selection

1. **Start small.** njord uses the Ensemble Mean API by default, which provides statistically robust forecasts from a single API call per location. Individual models are optional and can be added for comparison.

2. **Mix global and regional.** A global model provides the long-range outlook; a regional model gives higher-resolution short-term detail.

3. **Use per-location models for regional coverage.** Add regional models only to the locations they cover. See [per-location model configuration](/configuration/locations).

4. **Check horizon vs. your needs.** If you only display +24h forecasts, a regional model with a 48h horizon is plenty. Long-range planning needs global models.

5. **Consider the budget.** Each model-location pair is one API request per poll cycle. See [budget calculation](/configuration/budget) for details.
