# Features & Budget

njord has **89 features** organized in 7 output kinds. Each feature is a toggle you enable or disable.

## Output kinds

| Kind | Count | What it produces |
|------|-------|-----------------|
| **Alert** | 30 | Binary sensor that turns on when a threshold is exceeded (frost, heat, storm, etc.) |
| **Score** | 7 | Numeric score 0-100 (BBQ weather, laundry drying, outdoor enjoyment, etc.) |
| **Condition** | 23 | Text state with attributes (commute safety, soil moisture, surf conditions, etc.) |
| **Trend** | 1 | Direction indicator (forecast confidence trend) |
| **Forecast** | 4 | Multi-model comparison and seasonal outlook |
| **Weather** | 1 | Standard Home Assistant weather entity with hourly forecast |
| **Observation** | 23 | Raw sensor values (dew point, UV index, wave height, PM2.5, etc.) |

## Defaults

21 features are on by default:

- 15 weather alerts (Frost, Heat, Storm, HeavyRain, Uv, Fog, Snow, PressureDrop, Thunderstorm, Ice, WindChill, Visibility, TropicalNight, Humidity, RainProbability)
- Commute safety
- Pollen and PM2.5 alerts
- Forecast and Ensemble model comparison
- Weather entity

These need the **Forecast**, **Ensemble** and **Air Quality** domains from Open-Meteo.

## Domains

Each feature declares which Open-Meteo domains it needs. njord fetches only the domains that at least one enabled feature requires:

| Domain | API | Examples |
|--------|-----|----------|
| **Forecast** | `api.open-meteo.com` | All weather alerts, scores, weather entity |
| **Ensemble** | `ensemble-api.open-meteo.com` | Alert confidence, model comparison |
| **Air Quality** | `air-quality-api.open-meteo.com` | Pollen, PM2.5, ozone, AQI |
| **Flood** | `flood-api.open-meteo.com` | Flood risk, discharge monitoring |
| **Marine** | `marine-api.open-meteo.com` | Surf conditions, swimming safety, waves |
| **Seasonal** | `seasonal-api.open-meteo.com` | Temperature and precipitation outlook |

Enabling Flood, Marine or Seasonal features adds those domains to each poll cycle and increases API usage.

## Budget

Open-Meteo's free tier allows **300,000 requests/month** (non-commercial). njord tracks usage and stays well within this:

- **Default setup** (1 location, 21 features, 3 domains): ~3,600 requests/month (~1.2%)
- Each additional **location** multiplies the cost
- Each additional **domain** adds API calls per cycle
- The poll interval defaults to **60 minutes** (24 polls/day)

njord has a budget guard that warns at startup if projected usage exceeds 80% of the monthly limit.

::: tip
Use the [Config Builder](/builder) to see the exact budget impact of any feature combination before deploying.
:::

## Enabling features

Toggle a feature with an environment variable:

```bash
Njord__Features__FloodRisk__Enabled=true
Njord__Features__SolarYield__Enabled=true
```

Or in `appsettings.json`:

```json
{
  "Njord": {
    "Features": {
      "FloodRisk": { "Enabled": true },
      "SolarYield": { "Enabled": true }
    }
  }
}
```

Many features have configurable thresholds. See [Feature categories](/features/) and [Configuration](/configuration/) for details.

## Next

- [Config Builder](/builder) -- interactive configuration tool
- [Feature categories](/features/) -- browse all 89 features by category
- [Budget configuration](/configuration/budget) -- budget limits and overrides
