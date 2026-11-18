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

## Call weight formula

Each API request costs a **call weight** that depends on how many variables are fetched and how many forecast days are requested:

```
weight = ceil(variables / 10) x (forecastDays / 14)
```

A request with 20 variables and 4 forecast days weighs `ceil(20/10) x (4/14) = 2 x 0.286 = 0.57`. Fewer variables or fewer days means a lighter request.

## Calls per domain

Each domain polls at its own interval and weighs differently:

| Domain | Default days | Default interval | Weight formula | Cycles/month |
|--------|-------------|------------------|----------------|-------------|
| Forecast | 4 | 60 min | `ceil(vars/10) x (4/14) x max(1, modelCount)` | 720 |
| Ensemble | 4 | 6 h | `ceil(vars/10) x (4/14) x modelCount` | 120 |
| Air Quality | 5 | 60 min | `ceil(vars/10) x (5/14)` | 720 |
| Marine | 8 | 6 h | `ceil(vars/10) x (8/14)` | 120 |
| Flood | 92 | 24 h | `ceil(vars/10) x (92/14)` | 30 |
| Seasonal | 3 months | ~daily | 30/month x `max(ceil(vars/10) x (90/14), 1.3)` | 30 (fixed) |

The startup guard projects:

```
projected = sum over each domain: locations x weight x cycles/month
guard     = monthly budget x 0.8
```

### Ensemble weight details

With multiple comparison models (default: 3), the ensemble request includes both the forecast variables and the spread variables. The weight is `ceil((forecastVars + spreadVars) / 10) x (days/14) x modelCount`. With a single model, only the spread variables count.

### Flood weight details

Flood's 92 default forecast days make its per-request weight high: 5 variables at 92 days = `ceil(5/10) x (92/14) = 6.57`. But it only polls once per day (30 cycles/month), so the monthly cost stays moderate.

Enabling `Njord:Flood:IncludeEnsemble` adds 4 to the base weight (50 GloFAS members, budgeted as `FloodEnsembleWeight - 1 = 4` on top of the base call weight).

### Seasonal weight

Seasonal is budgeted as a flat 30 requests per month (roughly daily), each weighing `max(ceil(vars/10) x (months x 30 / 14), 1.3)`.

### Example: one location, defaults (21 features)

The defaults need three domains (Forecast, Ensemble, Air Quality) with 20 forecast variables, 3 ensemble spread variables, and 5 air quality variables:

| Domain | Variables | Weight | Interval | Cycles/month | Monthly |
|--------|-----------|--------|----------|-------------|---------|
| Forecast | 20 | 0.57 | 60 min | 720 | 411 |
| Ensemble | 23 (20 + 3 spread) x 3 models | 2.57 | 6 h | 120 | 309 |
| Air Quality | 5 | 0.36 | 60 min | 720 | 257 |
| **Total** | | | | | **977** |

977 calls is about **0.3%** of the free-tier month. Even ten locations (9,770) stay well below the guard.

### Example: all 89 features, 3 ensemble models

Enabling every feature adds Flood, Marine and Seasonal:

| Domain | Variables | Weight | Interval | Cycles/month | Monthly |
|--------|-----------|--------|----------|-------------|---------|
| Forecast | 42 | 1.43 | 60 min | 720 | 1,029 |
| Ensemble | 45 (42 + 3 spread) x 3 models | 4.29 | 6 h | 120 | 514 |
| Air Quality | 18 | 0.71 | 60 min | 720 | 514 |
| Flood | 5 | 6.57 | 24 h | 30 | 197 |
| Marine | 11 | 1.14 | 6 h | 120 | 137 |
| Seasonal | 3 | 30/month x 6.43 | ~daily | 30 | 193 |
| **Total** | | | | | **2,584** |

Still only **0.9%** of the free tier. The [Config Builder](/builder) calculates the exact cost for your configuration.

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
