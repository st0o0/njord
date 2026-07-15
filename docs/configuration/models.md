# Models

The global `Models` list defines which Open-Meteo weather models njord polls for every location. Each model produces a separate Home Assistant device with its own set of sensors.

## Configuration

```json
{
  "Njord": {
    "Models": [
      "icon_eu",
      "ecmwf_ifs025",
      "gfs_seamless"
    ]
  }
}
```

Models can also be added [per location](./locations) to include regional models that only cover specific areas.

## Verified model IDs

These models have been tested with njord and are known to work:

| Model ID | Coverage | Notes |
|----------|----------|-------|
| `icon_d2` | Germany, Switzerland, Austria | High resolution (~2 km), 48h horizon |
| `icon_eu` | Europe | ~7 km, 120h horizon |
| `icon_global` | Global | ~13 km, 180h horizon |
| `ecmwf_ifs025` | Global | ECMWF high-res, 240h horizon |
| `gfs_seamless` | Global | US GFS blended, 384h horizon |
| `ukmo_seamless` | Global | UK Met Office, 168h horizon |
| `arpege_europe` | Europe | Meteo-France, 96h horizon |
| `knmi_harmonie_arome_europe` | Europe | KNMI, 60h horizon |
| `knmi_harmonie_arome_netherlands` | Netherlands, Belgium | KNMI high-res, 48h horizon |
| `dmi_harmonie_arome_europe` | Europe | Danish Met, 60h horizon |
| `meteoswiss_icon_ch1` | Switzerland | ~1 km, 33h horizon |
| `meteoswiss_icon_ch2` | Switzerland | ~2 km, 45h horizon |
| `metno_nordic` | Scandinavia | Norwegian Met, 60h horizon |

See the full [model catalog](/models) for all available models with resolution and coverage details.

## Coverage validation at startup

njord validates each model against a built-in coverage registry of geographic bounding boxes. If a model does not cover a configured location, startup logs a warning. The API itself returns HTTP 400 for out-of-coverage requests, which njord handles by skipping the model for that location.

::: warning
Adding a regional model (like `icon_d2` or `meteoswiss_icon_ch1`) to the global model list will cause API errors for locations outside its coverage area. Use [per-location models](./locations) instead.
:::

## Choosing models

- Start with 2--3 global models (e.g. `ecmwf_ifs025`, `gfs_seamless`, `icon_global`) for broad coverage.
- Add regional models per location for higher resolution where available.
- More models improve consensus accuracy but increase API usage — see [budget](./budget).
