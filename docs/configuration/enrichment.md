# Enrichment

Alerts are the only enrichment. They run as projection groups: each group declares the domains it reads and is evaluated only when one of them has new data.

| Alert group | Domains | Rules |
|-------------|---------|-------|
| Weather alerts | Weather | 14 rules (below) |
| Air quality alerts | Air Quality | Pollen, PM2.5 |
| Flood risk | Flood | FloodRisk |
| Flood rain (cross-domain) | Flood + Weather | FloodRain |

All groups are switched together with `Enrichment:Alerts:Enabled` (default `true`). Thresholds below apply to the weather alerts; air quality and flood rules use fixed thresholds.

## Weather alerts

Rules run on the Ensemble Mean forecast. Confidence comes from the ensemble standard deviation at the alert's peak point: low spread means high confidence.

```json
{
  "Njord": {
    "Enrichment": {
      "Alerts": {
        "Enabled": true,
        "FrostThresholds": [0, -5, -15],
        "HeatThresholds": [30, 35, 40],
        "StormGustThresholds": [17, 25, 33],
        "HeavyRainHourlyThreshold": 10.0,
        "HeavyRainDailyThreshold": 25.0,
        "PressureDropThreshold": 5.0,
        "PressureDropSevereThreshold": 10.0,
        "FogPersistentHours": 4,
        "CapeThreshold": 1000.0,
        "ThunderstormPrecipThreshold": 5.0,
        "ThunderstormGustThreshold": 15.0,
        "IceThreshold": 2.0,
        "WindChillThresholds": [-10, -20, -30],
        "VisibilityThresholds": [1000, 200, 50],
        "TropicalNightThresholds": [20, 23, 25],
        "HumidityThresholds": [16, 21, 24]
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

## Wire exposure

Weather alerts are delivered over gRPC (`GetEnrichments`, `StreamEnrichments`) and MQTT (`njord/{location}/alerts/{type}`). Air quality and flood alerts are produced by the pipeline as their own events; their gRPC and MQTT representation is not exposed yet.

Values such as apparent temperature, dew point and UV index come directly from the Open-Meteo API; njord computes no further derived values.
