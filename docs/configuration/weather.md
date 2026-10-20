# Weather

Options for the Weather domain under `Njord:Weather`. The model defaults to the ECMWF IFS ensemble mean and can be changed with `Models`; the variable list is built into the client. Air Quality, Flood and Marine use fixed variable sets too.

```json
{
  "Njord": {
    "Weather": {
      "ForecastDays": 4
    }
  }
}
```

As environment variables: `Njord__Weather__IncludeSolar=true`, `Njord__Weather__ForecastDays=4`.

| Option | Default | Description |
|--------|---------|-------------|
| `ForecastDays` | `4` | Days of forecast to request (1 to 16) |
| `Models` | `["ecmwf_ifs025_ensemble_mean"]` | Ensemble-mean model ids; the first is the primary. Known: `ecmwf_ifs025_ensemble_mean`, `ecmwf_aifs025_ensemble_mean`, `ukmo_global_ensemble_mean_20km`. Models beyond the first only take effect with [Model comparison](./model-comparison). The old `Model` key is rejected. |

## API call weight

The weather request asks only for the hourly variables the enabled features need (the union of their requirements). Open-Meteo counts a request as `ceil(variables / 10)` calls, doubled for every 14 forecast days started:

```
weather weight = ceil(variables / 10) x ceil(ForecastDays / 14) x model count
```

The weight is charged to the Weather domain only; every other domain costs one call per location and cycle. See the [budget](./budget) projection.
