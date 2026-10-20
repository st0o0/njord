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

Each PollWorker fetches the domains required by the enabled [features](./#features-and-domains), one API call each. The defaults need two:

| Call | Endpoint | Fetched when |
|------|----------|--------------|
| Weather | Ensemble Mean | a Weather feature is enabled (default) |
| Air Quality | Air Quality API | `PollenAlert`, `Pm25Alert` or another Air Quality feature is enabled (default) |
| Flood | Flood API | `FloodRisk` or `FloodRain` is enabled |
| Marine | Marine API | `SwimmingConditions` or `SurfConditions` is enabled |


```
calls per cycle  = locations x (weather weight + other required domains)
cycles per month = 30 days / poll interval
projected        = calls per cycle x cycles per month
guard            = monthly budget x 0.8
```

### Example: one location (Lucerne)

| Setting | Value |
|---------|-------|
| Locations | 1 |
| Domains | Weather, Air Quality (default features) |
| Poll interval | 60 minutes |

```
calls per cycle  = 1 x (2 + 1) = 3   (17 weather variables = weight 2, plus Air Quality)
cycles per month = 30 x 24 = 720
projected        = 3 x 720 = 2,160 per month
guard            = 300,000 x 0.8 = 240,000
```

2,160 calls is about 0.7% of the free-tier month. Even ten locations (21,600) stay far below the guard.

Open-Meteo counts requests with many hourly variables or long forecasts as more than one call. That weight applies to the Weather domain only; see [Weather](./weather#api-call-weight).

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
