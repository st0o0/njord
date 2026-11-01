# Model comparison

Two passive features compare models side by side: `ForecastModelComparison` (models of the Forecast API) and `EnsembleModelComparison` (ensemble-mean models). Both are on by default.

They request no data of their own. No feature drives multi-model fetching: you choose the models in `Njord:Forecast:Models` and `Njord:Ensemble:Models`, and the comparison features observe whatever is fetched.

```json
{
  "Njord": {
    "Forecast": {
      "Models": ["icon_seamless", "ecmwf_ifs025"]
    },
    "Ensemble": {
      "Models": ["ecmwf_ifs025_ensemble_mean", "ncep_gefs025_ensemble_mean"]
    },
    "Features": {
      "ForecastModelComparison": { "Enabled": true },
      "EnsembleModelComparison": { "Enabled": true }
    }
  }
}
```

As environment variables: `Njord__Ensemble__Models__0=ecmwf_ifs025_ensemble_mean`, `Njord__Ensemble__Models__1=ncep_gefs025_ensemble_mean`, `Njord__Features__EnsembleModelComparison__Enabled=true`, `Njord__Features__ForecastModelComparison__Enabled=true`.

## Defaults

- `Njord:Ensemble:Models` defaults to `ecmwf_ifs025_ensemble_mean` and `ncep_gefs025_ensemble_mean`, so `EnsembleModelComparison` works out of the box.
- `Njord:Forecast:Models` is empty by default (best_match). `ForecastModelComparison` has nothing to compare until you list at least two forecast models.

## Ensemble models

| Id | Provider | Region | Resolution | Forecast |
|----|----------|--------|-----------:|---------:|
| `ecmwf_ifs025_ensemble_mean` | ECMWF | Global | 25 km | 15 days |
| `ncep_gefs025_ensemble_mean` | NOAA | Global | 25 km | 16 days |
| `ecmwf_aifs025_ensemble_mean` | ECMWF | Global | 25 km | 15 days |
| `ukmo_global_ensemble_mean_20km` | UK Met Office | Global | 20 km | 7 days |

The first entry of `Models` is the primary model. All feature rules (alerts, scores, trends) evaluate the primary model only; the other models feed the comparison features and the gRPC data stream.

## Behaviour

- Every configured model is requested in one call per domain, so the API call weight is multiplied by the model count. The startup budget gate and the Config Builder use the multiplied figure.
- The comparison features add no calls themselves. Reduce `Models` to one entry to lower the cost; the comparison then reports nothing.
- Each feature reports the per-day temperature and precipitation of every model for the next days.
- An unknown id, a duplicate or an empty ensemble list fails validation at startup.
