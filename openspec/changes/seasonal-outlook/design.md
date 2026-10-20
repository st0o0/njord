## Context

njord already has the "slow domain" pattern from the Climate Anomaly feature: `ClimateBaselineService` fetches once daily, stores in `IClimateBaselineStore`, and the `ClimateAnomalyHandler` reads it on Weather updates. Seasonal Outlook follows the same pattern but fetches from the Seasonal Forecast API and produces a forward-looking 3-month anomaly comparison.

## Goals / Non-Goals

**Goals:** 3-month outlook comparing seasonal ensemble forecast with 30-year climate baseline. Daily fetch, enrichment-driven toggle, gRPC + MQTT wire.

**Non-Goals:** Weekly resolution, EFI interpretation, seasonal data as a fast-poll domain.

## Decisions

### D1: Same architecture as Climate Anomaly

`SeasonalOutlookService` (hosted service, daily fetch) → `ISeasonalOutlookStore` (cache in Core) → `SeasonalOutlookHandler` (IDomainHandler, triggers on Weather). This mirrors `ClimateBaselineService` → `IClimateBaselineStore` → `ClimateAnomalyHandler` exactly. One proven pattern, two instances.

### D2: 6-hourly → monthly aggregation

The Seasonal API returns 6-hourly data for 7 months. Aggregation: group by year-month, compute mean temperature and total precipitation per month. This is the same aggregation `ClimateResponseMappings` does for the 30-year baseline, so the code shape is identical.

### D3: 3-month window, not 7

The ensemble spread widens significantly past 3 months — confidence drops to "Low" for most variables. Showing 7 months of "we don't know" is worse than showing 3 months of actionable outlook. The handler emits the next 3 calendar months only.

### D4: Confidence from temperature spread

The Seasonal API returns ensemble members (51 for ECMWF). The client requests the ensemble mean model (`ecmwf_seasonal_mean`). The spread (std dev) across the 6-hourly values within each month serves as the confidence proxy. This is the same approach as Climate Anomaly uses `EnsembleStdDev` for alert confidence.

### D5: Wire via LifestyleUpdate

Rather than adding another proto message type, the seasonal outlook flows through the existing `LifestyleUpdate` oneof with `type = "seasonal_outlook"`. The `data` map carries `month_1`, `month_1_temp_anomaly`, `month_1_precip_anomaly`, `month_1_confidence`, etc. This is consistent with how all lifestyle events are wired today.

## Risks / Trade-offs

- **[Seasonal API data quality]** — At 7 months, skill is very low. We mitigate by showing only 3 months and confidence levels.
- **[Baseline dependency]** — Requires Climate Anomaly's baseline store. If climate fetch fails, seasonal outlook has nothing to compare against. The handler returns empty in this case.
- **[API budget]** — +1 call/day/location. Negligible vs the hourly poll cycle cost.
