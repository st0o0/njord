## Why

BudgetGuard massively underestimates API call costs for non-Forecast/Ensemble domains.
AirQuality, Flood, Marine, Satellite, and Seasonal are all counted as 1 call per cycle,
but Open-Meteo charges `ceil(vars/10) × (days/14)` for every endpoint. Flood with 92
default ForecastDays costs ~6.6 calls per request, not 1 — the guard could approve
configurations that blow through the free-tier budget. Additionally, `CallWeight.Of()`
uses `ceil()` on the days multiplier where Open-Meteo uses fractional counting.

## What Changes

- **Fix `CallWeight.Of()`** to use fractional day multiplier (`days / 14.0`) instead of
  `ceil(days / 14)`, matching Open-Meteo's documented billing.
- **Calculate real call weights for all domains** in BudgetGuard instead of counting
  AirQuality, Flood, Marine, Satellite as 1 each. Each domain gets
  `ceil(resolvedVars / 10) × (forecastDays / 14.0)`.
- **Introduce `DomainCallConfig`** in `Njord.Core` — a read-only record carrying the
  per-domain ForecastDays/PastHours and model counts that BudgetGuard needs. The Host
  builds it from the domain Options and registers it as `IOptions<DomainCallConfig>`.
- **Move ForecastOptions, EnsembleOptions, FloodOptions** (+ their validators) from
  `Njord.Core/Configuration/` to `Njord.OpenMeteo/Configuration/`, consistent with
  AirQuality/Marine/Seasonal/Satellite Options that already live there.
- **Satellite special case**: archive API, counted per poll cycle by PastHours rather
  than ForecastDays — BudgetGuard must use `ceil(vars/10) × (pastHours / 336.0)` or
  the documented ~1.0 per request estimate.

## Non-goals

- Changing the Catalog (`GetCatalog`) — that is a separate concern.
- Adding runtime budget tracking or throttling changes.
- Changing the polling intervals or domain discovery.
- Touching the Config Builder (docs site) — its budget formula is a follow-up.

## API-budget impact estimate

No change to actual API usage. This change only improves the **accuracy of the
projection** that runs at startup. Configurations previously approved despite exceeding
the 80% guard will now be correctly rejected. Example: 1 location, all features enabled,
60-min poll, default ForecastDays — the old guard counted ~11 calls/cycle; the corrected
guard will count ~18 calls/cycle (Flood alone jumps from 1 to ~6.6).

## Capabilities

### New Capabilities

- `domain-call-config`: A read-only record in Core carrying per-domain ForecastDays,
  model counts, and PastHours — built by the Host from all domain Options so that
  BudgetGuard (Polling) can compute accurate call weights without referencing OpenMeteo.

### Modified Capabilities

- `per-domain-options`: ForecastOptions, EnsembleOptions, FloodOptions move from
  `Njord.Core/Configuration/` to `Njord.OpenMeteo/Configuration/`. No property changes,
  only namespace and project location.
- `feature-requirements-options`: BudgetGuard switches from consuming individual domain
  Options to consuming `DomainCallConfig` + `DomainRequirements` for all weight
  calculations.

## Impact

- `Njord.Core`: new `DomainCallConfig` record, `CallWeight.Of()` signature change
  (returns `double` instead of `int`).
- `Njord.Core/Configuration/`: ForecastOptions, EnsembleOptions, FloodOptions and their
  validators removed (moved to OpenMeteo).
- `Njord.OpenMeteo/Configuration/`: receives the three Options + validators.
- `Njord.Polling/Configuration/BudgetGuard.cs`: rewritten to use `DomainCallConfig` +
  `DomainRequirements`, per-domain weight calculation.
- `Njord.Polling/Configuration/PollingOptionsValidator.cs`: depends on
  `IOptions<DomainCallConfig>` instead of individual domain Options.
- `Njord/` (Host): builds and registers `DomainCallConfig` from all domain Options.
- `Njord.Grpc/WeatherGrpcService.cs`: currently depends on `EnsembleOptions` for
  Catalog — unchanged for now (Catalog fix is a separate change), will temporarily
  reference `Njord.OpenMeteo` or use a minimal shim until the Catalog change.
- Tests: BudgetGuardTests, CallWeightTests, PollingOptionsValidatorTests,
  ArchitectureTests (zone rules for the moved files).
