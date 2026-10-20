## Context

The Climate API returns 1950-2050 daily data per location. Unlike Weather/AQ/Flood, this data is quasi-static — the historical baseline doesn't change, and the projection changes only with new CMIP6 runs (yearly). Fetching it every poll cycle would waste budget and bandwidth.

## Goals / Non-Goals

**Goals:** Climate baseline caching, anomaly computation, slow-domain concept.
**Non-Goals:** Climate projections, seasonal trends, per-model climate comparison.

## Decisions

### D1: Fetch-once-at-startup, refresh daily

`ClimateBaselineService` is a `BackgroundService` that:
1. At startup: fetches 30 years (1991-2020) of daily data per location from the Climate API.
2. Aggregates to monthly averages: for each month, compute mean temperature, total precipitation, mean sunshine.
3. Caches as `IReadOnlyDictionary<string, MonthlyBaseline[]>` (keyed by location name, 12 months).
4. Refreshes once per day (the baseline itself doesn't change, but the 2020s data accumulates).

### D2: ClimateAnomalyHandler triggers on Weather, uses cached baseline

The handler triggers on `DomainKind.Weather` (not a new domain). When weather data arrives, it reads the cached baseline for the current month and location, computes the anomaly, and emits `ClimateAnomalyComputed`.

This means no new "slow domain" infrastructure is needed — the baseline is just a cached dependency injected into the handler, like AlertOptions.

### D3: Climate API request parameters

Request: `climate-api.open-meteo.com/v1/climate?latitude=X&longitude=Y&start_date=1991-01-01&end_date=2020-12-31&daily=temperature_2m_mean,temperature_2m_max,temperature_2m_min,precipitation_sum,sunshine_duration&models=EC_Earth3P_HR`

One model is sufficient for a 30-year baseline. EC_Earth3P_HR has 25km resolution.

### D4: Anomaly computation

For the current month:
- `TempAnomaly = todayForecastTemp - baselineMonthlyMeanTemp`
- `PrecipAnomaly = (todayForecastPrecip - baselineMonthlyMeanDailyPrecip) / baselineMonthlyMeanDailyPrecip × 100` (percentage)
- Report as: "+3.2°C above July average" or "-30% precipitation vs July average"

### D5: Typed response model

`ClimateResponse` with `ClimateDailyData` (time[], temperature_2m_mean[], precipitation_sum[], sunshine_duration[]). Source-gen JSON, same pattern as other clients.

## Risks / Trade-offs

- **[Large initial response]** → ~100KB per location for 30 years of daily data. One-time cost at startup.
- **[Model choice affects baseline]** → Different CMIP6 models give slightly different historical reconstructions. EC_Earth3P_HR is a reasonable default.
