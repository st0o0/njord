# Enrichment

Enrichment features compute additional data from the raw domain forecasts using the projection group pipeline. Each enrichment group declares which domains it needs and runs only when those domains are updated.

## Overview

| Feature | Default | Description |
|---------|---------|-------------|
| [Alerts](#alerts) | enabled | Weather alerts derived from ensemble forecast data with confidence from ensemble spread |

::: info Removed features
Consensus, Derived, Trends, and Indices were removed in the v2 redesign. njord now uses the Open-Meteo Ensemble Mean API instead of computing its own multi-model consensus. Derived values (apparent temperature, dew point, VPD, UV index) are available natively from the API. Activity indices and trend analysis may return as optional projection groups in a future release.
:::

## Alerts

Generates weather alerts based on configurable thresholds applied to the ensemble forecast data. Alert confidence is derived from the ensemble standard deviation (low spread = high confidence).

```json
{
  "Enrichment": {
    "Alerts": {
      "Enabled": true,
      "FrostThreshold": 0.0,
      "HeatThresholds": [30, 35, 40],
      "StormGustThreshold": 16.7,
      "HeavyRainHourlyThreshold": 10.0,
      "HeavyRainDailyThreshold": 25.0,
      "PressureDropThreshold": 5.0,
      "CapeThreshold": 1000.0,
      "ThunderstormPrecipThreshold": 5.0,
      "ThunderstormGustThreshold": 15.0
    }
  }
}
```

**Alert types:** Frost, Heat, Storm, HeavyRain, UV, Fog, Snow, PressureDrop, Thunderstorm, Ice, Humidity, Visibility, WindChill, TropicalNight

Each alert is published with:
- `severity`: None, Yellow, Orange, or Red
- `confidence`: High, Medium, or Low (derived from ensemble standard deviation at the alert's peak point)
- Type-specific details (e.g. `peak_value`, `hours_until`, `duration_hours`)

::: tip
Thresholds use metric units: temperatures in Celsius, wind speed in m/s, rain in mm, pressure in hPa, CAPE in J/kg.
:::
