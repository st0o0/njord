## Context

Adding Flood as a new domain to the multi-domain proxy. Uses a separate Open-Meteo endpoint at `flood-api.open-meteo.com` with **daily** resolution (not hourly). GloFAS model provides river discharge forecasts up to 210 days ahead with percentile bands.

## Goals / Non-Goals

**Goals:** Flood fetch with daily resolution, flood risk alert based on percentiles, optional cross-domain flood-rain correlation.
**Non-Goals:** Historical flood analysis, custom river selection.

## Decisions

### D1: Daily resolution handled by DomainForecast

`DomainForecast<FloodPoint>` with `TimeResolution.Daily`. ForecastQueries handles the different resolution transparently.

### D2: FloodPoint updated for API response

The API provides: river_discharge, river_discharge_mean, river_discharge_median, river_discharge_max, river_discharge_min, river_discharge_p25, river_discharge_p75. FloodPoint is updated to match with nullable fields for all statistical variants.

### D3: Flood risk from percentile comparison

FloodRiskAlert compares current discharge forecast against historical percentiles: above p75 → Warning, above historical max → Critical. Severity derived from how far above normal.

### D4: Cross-domain FloodRain alert (optional)

FloodRainProjector requires both Flood and Weather domains. Correlates heavy precipitation from Weather with rising discharge from Flood for a combined risk assessment.
