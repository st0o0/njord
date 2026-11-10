## Why

The weather entity in HA is incomplete in two ways: (1) per-model weather entities are registered in the manifest but never populated — the FeatureEngine stores model data in LocationForecast then returns early before running WeatherRule, and WeatherRule only produces a single result from primary Forecast data; (2) the primary weather entity is missing current-weather attributes that Open-Meteo provides and HA supports (cloud_cover, dew_point, visibility, precipitation, wind_gusts), plus wind_direction is present end-to-end but not exposed in ha-njord.

## What Changes

- **WeatherRule produces per-model results**: add an `EvaluateAll`-style mechanism so WeatherRule emits one `WeatherResult` per model in addition to the primary, with a `ModelId` field to identify the source
- **FeatureEngine runs WeatherRule on model data**: instead of returning early after storing model data, run weather-capable rules against it
- **EntityRegistry routes model WeatherResults**: `Find` maps `WeatherResult` with `ModelId` to the correct per-model entity ID (e.g. `barcelona_ecmwf_ifs025_ensemble_mean_weather`)
- **WeatherResult gains current-weather fields**: `CloudCover`, `DewPoint`, `Visibility`, `Precipitation`, `WindGusts` added to `WeatherResult`
- **Proto WeatherValue extended**: matching fields added to the gRPC contract
- **ForecastParams extended**: flags for `CloudCover`, `DewPoint`, `Visibility`, `WindGusts` added; `WeatherRule.Requirements` updated
- **ha-njord weather.py maps all attributes**: `native_wind_bearing`, `native_dew_point`, `cloud_coverage`, `native_visibility`, `native_precipitation_unit`/value, wind gusts

## Non-goals

- Changing how `EnsembleModelComparison` or `ForecastModelComparison` features work — they consume stored model data on the next normal domain event, which is a different pattern
- Adding new weather condition codes or changing the condition mapping
- Changing poll intervals or budget behavior — no new API calls are added (the model data is already fetched)

## Capabilities

### New Capabilities
- `per-model-weather-entities`: FeatureEngine + WeatherRule + EntityRegistry changes to populate per-model weather entities from already-fetched model data

### Modified Capabilities
- `weather-feature`: WeatherResult gains current-weather fields (cloud_cover, dew_point, visibility, precipitation, wind_gusts); WeatherRule requirements extended
- `entity-registry`: Find routes WeatherResult with ModelId to per-model entity IDs

## Impact

- **njord**: `WeatherResult` (Messages), `WeatherRule` (Features), `FeatureEngine` (Pipeline), `EntityRegistry` (Grpc), proto `WeatherValue`, `ForecastParams` (Core)
- **ha-njord**: `weather.py` attribute mapping
- **API budget**: zero impact — ensemble model data is already fetched, this change only processes it through existing rules
- **Proto**: additive fields on `WeatherValue` (non-breaking)
- **Persistence**: no impact (WeatherResult is not persisted)
