# Design: Passive Model Comparison

## Core principle

No feature drives multi-model fetching. The operator's config (`Njord:Ensemble:Models`, `Njord:Forecast:Models`) decides how many models are fetched. The comparison features passively observe what's there.

## EnsembleParams.Models removal

The `Models` flag in `EnsembleParams` is a meta-signal, not a real API variable. It exists solely to tell `EnsembleClient` "fetch all configured models". With config-driven multi-model, the flag is unnecessary.

Sites to change:
1. **`EnsembleParams` enum** -- remove `Models = 1L << 3`
2. **`EnsembleClient`** -- replace `_needsModels = resolved.AllEnsemble.HasFlag(EnsembleParams.Models)` with `configured.Length > 1`
3. **`BudgetGuard.EnsembleWeight`** -- replace flag check with `config.EnsembleModelCount > 1`
4. **`EnsemblePoint.HasFields`** -- remove `Models => true` case
5. **`EnsembleApiVariableMappingExtensions`** -- remove `Models` filter from `ToApiNames`
6. **`ModelComparison` feature** -- delete entirely (sole consumer of the flag)
7. **`DomainRequirementsTests`** -- update tests that reference the flag

## EnsembleClient: config-driven

```
OLD:
  _needsModels = resolved.AllEnsemble.HasFlag(EnsembleParams.Models)
  _modelIds = _needsModels ? configured : [_primaryModel]
  _weatherVariables = _needsModels ? AllForecast.ToApiNames() : []

NEW:
  _hasMultipleModels = configured.Length > 1
  _modelIds = configured               // always fetch all configured
  _weatherVariables = _hasMultipleModels ? AllForecast.ToApiNames() : []
```

This matches `ForecastClient` which is already config-driven: `_models.Length > 1` gates `FetchSuccess.Models`.

Weather variables for multi-model come from `AllForecast` -- whatever Forecast features require is also fetched per ensemble model. This is correct: the comparison needs the same variables the primary forecast has.

## LocationForecast: split model dictionaries

```
OLD:
  ImmutableDictionary<string, DomainForecast<ForecastPoint>>? ModelForecast

NEW:
  ImmutableDictionary<string, DomainForecast<ForecastPoint>>? ForecastModels
  ImmutableDictionary<string, DomainForecast<ForecastPoint>>? EnsembleModels
```

Both are `DomainForecast<ForecastPoint>` because both APIs return per-model weather data as ForecastPoints. The dictionaries distinguish the source API.

Remove `Models` convenience property and `WithModel`. Add `WithForecastModel` and `WithEnsembleModel`.

## FeatureEngine changes

### Model routing by Domain

```
DomainDataReceived(Domain: Forecast, ModelId: "icon_d2")
  -> LocationForecast.WithForecastModel("icon_d2", data)

DomainDataReceived(Domain: Ensemble, ModelId: "ecmwf_ens_mean")
  -> LocationForecast.WithEnsembleModel("ecmwf_ens_mean", data)
```

The existing Gateway `SelectMany` already sends models first, then primary. No change needed there.

### Features with no requirements

Features with `DomainRequirements` equal to `default` (all fields zero) are evaluated on every domain push. This is a one-line change in FeatureEngine:

```csharp
// Skip features that don't care about this domain
// Features with no requirements (passive) are always evaluated
if (requirements != default && !requirements.Requires(received.Domain))
    continue;
```

The comparison features' `Project` method filters: `EnsembleModels.Count >= 2 ? emit : null`. This is cheap (dictionary count check) and runs at most once per domain push for passive features.

## New features

### ForecastModelComparison

- **Path**: `Njord.Features/Forecasts/ForecastModelComparison/`
- **Input**: `ForecastModelsInput(ImmutableDictionary<string, DomainForecast<ForecastPoint>>)`
- **Requirements**: `DomainRequirements` default (none)
- **Enabled**: `config.Enabled && forecastOptions.Models.Length >= 2`
- **Project**: `forecast.ForecastModels is { Count: >= 2 } models ? new(models) : null`
- **Evaluate**: daily temp min/max and precipitation per model, 5 days
- **Result**: `ForecastModelComparisonResult`
- **Config section**: `Njord:Features:ForecastModelComparison`, Enabled = true

### EnsembleModelComparison

- **Path**: `Njord.Features/Forecasts/EnsembleModelComparison/`
- **Input**: `EnsembleModelsInput(ImmutableDictionary<string, DomainForecast<ForecastPoint>>)`
- **Requirements**: `DomainRequirements` default (none)
- **Enabled**: `config.Enabled && ensembleOptions.Models.Length >= 2`
- **Project**: `forecast.EnsembleModels is { Count: >= 2 } models ? new(models) : null`
- **Evaluate**: daily temp min/max and precipitation per model, 5 days
- **Result**: `EnsembleModelComparisonResult`
- **Config section**: `Njord:Features:EnsembleModelComparison`, Enabled = true

Both share the same evaluation logic (extract from old `ModelComparison.Summarize`). Extract to a shared static helper or keep duplicated (the logic is 10 lines).

### Registration

```csharp
// AddFeatures: requires IOptions<ForecastOptions> and IOptions<EnsembleOptions>
// for the Enabled check. Use AddFeature overload or register manually.
services.AddFeature<ForecastModelComparison, ForecastModelComparisonConfig>();
services.AddFeature<EnsembleModelComparison, EnsembleModelComparisonConfig>();
```

The features inject `IOptions<ForecastOptions>` / `IOptions<EnsembleOptions>` in their constructor to check model count in `Enabled`.

## BudgetGuard

```
OLD:
  var hasModels = requirements.AllEnsemble.HasFlag(EnsembleParams.Models);
  var spreadVarCount = PopCount(AllEnsemble & ~EnsembleParams.Models);
  var ensembleVars = hasModels ? PopCount(AllForecast) + spreadVarCount : spreadVarCount;
  var modelCount = hasModels ? Max(1, config.EnsembleModelCount) : 1;

NEW:
  var spreadVarCount = PopCount(AllEnsemble);
  var modelCount = Max(1, config.EnsembleModelCount);
  var ensembleVars = modelCount > 1
      ? PopCount(AllForecast) + spreadVarCount
      : spreadVarCount;
```

Config-driven. If 3 models configured, budget accounts for 3 regardless of which features are on.

## BaseParams cleanup

Remove `Pm10` from `WithBaseParams` -- no feature consumes it. Keep `WeatherCode` (useful for HA display icons, cheap).

```csharp
public DomainRequirements WithBaseParams() => this with
{
    Forecast = AllForecast != default ? Forecast | ForecastParams.WeatherCode : Forecast,
};
```

## Proto changes (weather.proto)

Reserve old field 91. Add two new oneof cases in `EnrichmentEvent`:

```protobuf
reserved 67, 91;
reserved "type_name", "alerts", "lifestyle", "payload", "model_comparison";

ForecastModelComparison forecast_model_comparison = 93;
EnsembleModelComparison ensemble_model_comparison = 94;
```

Reuse existing `ModelDayValues` and `ModelComparisonDay` messages. Add two wrapper messages:

```protobuf
message ForecastModelComparison {
  ForecastData forecast = 1;
  repeated ModelComparisonDay days = 2;
}

message EnsembleModelComparison {
  ForecastData forecast = 1;
  repeated ModelComparisonDay days = 2;
}
```

Remove old `ModelComparison` message.

## Default model config

`EnsembleOptions` default changes from 1 model to 2:

```csharp
public void ApplyDefaults()
{
    if (Models is { Length: 0 })
    {
        Models = [DefaultModel, SecondaryModel];
    }
}
```

With `DefaultModel = "ecmwf_ifs025_ensemble_mean"` and `SecondaryModel = "gfs025_ensemble_mean"`.

`ForecastOptions.Models` stays empty (= best_match). Users who want forecast model comparison configure explicit models.

## ha-njord changes

### const.py

Replace `MODEL_COMPARISON = "model_comparison"` with:
- `FORECAST_MODEL_COMPARISON = "forecast_model_comparison"`
- `ENSEMBLE_MODEL_COMPARISON = "ensemble_model_comparison"`

Update `FEATURE_KEYS`:
- `Feature.FORECAST_MODEL_COMPARISON: "Forecast:ForecastModelComparison"`
- `Feature.ENSEMBLE_MODEL_COMPARISON: "Forecast:EnsembleModelComparison"`

### grpc_client.py

Update enrichment event conversion for the two new oneof cases.

### lifestyle.py

Replace single sensor with two sensors using the same `_model_spread` value function:

```python
LifestyleSensorDescription(
    feature=Feature.FORECAST_MODEL_COMPARISON,
    key="forecast_model_comparison",
    name="Forecast Model Spread",
    icon="mdi:chart-bell-curve",
    value_fn=_model_spread,
    ...
),
LifestyleSensorDescription(
    feature=Feature.ENSEMBLE_MODEL_COMPARISON,
    key="ensemble_model_comparison",
    name="Ensemble Model Spread",
    icon="mdi:chart-bell-curve",
    value_fn=_model_spread,
    ...
),
```

### translations

Add entries for both new sensors in `en.json`, `de.json`, `strings.json`. Remove old `model_comparison`.

### tests

Update `test_lifestyle.py`, `test_grpc_client.py`, `test_proto_import.py` for the two new features.
