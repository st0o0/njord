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

Each PollWorker fetches three domains per cycle, one API call each:

| Call | Endpoint |
|------|----------|
| Weather | Ensemble Mean |
| Air Quality | Air Quality API |
| Flood | Flood API |

Solar variables are part of the Weather call when the `Solar` parameter group is enabled, so they add no call.

```
calls per cycle  = locations x 3
cycles per month = 30 days / poll interval
projected        = calls per cycle x cycles per month
guard            = monthly budget x 0.8
```

### Example: one location (Lucerne)

| Setting | Value |
|---------|-------|
| Locations | 1 |
| Domains | Weather, Air Quality, Flood |
| Poll interval | 60 minutes |

```
calls per cycle  = 1 x 3 = 3
cycles per month = 30 x 24 = 720
projected        = 3 x 720 = 2,160 per month
guard            = 300,000 x 0.8 = 240,000
```

2,160 calls is about 0.7% of the free-tier month. Even ten locations (21,600) stay far below the guard.

Open-Meteo counts requests with many hourly variables as more than one call (more than 10 variables count fractionally). That weight is applied on top; see [Parameters](./parameters#api-call-weight).

## Startup guard

At startup njord refuses to start when the projection exceeds 80% of the monthly budget. If you hit it:

- Increase `Polling:PollInterval` (every 2 hours halves the calls)
- Reduce the number of locations
- Exclude parameters to lower the API call weight

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
