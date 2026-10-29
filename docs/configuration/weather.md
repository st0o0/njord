# Forecast and Ensemble

Options for the Forecast domain under `Njord:Forecast` and the Ensemble domain under `Njord:Ensemble`. The Forecast API uses best_match (automatic high-resolution model blending) unless `Models` is set; the variable list follows the enabled features. Air Quality, Flood and Marine use fixed variable sets.

```json
{
  "Njord": {
    "Forecast": { "ForecastDays": 4, "PastHours": 0 },
    "Ensemble": { "ForecastDays": 4, "Models": ["ecmwf_ifs025_ensemble_mean"] }
  }
}
```

As environment variables: `Njord__Forecast__ForecastDays=4`, `Njord__Ensemble__ForecastDays=4`, `Njord__Ensemble__Models__0=ecmwf_ifs025_ensemble_mean`.

`Njord:Forecast`

| Option | Default | Description |
|--------|---------|-------------|
| `ForecastDays` | `4` | Days of forecast to request (1 to 16) |
| `PastHours` | `0` | Hours of past data to include (0 to 96) |
| `Models` | `[]` | Optional forecast model ids; empty uses best_match |

`Njord:Ensemble`

| Option | Default | Description |
|--------|---------|-------------|
| `ForecastDays` | `4` | Days of ensemble forecast to request (1 to 35) |
| `Models` | `["ecmwf_ifs025_ensemble_mean"]` | Ensemble-mean model ids (required, no duplicates); the first is the primary. Known: `ecmwf_ifs025_ensemble_mean`, `ecmwf_aifs025_ensemble_mean`, `ukmo_global_ensemble_mean_20km`. Models beyond the first only take effect with [Model comparison](./model-comparison). |

## API call weight

Each request asks only for the hourly variables the enabled features need (the union of their requirements). Open-Meteo counts a request as `ceil(variables / 10)` calls, doubled for every 14 forecast days started:

```
forecast weight = ceil(variables / 10) x ceil(Forecast.ForecastDays / 14)
ensemble weight = ceil(variables / 10) x ceil(Ensemble.ForecastDays / 14) x model count
```

Every other domain costs one call per location and cycle. See the [budget](./budget) projection.
