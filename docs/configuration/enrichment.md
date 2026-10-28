# Enrichment

Alerts, scores, conditions, trends and forecasts are all features. Each feature is a rule that declares the domains it reads and is evaluated only when one of them has new data. A feature is switched with `Njord:Features:<Category>:<Name>:Enabled` and carries its own options (thresholds) in the same section; see [Features and domains](./#features-and-domains).

::: tip Detailed feature documentation
For full descriptions, configuration examples, and automation ideas for all 58 features, see the [Features reference](/features/).
:::

| Alert group | Domains | Rules |
|-------------|---------|-------|
| Weather alerts | Forecast (+ Ensemble for 9 alerts) | 20 alerts (below) |
| Air quality alerts | Air Quality (+ Forecast for SmogRisk, PollenBlast) | Pollen, PM2.5, Dust, SmogRisk, PollenBlast |
| Flood alerts | Flood (+ Forecast for FloodRain) | FloodRisk, FloodRain |
| Marine alerts | Marine (+ Forecast for CoastalFlood) | Swimming, CoastalFlood |

Thresholds below apply to the weather alerts; the Pollen and PM2.5 rules use fixed thresholds.

## Weather alerts

Alert rules run on the high-resolution deterministic forecast from the Forecast API. Nine alerts (Frost, Heat, Storm, HeavyRain, Thunderstorm, Snow, Ice, WindChill, TropicalNight) also use ensemble spread data for confidence: low spread across the 51 ensemble members means high confidence. The remaining alerts (PressureDrop, Fog, Visibility, UV, Humidity, RainProbability, WetBulbDanger, ClearSkyUv, Shower, SnowDepthWatch, FireRisk) use a fixed Medium confidence because their variables have low ensemble sensitivity or are deterministic by nature.

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

**Alert types:** Frost, Heat, Storm, HeavyRain, UV, Fog, Snow, PressureDrop, Thunderstorm, Ice, WindChill, Visibility, TropicalNight, Humidity, RainProbability, WetBulbDanger, ClearSkyUv, Shower, SnowDepthWatch, FireRisk.

### New in this version

| Alert | What it does | Default |
|-------|-------------|---------|
| **RainProbability** | Warns when rain probability exceeds 50/70/90% in a rolling 6-hour window. Uses the Forecast API's native precipitation probability. | on |
| **WetBulbDanger** | Warns when wet-bulb temperature reaches dangerous levels (26/28/32 °C). A wet-bulb temperature above 28 °C means the body cannot cool itself through sweating, even in shade. | off |
| **ClearSkyUv** | Warns when the clear-sky UV index is high (6/8/10), meaning dangerous UV levels are possible whenever clouds break. Useful for days with intermittent sun. | off |
| **Shower** | Warns when convective (sudden, intense) showers are expected, separate from frontal steady rain. Thresholds: 3/5/10 mm/h. | off |
| **SnowDepthWatch** | Reports the current snow accumulation status: light (>2 cm), moderate (>10 cm), heavy (>25 cm), extreme (>50 cm). Useful for winter road management. | off |
| **FireRisk** | Wildfire risk index combining high temperature (>30 °C), low humidity (<30%), strong wind, dry soil and a dry spell. Weighted scoring with ensemble confidence. | off |

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
| FloodRain | Flood + Forecast | At least 30 mm forecast rain in 24 h, discharge rising and above the median | Orange (Red when above P75) |

FloodRain is the cross-domain example: it re-evaluates when either the Forecast or the Flood domain is updated.

### Flood ensemble

`Njord:Flood:IncludeEnsemble` (default `false`) requests all 50 GloFAS ensemble members (`ensemble=true`). FloodRisk then reports its confidence as the share of members above the alert threshold: High from 70 %, Low below 30 %, Medium in between. Severity is unchanged, and without the option the confidence stays at its fixed default.

Cost: an ensemble flood request is budgeted with a call weight of 5 (50 members / 10 variables per call) instead of 1, which the startup budget guard and the Config Builder include. A shorter `Njord:Flood:ForecastDays` (default 92, maximum 210) reduces the response size; GloFAS skill also drops with lead time.

## Air quality features

| Feature | Domains | What it does |
|---------|---------|-------------|
| **SmogRisk** | Forecast + Air Quality | Warns when a shallow atmospheric boundary layer (<500 m) traps fine particles. Combines boundary layer height from the weather forecast with PM2.5 from the air quality forecast. |
| **PollenBlast** | Forecast + Air Quality | Predicts pollen explosions: after a rainy period, warm and dry weather with wind causes a sudden burst of pollen release. Uses 8 polls of history to detect the transition. |
| **RagweedPollen** | Air Quality | Ragweed pollen concentration sensor, relevant for North America and Eastern Europe. Follows the same pattern as birch, grass, alder and mugwort pollen. |
| **GranularAqi** | Air Quality | Exposes five per-pollutant European AQI sub-indices (PM2.5, PM10, NO2, ozone, SO2). More specific than the overall AQI. |

## Marine features

| Feature | Domains | What it does |
|---------|---------|-------------|
| **Surf** | Forecast + Marine | Rates surf conditions from swell height, period and wind direction. Swell-only when wind data is absent. |
| **OceanCurrents** | Marine | Reports ocean current speed and direction. Highlights strong currents above the configurable threshold (default 3 km/h). |
| **CoastalFlood** | Marine + Forecast + Ensemble | Storm surge risk: combines sea level deviation above normal with high waves and onshore wind. Three severity levels with ensemble confidence. |

## Lifestyle and gardening

| Feature | Domains | What it does |
|---------|---------|-------------|
| **SoilMoisture** | Forecast | Reports soil moisture at the surface: dry (<0.15 m3/m3), optimal, or wet (>0.35). Helps you decide when to water or when to stay off the lawn. |
| **Evapotranspiration** | Forecast | Shows how much water plants lose per day (mm). Low (<2 mm) means minimal watering, high (>5 mm) means irrigation is needed. |
| **GrowingDegree** | Forecast | Accumulates Growing Degree Days above a base temperature (default 5 °C). A planning tool for agriculture and gardening: track warmth accumulation over the forecast window. |

## Energy features

| Feature | Domains | What it does |
|---------|---------|-------------|
| **SolarYield** | Forecast | Expected solar yield from the tilted panel irradiance forecast. Configure panel tilt and azimuth under `Njord:SolarYield`. |
| **SatelliteRadiation** | Satellite | Observed solar irradiance from satellite, blended with the ensemble forecast for hours ahead. |
| **ElevatedWind** | Forecast | 80 m and 120 m hub-height wind speed and direction for wind energy monitoring. Exposed as HA sensors. |

## Wire exposure

Every feature result is delivered over gRPC (`GetEnrichments`, `StreamEnrichments`) as an `EnrichmentEvent`; its `feature` oneof case identifies the result type.

Values such as apparent temperature, dew point and UV index come directly from the Open-Meteo API; njord computes no further derived values.
