# Flood Features

Flood features monitor river discharge from the Open-Meteo Flood API, which provides ensemble-based discharge forecasts up to 210 days ahead. These features are valuable for locations near rivers.

## FloodRisk

Warns when forecast river discharge exceeds historical levels. Compares current and forecast discharge against the location's statistical distribution.

| Severity | Condition |
|----------|-----------|
| Yellow | Discharge above twice the historical median |
| Orange | Discharge above the 75th percentile |
| Red | Discharge above the historical maximum |

**Entity:** `binary_sensor.<location>_flood_risk_alert`
**Domains:** Flood | **Default:** Off

```yaml
Njord__Features__FloodRisk__Enabled: "true"
```

**Automation ideas:**
- Alert property managers near rivers
- Activate flood monitoring cameras
- Pre-position sandbags or barriers at orange severity

---

## FloodRain

A cross-domain alert combining heavy rainfall from the Forecast domain with rising river discharge from the Flood forecast. Fires when both conditions are present simultaneously -- heavy rain upstream and discharge already elevated.

| Severity | Condition |
|----------|-----------|
| Orange | 30+ mm rain in 24h while discharge is above median and rising |
| Red | Same conditions but discharge above the 75th percentile |

**Entity:** `binary_sensor.<location>_flood_rain_alert`
**Domains:** Forecast + Flood | **Default:** Off

```yaml
Njord__Features__FloodRain__Enabled: "true"
```

**Automation ideas:**
- Close basement flood gates
- Alert when rain + rising river combine
- Trigger evacuation planning at red severity

---

## FloodTimeline

Provides a projected discharge timeline for the coming days. Not an alert -- just the raw discharge forecast as a sensor, giving you visibility into how river levels are expected to change.

**Entity:** `sensor.<location>_river_discharge` (m3/s)
**Domains:** Flood | **Default:** Off

```yaml
Njord__Features__FloodTimeline__Enabled: "true"
Njord__Flood__ForecastDays: "92"
```

::: tip
The Flood API provides up to 210 days of forecast data. The default is 92 days, which covers a full season. Increase `ForecastDays` if you need longer-term planning.
:::

**Automation ideas:**
- Chart discharge trend on a dashboard
- Alert when discharge is projected to peak above a custom threshold
- Track river level recovery after flood events
