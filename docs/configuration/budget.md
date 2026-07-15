# Budget

njord respects the Open-Meteo free-tier rate limits by default and validates projected API usage at startup.

## Free-tier limits

Open-Meteo's free tier (non-commercial use) has these soft limits:

| Limit | Value |
|-------|-------|
| Monthly requests | 300,000 |
| Per-minute requests | 600 |
| Daily requests | 10,000 |
| Hourly requests | 5,000 |

## Budget projection

At startup, njord calculates projected monthly API usage and refuses to start if it exceeds 80% of the budget:

```
projected = totalModelsPerCycle x cyclesPerMonth x apiCallWeight
guard     = budget.requestsPerMonth x 0.8
```

Where:
- **totalModelsPerCycle** = sum of effective models across all locations (global + per-location)
- **cyclesPerMonth** = 30 days / poll interval
- **apiCallWeight** = `ceil(hourlyVariableCount / 10)` — see [parameters](./parameters)

### Example calculation

| Setting | Value |
|---------|-------|
| Locations | 2 |
| Models per location | 5 (average) |
| Poll interval | 60 minutes |
| Parameter groups | Weather only (31 hourly vars) |
| API call weight | ceil(31/10) = 4 |

```
totalModelsPerCycle = 10
cyclesPerMonth      = 30 x 24 x 60 / 60 = 720
projected           = 10 x 720 x 4 = 28,800
guard               = 300,000 x 0.8 = 240,000
```

28,800 is well within the 240,000 guard, so startup proceeds.

### When the budget is tight

With many locations, models, or all parameter groups enabled, projected usage can approach the limit. Options to reduce it:

- **Increase `PollInterval`** — polling every 2 hours halves the request count
- **Reduce models** — fewer models per location means fewer API calls
- **Use `Exclude`** — removing hourly parameters can lower the API call weight
- **Cap `ForecastDays`** — requesting fewer days slightly reduces API weight for large variable counts

## BudgetOverride

If you have a commercial Open-Meteo plan or want to self-throttle below the free tier, override the budget:

```json
{
  "Njord": {
    "BudgetOverride": {
      "RequestsPerMonth": 500000,
      "RequestsPerMinute": 1000
    }
  }
}
```

Set both fields. The per-minute limit controls request throttling within a poll cycle; the monthly limit controls the startup guard.

::: tip
njord is designed to be polite to the free Open-Meteo API. The default 60-minute poll interval with a handful of models stays well within limits. Only adjust the budget if you have a specific reason.
:::
