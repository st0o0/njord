## Context

The weather entity pipeline currently has two gaps:

1. **Per-model weather entities**: `EntityRegistry` creates manifest entries for per-model weather entities (from `ActiveModels.EnsembleModels` and `ForecastModels.Skip(1)`), but `FeatureEngine.Process` returns early at line 51 after storing model data — feature rules never run against it. `EntityRegistry.Find` only maps `(typeof(WeatherResult), locationName)` to the primary entity ID; per-model entity IDs are added to the `All` list but never to `_ids`.

2. **Missing current-weather fields**: `WeatherResult` carries temperature, humidity, pressure, wind speed/direction, condition — but not cloud_cover, dew_point, visibility, precipitation, or wind_gusts. The data is available from Open-Meteo and already partially present on `ForecastPoint` (CloudCover exists); others need new `ForecastParams` flags.

## Goals / Non-Goals

**Goals:**
- Per-model weather entities show live data once their domain is polled
- Primary weather entity exposes all standard HA weather attributes
- Zero additional API calls — all data is already fetched

**Non-Goals:**
- Changing how EnsembleModelComparison/ForecastModelComparison work (they use stored models on the next normal event)
- Running non-weather rules against model data (alerts, scores, etc.)
- Per-model hourly forecasts with different lengths or parameters

## Decisions

### D1: Run WeatherRule on model data inside FeatureEngine's model branch

**Choice:** After storing model data (lines 35-48), build a temporary single-entry `RingBuffer<LocationForecast>` containing just that model's forecast as the `Forecast` property, then call `WeatherRule.ExecuteAll` on it. Tag each resulting event with the ModelId.

**Alternative considered:** Override `EvaluateAll` in WeatherRule to iterate `LocationForecast.EnsembleModels` / `ForecastModels` and emit multiple results. Rejected because: (a) the rule shouldn't know about models — it evaluates one forecast, (b) it would only run when primary Forecast data arrives, not when model data updates, (c) it mixes concerns.

**Alternative considered:** Remove the early return entirely and run all rules on model data. Rejected because: alerts, scores, conditions running on per-model data would produce duplicate/conflicting results.

**Implementation:** FeatureEngine gains a `_weatherRule` reference (the single `OutputKind.Weather` rule, resolved from `_enabled`). After storing model data, if `_weatherRule` is non-null:
1. Create a `LocationForecast` with just `Forecast = modelWeather` (for Ensemble models) or the model's forecast data
2. Create a 1-deep `RingBuffer`, push the forecast
3. Call `_weatherRule.ExecuteAll(ring, now)`
4. Wrap each result in a `ModelWeatherResult` or tag the `WeatherResult` with ModelId
5. Add to `events`

### D2: WeatherResult gains ModelId as a nullable string property

**Choice:** Add `string? ModelId = null` as a defaulted parameter on `WeatherResult`. The primary result keeps `null`; per-model results carry the model ID string.

**Alternative considered:** A separate `ModelWeatherResult` type. Rejected because: the proto mapping is identical, it would need a new branch in every consumer, and `EntityRegistry.Find` already handles Weather specially.

### D3: EntityRegistry.Find uses ModelId for per-model routing

**Choice:** Extend `Find` to check `WeatherResult.ModelId`. The `_ids` dictionary gains entries for per-model weather entities keyed by `(typeof(WeatherResult), $"{locationName}:{modelId}")` — a compound key that distinguishes models. `Find` checks `ModelId` on WeatherResult and adjusts the lookup key.

**Alternative considered:** A separate `_modelWeatherIds` dictionary keyed by `(modelId, locationName)`. Simpler but adds a third dictionary with its own maintenance. The compound key reuses existing infrastructure.

### D4: Add 5 nullable fields to WeatherResult and WeatherValue proto

**Choice:** Add `CloudCover`, `DewPoint`, `Visibility`, `Precipitation`, `WindGusts` as `double?` to `WeatherResult`. Add matching `optional double` fields to proto `WeatherValue`. All nullable — missing data produces no HA attribute rather than a zero.

**ForecastParams additions:** `DewPoint`, `Visibility`, `WindGusts` need new flags (CloudCover and Precipitation already exist on `ForecastPoint`). `WeatherRule.Descriptor.Requirements.Forecast` gains these flags.

### D5: ha-njord exposes all attributes via standard HA properties

**Choice:** Add `native_wind_bearing`, `native_dew_point`, `cloud_coverage`, `native_visibility`, and wind gust display to `NjordWeatherEntity`. These are standard HA `WeatherEntity` properties — no custom attributes needed.

## Risks / Trade-offs

- **[FeatureEngine complexity]** The model-data branch gains weather-specific logic, coupling FeatureEngine to OutputKind.Weather. Mitigated by keeping it to a 5-line block gated on `_weatherRule != null`.
- **[Proto field bloat]** 5 new optional fields on WeatherValue. Acceptable — these are standard weather attributes, not niche.
- **[ForecastParams flag space]** Adding 3 flags. ForecastParams is a `[Flags]` enum with room.

## Open Questions

None — the approach is straightforward and all data sources are confirmed available.
