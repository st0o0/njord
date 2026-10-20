# Enrichment

Alerts, scores, conditions, trends and forecasts are all features. Each feature is a rule that declares the domains it reads and is evaluated only when one of them has new data. A feature is switched with `Njord:Features:<Category>:<Name>:Enabled` and carries its own options (thresholds) in the same section; see [Features and domains](./#features-and-domains).

| Alert group | Domains | Rules |
|-------------|---------|-------|
| Weather alerts | Weather | 14 rules (below) |
| Air quality alerts | Air Quality | Pollen, PM2.5, Dust |
| Flood risk | Flood | FloodRisk |
| Flood rain (cross-domain) | Flood + Weather | FloodRain |

Thresholds below apply to the weather alerts; the Pollen and PM2.5 rules use fixed thresholds.

## Weather alerts

Rules run on the Ensemble Mean forecast. Confidence comes from the ensemble standard deviation at the alert's peak point: low spread means high confidence.

```json
{
  "Njord": {
    "Features": {
      "Weather": {
        "Frost": { "Enabled": true, "Thresholds": [0, -5, -15] },
        "Heat": { "Thresholds": [30, 35, 40] },
        "Storm": { "GustThresholds": [17, 25, 33] },
        "HeavyRain": { "HourlyThreshold": 10.0, "DailyThreshold": 25.0 },
        "PressureDrop": { "Threshold": 5.0, "SevereThreshold": 10.0 },
        "Fog": { "PersistentHours": 4 },
        "Thunderstorm": { "CapeThreshold": 1000.0, "PrecipThreshold": 5.0, "GustThreshold": 15.0 },
        "Ice": { "Threshold": 2.0 },
        "WindChill": { "Thresholds": [-10, -20, -30] },
        "Visibility": { "Thresholds": [1000, 200, 50] },
        "TropicalNight": { "Thresholds": [20, 23, 25] },
        "Humidity": { "Thresholds": [16, 21, 24] }
      },
      "AirQuality": {
        "Dust": { "Thresholds": [50, 150, 500] }
      }
    }
  }
}
```

The values shown are the defaults. Array thresholds are the yellow, orange and red levels.

**Alert types:** Frost, Heat, Storm, HeavyRain, UV, Fog, Snow, PressureDrop, Thunderstorm, Ice, WindChill, Visibility, TropicalNight, Humidity.

Each alert carries:

- `severity`: none, yellow, orange or red
- `confidence`: derived from the ensemble standard deviation
- `trigger_value` and `threshold`
- `peak_value`, `hours_until`, `duration_hours` when applicable

::: tip
Thresholds use metric units: Celsius, m/s for wind, mm for rain, hPa for pressure, J/kg for CAPE, m for visibility.
:::

## Air quality alerts

Evaluated on the next 24 hours of the Air Quality forecast.

| Alert | Metric | Yellow | Orange | Red |
|-------|--------|--------|--------|-----|
| Pollen | Highest of birch, grass, alder, mugwort | 20 | 50 | 100 |
| PM2.5 | Peak PM2.5 (ug/m3) | 15 (WHO guideline) | 25 | 50 |

## Flood alerts

Evaluated on the Flood forecast for the next seven days.

| Alert | Domains | Condition | Severity |
|-------|---------|-----------|----------|
| FloodRisk | Flood | Discharge above twice the historical median | Yellow |
| | | Discharge above P75 | Orange |
| | | Discharge above the historical maximum | Red |
| FloodRain | Flood + Weather | At least 30 mm forecast rain in 24 h, discharge rising and above the median | Orange (Red when above P75) |

FloodRain is the cross-domain example: it re-evaluates when either the Weather or the Flood domain is updated.

### Flood ensemble

`Njord:Flood:IncludeEnsemble` (default `false`) requests all 50 GloFAS ensemble members (`ensemble=true`). FloodRisk then reports its confidence as the share of members above the alert threshold: High from 70 %, Low below 30 %, Medium in between. Severity is unchanged, and without the option the confidence stays at its fixed default.

Cost: an ensemble flood request is budgeted with a call weight of 5 (50 members / 10 variables per call) instead of 1, which the startup budget guard and the Config Builder include. A shorter `Njord:Flood:ForecastDays` (default 92, maximum 210) reduces the response size; GloFAS skill also drops with lead time.

## Wire exposure

Every feature result is delivered over gRPC (`GetEnrichments`, `StreamEnrichments`) as an `EnrichmentEvent`; its `feature` oneof case identifies the result type.

Values such as apparent temperature, dew point and UV index come directly from the Open-Meteo API; njord computes no further derived values.

## Surf conditions

`Marine:Surf` (off by default) separates swell from wind waves. Swell is distant, organised, long-period energy; wind waves are local chop, so a 1.5 m sea of 5 s wind waves and a 1.5 m swell of 12 s are very different days. The enrichment scores the best hour of the next 24 hours from swell period (below 8 s poor, 8 to 12 s good, above 12 s excellent), swell height (1.0 to 2.5 m is the ideal band) and, when Weather wind direction is available, wind relative to swell direction (offshore, cross, onshore). A high wind-wave share lowers the score and a swell period below 8 s caps the rating at poor. Without wind data the result is swell-only (`wind_considered` is `false`). The score is indicative, the thresholds are constants and not options.

The result is a condition result delivered as an `EnrichmentEvent`. The Marine API call count is unchanged (one call per location and cycle); the extra Marine variables are only requested when the toggle is on.
