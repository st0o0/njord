# Models

Weather data comes from the Open-Meteo **Ensemble Mean** API, one call per location per cycle. You do not select ensemble members.

The `Models` list is still required (at least one id). It has two roles:

- **Catalog:** the effective list per location is returned by `GetCatalog` over gRPC.
- **Coverage validation:** each id is checked against a bounding-box registry at startup.

It does not add API calls, and it does not create per-model entities.

## Configuration

```json
{
  "Njord": {
    "Models": ["icon_eu"]
  }
}
```

Models can also be set [per location](./locations) for regional ids that only cover specific areas.

## Verified model IDs

| Model ID | Coverage | Notes |
|----------|----------|-------|
| `icon_d2` | Germany, Switzerland, Austria | ~2 km |
| `icon_eu` | Europe | ~7 km |
| `icon_global` | Global | ~13 km |
| `ecmwf_ifs025` | Global | ECMWF |
| `gfs_seamless` | Global | NOAA GFS blend |
| `ukmo_seamless` | Global | UK Met Office |
| `arpege_europe` | Europe | Météo-France |
| `knmi_harmonie_arome_europe` | Europe | KNMI |
| `knmi_harmonie_arome_netherlands` | Netherlands, Belgium | KNMI |
| `dmi_harmonie_arome_europe` | Europe | Danish Met |
| `meteoswiss_icon_ch1` | Switzerland | ~1 km |
| `meteoswiss_icon_ch2` | Switzerland | ~2 km |
| `metno_nordic` | Scandinavia | Norwegian Met |

See the [model catalog](/models) for resolution, region and horizons.

## Coverage validation

If a model does not cover a configured location, startup logs a warning.

::: warning
Put regional ids (`icon_d2`, `meteoswiss_icon_ch1`) in the per-location `Models` of the locations they cover, not in the global list.
:::

## Choosing a model id

Pick one global or continental id that covers all locations (`icon_eu` for Europe, `icon_global` elsewhere). Add regional ids per location only if you want the catalog to reflect them. Budget is unaffected: see [Budget](./budget).
