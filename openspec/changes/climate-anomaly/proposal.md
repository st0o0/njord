## Why

No other Home Assistant addon tells you "today is 3.2°C above the 30-year July average for your location." The Open-Meteo Climate API (CMIP6 HighResMip, 10km, 1950-2050 daily) provides the baseline data for free. This is a unique differentiator that turns raw forecasts into climate-contextual intelligence.

This also introduces the "slow domain" concept: domains that don't poll every cycle but fetch on a longer schedule (daily or at startup).

## What Changes

- New `IClimateClient` + `ClimateClient` in Njord.OpenMeteo targeting `climate-api.open-meteo.com/v1/climate`.
- New `ClimateResponse` typed response model (daily: temperature min/mean/max, precipitation, wind, radiation).
- New `ClimateBaseline` record: 30-year average per month for key parameters (temp, precip, sunshine).
- New `ClimateBaselineService` (HostedService or startup resolver): fetches 30 years of daily data for each location, aggregates to monthly averages, caches as `IReadOnlyDictionary<Location, MonthlyBaseline>`.
- New `ClimateAnomalyHandler` (IDomainHandler): compares today's forecast vs the baseline for the current month. Triggers on Weather (not a new domain — it uses Weather forecast + cached baseline).
- New `ClimateAnomalyComputed` PipelineEvent: `TempAnomaly`, `PrecipAnomaly`, `BaselinePeriod`.
- `EnrichmentOptions.ClimateAnomaly` toggle (default false).
- API budget: 1 call per location at startup (or once/day). The Climate API returns a large response (~100KB per location for 30 years) but is called rarely.

## Non-goals

- Climate change projections (2050+ scenarios).
- Seasonal forecast (separate Seasonal API, separate change).
- Real-time climate monitoring.

## Capabilities

### New Capabilities

- `climate-baseline`: IClimateClient, baseline computation, caching.
- `climate-anomaly-enrichment`: ClimateAnomalyHandler comparing forecast vs 30-year baseline.
- `slow-domain`: Infrastructure for domains that fetch on startup/daily schedule instead of every poll cycle.

### Modified Capabilities

- `enrichment-config`: EnrichmentOptions gains ClimateAnomaly toggle.

## Impact

- **Njord.OpenMeteo**: New typed client + response model.
- **Njord.Weather** (or new project): ClimateBaselineService, ClimateAnomalyHandler.
- **Njord.Core**: Slow-domain concept (optional; could be just a HostedService).
- **Njord.Messages**: ClimateAnomalyComputed PipelineEvent.
- **API budget**: +1 call per location per day (negligible).
