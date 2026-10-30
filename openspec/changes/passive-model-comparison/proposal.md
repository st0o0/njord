# Passive Model Comparison

Replace the single `ModelComparison` feature with two passive features (`ForecastModelComparison`, `EnsembleModelComparison`) that observe whatever models the config already fetches instead of driving multi-model fetches themselves. Remove `EnsembleParams.Models` and make both clients config-driven.

## Motivation

`ModelComparison` is the only feature that sets `EnsembleParams.Models`, a meta-flag that triggers multi-model fetching in `EnsembleClient`. This couples a single off-by-default feature to significant pipeline infrastructure (6 code sites). The feature also mixes Forecast-API and Ensemble-API model data into one `LocationForecast.ModelForecast` dictionary with no way to distinguish the source.

The new design:
- **Config decides what gets fetched.** `Njord:Ensemble:Models` and `Njord:Forecast:Models` control model count. No feature drives fetches.
- **Features observe passively.** Both comparisons are default-on but auto-disable when fewer than 2 models are configured. Zero cost when inactive.
- **Two separate model slots** on `LocationForecast` (`ForecastModels`, `EnsembleModels`) keep the data sources distinct.

## Scope

### njord

- Remove `ModelComparison` feature, `ModelComparisonConfig`, `ModelComparisonInput`
- Remove `EnsembleParams.Models` from the enum and all 7 production-code sites that reference it
- Make `EnsembleClient` config-driven: multi-model when `Models.Length > 1` (like `ForecastClient` already is)
- Split `LocationForecast.ModelForecast` into `ForecastModels` and `EnsembleModels`
- Update `FeatureEngine` to route `DomainDataReceived` by `Domain` + `ModelId` into the correct dictionary
- Update `FeatureEngine` to evaluate features with empty `DomainRequirements` on every domain push
- Update `BudgetGuard.EnsembleWeight` to be config-driven (no flag check)
- Add `ForecastModelComparison` feature (default-on, Enabled requires `ForecastOptions.Models.Length >= 2`)
- Add `EnsembleModelComparison` feature (default-on, Enabled requires `EnsembleOptions.Models.Length >= 2`)
- Update proto: replace `ModelComparison model_comparison = 91` with two new oneof cases; reserve field 91
- Update gRPC mappings for new result types
- Update `EnsembleOptions` default models to include at least 2 ensemble-mean models
- Remove `WithBaseParams` `Pm10` addition (no consumer); keep `WeatherCode`
- Update `docs/data/features.json`, Config Builder, example configs

### ha-njord

- Replace `Feature.MODEL_COMPARISON` with `Feature.FORECAST_MODEL_COMPARISON` and `Feature.ENSEMBLE_MODEL_COMPARISON`
- Update `FEATURE_KEYS` mapping
- Update `lifestyle.py`: two sensors ("Forecast Model Spread", "Ensemble Model Spread") using the same `_model_spread` extraction logic
- Update proto codegen, gRPC client conversion, translations
- Update tests

## Specs touched

- `multi-model-weather` -- `EnsembleParams.Models` flag removal, config-driven multi-model
- `domain-point-types` -- `EnsembleParams` enum change
- `feature-requirements-options` -- no feature drives model fetches
- `ensemble-domain` -- `EnsembleClient` config-driven
- `config-builder` -- new default models, updated budget formula

## Budget impact

Default config today: 1 ensemble model, ModelComparison off.
New default: 2 ensemble models (`ecmwf_ifs025_ensemble_mean`, `gfs025_ensemble_mean`), EnsembleModelComparison on.

Per location, 60 min poll, 4 forecast-days:
- Ensemble old: `ceil(3/10) * (4/14) * 1 = 0.29` -> weight 1
- Ensemble new: `ceil((15+3)/10) * (4/14) * 2 = 2 * 0.29 * 2 = 1.14` -> weight 2
- Delta: +1 call/cycle/location -> +720/month/location

With 5 locations: 3600 extra/month. Well within the 300k budget.
