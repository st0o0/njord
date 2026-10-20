## Why

Climate anomaly tells users "today is X°C above the 30-year average" — backward-looking context. Seasonal outlook adds the forward-looking piece: "next month is expected 2°C warmer and 15% drier than normal." Open-Meteo provides a free Seasonal Forecast API (7-month outlook, 51 ensemble members, 6-hourly) that no other HA integration exposes. Combined with the existing climate baseline, njord can deliver a unique long-range weather intelligence view.

## What Changes

- New `ISeasonalClient` + `SeasonalClient` in Njord.OpenMeteo — typed client for `seasonal-api.open-meteo.com/v1/seasonal`, using `OpenMeteoRequestBuilder`.
- New `SeasonalResponse` / `SeasonalSixHourlyData` typed response models, registered in `OpenMeteoJsonContext`.
- New `SeasonalOutlookService` (hosted service) — fetches once daily per location, aggregates 6-hourly data to monthly means, compares with the climate baseline from `IClimateBaselineStore`, caches the result.
- New `ISeasonalOutlookStore` + `SeasonalOutlookStore` in Njord.Core (same pattern as `IClimateBaselineStore`).
- New `SeasonalOutlookHandler` (IDomainHandler) in Njord.Weather — triggers on Weather (piggybacks on weather updates), reads cached outlook, emits `SeasonalOutlookComputed`.
- New `SeasonalOutlookComputed` PipelineEvent with `ImmutableArray<MonthlyOutlook>`.
- New `MonthlyOutlook` record: Month (string, e.g. "2026-11"), TempAnomaly (double, °C vs baseline), PrecipAnomaly (double, % vs baseline), Confidence (string: High/Medium/Low from ensemble spread).
- `EnrichmentOptions.SeasonalOutlook` toggle (default false).
- gRPC + MQTT wire support via the existing `LifestyleUpdate` mechanism (type = "seasonal_outlook", data = serialized monthly outlook).
- ha-njord: seasonal outlook sensor entity.

## Non-goals

- Sub-monthly resolution (weekly outlook) — the ensemble spread at that granularity is too wide to be actionable.
- Extreme Forecast Index (EFI) interpretation — complex, low user value for HA.
- Seasonal data as a "fast domain" in the PollWorker — it updates weekly, not hourly.

## Capabilities

### New Capabilities

- `seasonal-outlook`: ISeasonalClient, SeasonalOutlookService, SeasonalOutlookHandler, monthly anomaly comparison.

### Modified Capabilities

- `enrichment-config`: SeasonalOutlook toggle added.

## Impact

- **Njord.OpenMeteo**: New client, response model, hosted service.
- **Njord.Core**: ISeasonalOutlookStore (same pattern as IClimateBaselineStore).
- **Njord.Weather**: SeasonalOutlookHandler.
- **Njord.Messages**: SeasonalOutlookComputed event, MonthlyOutlook record.
- **API budget**: +1 call/day/location (NOT per poll cycle — slow domain). ~30 calls/month for 1 location.
