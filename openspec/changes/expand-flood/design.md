## Context

`FloodClient` calls `v1/flood` with `WithDaily(variables)` and `WithForecastDays`. `FloodOptions.ForecastDays` defaults to 92 and the validator already allows 1 to 210. `FloodRiskHandler` evaluates `river_discharge` statistics through `FloodRiskAlertRule`. `BudgetCalculator` currently counts flood as weight 1 (`otherDomains`). Weather already derives confidence from ensemble spread, which is the model for this change.

## Goals / Non-Goals

**Goals:** opt-in access to GloFAS ensemble members, a confidence signal in `FloodRisk`, honest budget accounting.

**Non-Goals:** hydrological modeling, historical data, new alert types, member data on the wire.

## Decisions

### D1: Opt-in through FloodOptions.IncludeEnsemble

Ensemble members are heavy and only useful for one handler, so a plain option (default false, `Njord:Flood:IncludeEnsemble`) controls the `ensemble=true` parameter. `OpenMeteoRequestBuilder` gets a `WithEnsemble(bool)` step (invariant, only emitted when true). The `FloodParams.Ensemble` flag lets the request composition and `HasFields` express the requirement; `FloodRiskHandler` declares it as optional only when the option is on, so default behavior is byte-identical to today.

### D2: Members are a list on the point

`FloodPoint.EnsembleMembers` is an immutable list of member discharges for that day (null when not requested, null entries dropped). The DTO carries 50 optional series (`river_discharge_member01..50`); the response mapping zips them per day. Names and count are verified in task 1.1. Keeping the list on the point lets handlers stay pure functions over `DomainForecast<FloodPoint>`.

### D3: No extra percentile flags

The statistic flags (mean, median, max, min, p25, p75) already exist. The API does not offer further percentiles, so more flags would be invented values. If finer percentiles are wanted, they can be computed from members in the rule (task 3.1) without new API variables.

### D4: Confidence in FloodRisk

With members present, `FloodRiskAlertRule` derives `confidence` as the share of members whose discharge exceeds the alert threshold for the evaluated day, rounded to a percentage and optionally bucketed (low / medium / high; bucket edges pinned by spec). Severity still follows the existing rule; confidence is additional context and may suppress nothing. Without members confidence is null and output equals today's. The confidence travels as an optional member on the existing flood alert wire (additive, nullable).

### D5: Budget accounting

The effective weight of an ensemble flood call is not known until task 1.1 verifies it against the Open-Meteo rules (weight scales with variables and range). `BudgetCalculator` receives the flood weight from options: 1 without ensemble; a named constant with `IncludeEnsemble`. The startup guard (80 % of monthly budget) and the Config Builder's `budget.ts` use the same constant. If the verified weight makes realistic setups exceed the guard, the design falls back to limiting ensemble requests to a shorter horizon (a separate, smaller `ForecastDays` for ensemble requests) before shipping; this decision is recorded in task 1.1.

### D6: Longer forecasts stay a plain option

`ForecastDays` up to 210 already validates. This change only documents the trade-off (longer ranges raise the call weight per the same Open-Meteo rules, GloFAS skill drops with lead time) and makes the weight depend on `ForecastDays` in the same calculator formula if task 1.1 confirms that. The default stays 92.

## Risks / Trade-offs

- Weight may be high -> opt-in, startup guard, documented; fallback horizon in D5.
- 50 series per response increases memory in the gateway -> members are reduced to per-day lists immediately and never forwarded as stream payloads beyond the point.
- Member naming drift -> task 1.1.
