# Forecast and Ensemble

Options for the Forecast domain under `Njord:Forecast` and the Ensemble domain under `Njord:Ensemble`. The Forecast API uses best_match (automatic high-resolution model blending) unless `Models` is set. All domains request only the variables the enabled features need (the union of their requirements).

```json
{
  "Njord": {
    "Forecast": { "ForecastDays": 4, "PastHours": 0 },
    "Ensemble": { "ForecastDays": 4, "Models": ["ecmwf_ifs025_ensemble_mean", "ncep_gefs025_ensemble_mean", "google_weathernext2_ensemble_mean"] }
  }
}
```

As environment variables: `Njord__Forecast__ForecastDays=4`, `Njord__Ensemble__ForecastDays=4`, `Njord__Ensemble__Models__0=ecmwf_ifs025_ensemble_mean`, `Njord__Ensemble__Models__1=ncep_gefs025_ensemble_mean`, `Njord__Ensemble__Models__2=google_weathernext2_ensemble_mean`.

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
| `Models` | `["ecmwf_ifs025_ensemble_mean", "ncep_gefs025_ensemble_mean", "google_weathernext2_ensemble_mean"]` | Ensemble-mean model ids (required, no duplicates); the first is the primary and drives all features. Known: `ecmwf_ifs025_ensemble_mean`, `ncep_gefs025_ensemble_mean`, `ecmwf_aifs025_ensemble_mean`, `ukmo_global_ensemble_mean_20km`. Every listed model is fetched; no feature drives model fetching, the list is config-driven. [EnsembleModelComparison](./model-comparison) passively compares them. |

## Solar

Options for the Global Tilted Irradiance (GTI) calculation under `Njord:Solar`. These values are passed to the Forecast and Ensemble API requests when solar features are enabled.

| Option | Default | Description |
|--------|---------|-------------|
| `Tilt` | `30` | Panel tilt angle in degrees (0 to 90) |
| `Azimuth` | `180` | Panel azimuth in degrees (0 to 360; 180 = south-facing) |

As environment variables: `Njord__Solar__Tilt=30`, `Njord__Solar__Azimuth=180`.

## API call weight

Each request asks only for the hourly variables the enabled features need (the union of their requirements). Open-Meteo counts a request as `ceil(variables / 10)` calls, scaled by the forecast window:

```
forecast weight = ceil(variables / 10) x (Forecast.ForecastDays / 14)
ensemble weight = ceil(variables / 10) x (Ensemble.ForecastDays / 14) x model count
```

The same formula applies to all other domains (Air Quality, Flood, Marine) using their respective forecast days. Seasonal is budgeted as 30 requests/month per location at `max(formula, 1.3)`. See the [budget](./budget) projection.
