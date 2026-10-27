# Budget

njord respects the Open-Meteo free-tier limits by default and validates projected API usage at startup.

## Free-tier limits

Open-Meteo's free tier (non-commercial use) has these soft limits:

| Limit | Value |
|-------|-------|
| Monthly requests | 300,000 |
| Daily requests | 10,000 |
| Hourly requests | 5,000 |
| Per-minute requests | 600 |

## Calls per cycle

Each PollWorker fetches the domains required by the enabled [features](./#features-and-domains). The defaults (18 features) need three domains:

| Domain | Endpoint | Fetched when | Call weight |
|--------|----------|--------------|-------------|
| Forecast | `api.open-meteo.com/v1/forecast` | any weather feature is enabled | `ceil(variables / 10) * ceil(forecastDays / 14)` |
| Ensemble | Ensemble Mean API | a feature requiring ensemble spread or model comparison is enabled | Spread-only: 1. With models: `ceil(variables / 10) * ceil(days / 14) * modelCount` |
| Air Quality | Air Quality API | any Air Quality feature is enabled | 1 |
| Flood | Flood API | `FloodRisk`, `FloodRain` or `FloodTimeline` is enabled | 1 |
| Marine | Marine API | `Swimming`, `Surf`, `OceanCurrents` or `CoastalFlood` is enabled | 1 |
| Seasonal | Seasonal API | `SeasonalOutlook` is enabled | ~1/day (daily cycle) |
| Satellite | Satellite Archive API | `SatelliteRadiation` is enabled | ~1/hour (hourly cycle) |

```
calls per cycle  = locations x sum(domain weights)
cycles per month = 30 days / poll interval
projected        = calls per cycle x cycles per month
guard            = monthly budget x 0.8
```

### Example: one location, defaults (Lucerne)

| Setting | Value |
|---------|-------|
| Locations | 1 |
| Domains | Forecast, Ensemble (spread-only), Air Quality |
| Poll interval | 60 minutes |

```
calls per cycle  = 1 x (4 + 1 + 1) = 6   (forecast ~30 vars = weight 4, ensemble spread = 1, AQ = 1)
cycles per month = 30 x 24 = 720
projected        = 6 x 720 = 4,320 per month
guard            = 300,000 x 0.8 = 240,000
```

4,320 calls is about 1.4% of the free-tier month. Even ten locations (43,200) stay well below the guard.

### Example: all features, 3 ensemble comparison models

```
calls per cycle  = 1 x (4 + 12 + 1 + 1 + 1) = 19  (forecast 4, ensemble with 3 models 12, AQ 1, Flood 1, Marine 1)
                   + Seasonal ~1/day + Satellite ~24/day
projected        ≈ 19 x 720 + 30 + 720 = ~14,430 per month
```

Still only 4.8% of the free tier. The [Config Builder](/builder) calculates the exact cost for your configuration.

## Startup guard

At startup njord refuses to start when the projection exceeds 80% of the monthly budget. If you hit it:

- Increase `Polling:PollInterval` (every 2 hours halves the calls)
- Reduce the number of locations
- Disable features that need an extra domain
- Enable fewer weather features to keep the weather variable count, and so the API call weight, low

## Polling:BudgetOverride

For a commercial Open-Meteo plan, or to self-throttle below the free tier:

```json
{
  "Njord": {
    "Polling": {
      "BudgetOverride": {
        "RequestsPerMonth": 500000,
        "RequestsPerMinute": 1000
      }
    }
  }
}
```

Set both fields. The per-minute limit controls gateway throttling within a cycle; the monthly limit controls the startup guard.

## Usage tracking

The `BudgetTracker` persists recorded API calls. The monthly and daily usage is available over gRPC (`GetStatus`) and as usage sensors in Home Assistant.

::: tip
The default 60-minute interval is deliberately polite to the free Open-Meteo API. Only change it with a specific reason.
:::
