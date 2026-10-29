# Model comparison

`Forecast:ModelComparison` fetches several ensemble-mean weather models in one request and compares them. It is off by default.

```json
{
  "Njord": {
    "Weather": {
      "Models": ["ecmwf_ifs025_ensemble_mean", "ecmwf_aifs025_ensemble_mean"]
    },
    "Features": {
      "Weather": { "ModelComparison": { "Enabled": true } }
    }
  }
}
```

As environment variables: `Njord__Ensemble__Models__0=ecmwf_ifs025_ensemble_mean`, `Njord__Ensemble__Models__1=ecmwf_aifs025_ensemble_mean`, `Njord__Features__ModelComparison__Enabled=true`.

## Models

| Id | Provider | Region | Resolution | Forecast |
|----|----------|--------|-----------:|---------:|
| `ecmwf_ifs025_ensemble_mean` | ECMWF | Global | 25 km | 15 days |
| `ecmwf_aifs025_ensemble_mean` | ECMWF | Global | 25 km | 15 days |
| `ukmo_global_ensemble_mean_20km` | UK Met Office | Global | 20 km | 7 days |

The first entry of `Models` is the primary model. All feature rules (alerts, scores, trends) evaluate the primary model only; the other models feed `ModelComparison` and the gRPC data stream.

## Behaviour

- With the feature off, only the primary model is requested. A startup warning names the ignored models.
- With the feature on, all models are requested in one call; the weather API weight is multiplied by the model count (31 variables and 3 models give 12 instead of 4). The startup budget gate uses the multiplied figure.
- `ModelComparison` reports the per-day temperature and precipitation of every model for the next days.
- An unknown id, a duplicate or an empty list fails validation at startup.
