## Context

BudgetGuard validates at startup whether the configured locations, features, and poll
interval will stay within the Open-Meteo free-tier budget (300k calls/month). Today it
computes accurate call weights only for Forecast and Ensemble; all other domains
(AirQuality, Flood, Marine, Satellite, Seasonal) are counted as 1 call each.

Open-Meteo bills every endpoint the same way:
`effective_calls = ceil(variables / 10) × (days / 14.0)` — fractional, not ceiling on
the days factor. The Ensemble and Seasonal API docs list per-request estimates (4.0 and
1.3 respectively) as guidelines, but the formula applies uniformly.

The domain Options (ForecastOptions, EnsembleOptions, FloodOptions) currently live in
`Njord.Core` solely because BudgetGuard in `Njord.Polling` consumes them. This breaks
the convention that domain Options live in `Njord.OpenMeteo`.

## Goals / Non-Goals

**Goals:**
- Accurate call-weight calculation for all 7 domains
- Fractional day multiplier matching Open-Meteo billing
- All domain Options in `Njord.OpenMeteo/Configuration/`
- BudgetGuard independent of any specific domain Options project

**Non-Goals:**
- Changing the Catalog (GetCatalog/EnsembleOptions dependency) — separate change
- Runtime budget enforcement changes
- Config Builder formula update (follow-up)

## Decisions

### D1: Introduce DomainCallConfig in Core

A sealed record in `Njord.Core.Polling` (next to `CallWeight` and `RequestBudget`):

```
sealed record DomainCallConfig(
    int ForecastDays,
    int ForecastModelCount,
    int EnsembleDays,
    int EnsembleModelCount,
    int AirQualityDays,
    int FloodDays,
    bool FloodIncludeEnsemble,
    int MarineDays,
    int SatellitePastHours,
    int SeasonalMonths);
```

The Host's setup container builds it from the resolved domain Options and registers it
as `IOptions<DomainCallConfig>` via `PostConfigure` or a simple factory. BudgetGuard
and PollingOptionsValidator consume only this record plus `DomainRequirements`.

**Why not pass individual Options?** The current approach forces Core to host Options
that belong to OpenMeteo. A single record in Core is a stable, minimal interface — adding
a domain means one new property, not a new project reference.

**Why not put it in Polling?** BudgetGuard lives in Polling, but the record could be
useful elsewhere (e.g., a future budget dashboard). Core is the lowest common ancestor.

### D2: CallWeight returns double

`CallWeight.Of(int variableCount, int forecastDays)` changes to return `double`:
`Math.Ceiling(variableCount / 10.0) * (forecastDays / 14.0)`.

BudgetGuard sums the doubles and rounds at the end for the projection.

### D3: BudgetGuard per-domain weight calculation

Replace the current flat `otherDomains` count with per-domain weight computation:

```
foreach required domain:
    vars = PopCount(requirements.All<Domain>)
    days = callConfig.<Domain>Days
    weight += CallWeight.Of(vars, days) × modelMultiplier
```

Special cases:
- **Forecast**: `× max(1, ForecastModelCount)` (existing behavior)
- **Ensemble**: `× EnsembleModelCount` (existing behavior, uses resolved model count)
- **Flood with ensemble**: `+ (FloodEnsembleWeight - 1)` when `FloodIncludeEnsemble`
- **Satellite**: uses `PastHours` as the time dimension with a 336-hour (14-day)
  denominator, matching the archive API's hourly billing
- **Seasonal**: fixed `SeasonalCallsPerMonth` per location (polled ~daily, not per
  cycle), weighted by `ceil(vars/10) × (months * 30 / 14.0)` or the documented 1.3
  estimate — use whichever is more conservative

### D4: Options move to OpenMeteo

Move `ForecastOptions`, `ForecastOptionsValidator`, `EnsembleOptions`,
`EnsembleOptionsValidator`, `FloodOptions`, `FloodOptionsValidator` from
`Njord.Core/Configuration/` to `Njord.OpenMeteo/Configuration/`. Namespace changes from
`Njord.Core.Configuration` to `Njord.OpenMeteo.Configuration`.

Consumers that change:
- OpenMeteo clients: update `using` (they already reference OpenMeteo)
- Host setup containers: update `using` (Host references both Core and OpenMeteo)
- `Njord.Grpc/WeatherGrpcService.cs`: currently uses `EnsembleOptions` for Catalog —
  add a temporary `ProjectReference` to OpenMeteo, or extract the model list into
  `DomainCallConfig.EnsembleModels` (but proposal says Catalog is a separate change,
  so the temporary reference is cleaner)

### D5: Grpc temporary OpenMeteo reference

`Njord.Grpc` gains a `ProjectReference` to `Njord.OpenMeteo` for `EnsembleOptions`
access in the Catalog path. This is explicitly temporary — the Catalog change will
remove it. The architecture test should allow this edge temporarily.

**Alternative considered:** Adding `EnsembleModels string[]` to `DomainCallConfig`.
Rejected because it mixes budget concerns with catalog concerns and the Catalog change
will resolve the dependency properly.

## Risks / Trade-offs

- **[Grpc → OpenMeteo reference]** Temporarily breaks the "feature libs independent"
  rule. → Mitigated by marking it as a known tech-debt item and resolving in the Catalog
  change. Architecture test gets a documented exception.
- **[Satellite billing unclear]** The archive API may count differently than the forecast
  API. → Use the conservative estimate; adjust if monitoring shows divergence.
- **[Seasonal estimate]** The 1.3 calls/request from the docs may not match the formula.
  → Use `max(formula, 1.3)` per request as a safety margin.

## Open Questions

- Satellite archive API: is `pastHours / 336` the right denominator, or does Open-Meteo
  count archive requests differently? May need to verify against actual usage.
