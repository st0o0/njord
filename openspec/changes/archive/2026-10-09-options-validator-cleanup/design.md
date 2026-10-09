## Context

`NjordOptionsValidator.Validate` (`src/Njord.Core/Configuration/NjordOptionsValidator.cs`,
142 lines) computes a projected monthly API usage inline:
```csharp
var cyclesPerMonth = Month.TotalMinutes / options.PollInterval.TotalMinutes;
var projected = (int)Math.Round(totalModelsPerCycle * cyclesPerMonth * resolved.ApiCallWeight);
var guard = (int)Math.Round(budget.RequestsPerMonth * MonthlyBudgetGuardFactor); // 80%
```
`BudgetCalculator.Validate` (`src/Njord.Core/Configuration/BudgetCalculator.cs`) computes a
related but differently-derived number:
```csharp
var apiCallWeight = (int)Math.Ceiling(hourlyCount / 10.0); // integer weight, not resolved.ApiCallWeight
var cyclesPerDay = (int)(TimeSpan.FromDays(1) / options.PollInterval);
var projectedMonthly = (long)callsPerCycle * cyclesPerDay * 30; // calendar approximation, not exact months
```
These can disagree: `BudgetCalculator` rounds the per-call weight up to the nearest integer
multiple of 10 hourly parameters, while the validator uses the exact
`resolved.ApiCallWeight` from `ParameterRegistry.Resolve`; `BudgetCalculator` approximates a
month as `30 * cyclesPerDay`, the validator uses the exact `Month.TotalMinutes /
PollInterval.TotalMinutes`. `BudgetCalculator.Validate` is also not actually called from
`NjordOptionsValidator` today — it appears to be used elsewhere (e.g. a status/diagnostics
endpoint) independently.

## Goals / Non-Goals

**Goals:**
- One authoritative budget-projection formula, used by both the startup validator and
  anywhere else a projection is needed.
- Remove the four-times-repeated range check in `IndexOptionsValidator`.
- Separate `NjordOptionsValidator`'s infrastructure probes (filesystem writability,
  `ModelCoverageRegistry` lookup) from its pure options-shape validation, as named methods.

**Non-Goals:**
- Not changing the 80% guard threshold or the 0.0-5.0 sensitivity range.
- Not changing where `BudgetCalculator.Validate` is called from outside this validator
  (e.g. status/ops endpoints keep using it as-is).

## Decisions

1. **Resolve the formula discrepancy by making `BudgetCalculator.Validate` use the exact
   formula** (`resolved.ApiCallWeight` via `ParameterRegistry.Resolve`, exact
   `Month.TotalMinutes / PollInterval.TotalMinutes` cycles), matching what
   `NjordOptionsValidator` currently does — the validator's formula is the more precise one
   (exact month length, exact resolved weight vs. a ceil-to-10 approximation). `
   BudgetCalculator.CountHourlyParameters`/the ceil-to-10 `apiCallWeight` approximation is
   removed in favor of `resolved.ApiCallWeight`.
   Alternative considered: keep both formulas, just have the validator call
   `BudgetCalculator.Validate` and accept its (less precise) number. Rejected — silently
   becoming less precise at the one place that gates startup (blocking a deploy) is worse
   than making `BudgetCalculator` precise once and having both call sites agree.
2. **`NjordOptionsValidator` calls `BudgetCalculator.Validate(options)` and uses its
   `UsagePercent > 80` as the guard condition** (replacing the inline `projected > guard`
   check), keeping the exact same error message content (projected count, model-location
   pairs, poll interval, weight, guard amount) computed from `BudgetCalculator.Validate`'s
   result fields plus `BudgetCalculator.GetEffectiveBudget`.
3. **`IndexOptionsValidator.ValidateRange(double? value, string propertyName, string path,
   double min, double max, List<string> errors)`** — a private static helper, called once
   per sensitivity property, producing the exact same error message format as today
   (`"{path}.{propertyName} must be between {min} and {max}, got {value}"`).
4. **`NjordOptionsValidator.Validate` restructured into named private steps**: e.g.
   `ValidateLocationsAndModels`, `ValidateBudget` (using decision 2), `ValidatePersistence`
   (the filesystem probe), called in sequence from `Validate`, each appending to the same
   `failures` list. No change to check order or content.

## Risks / Trade-offs

- [Risk] Making `BudgetCalculator.Validate` more precise changes its `ProjectedMonthlyCalls`/
  `UsagePercent` output for any existing caller outside this validator (e.g. a status
  endpoint). → Mitigation: grep all callers of `BudgetCalculator.Validate`/
  `CountHourlyParameters` before changing the formula; if another caller depends on the
  current approximation's exact number (unlikely, since it's a display/guard value, not a
  persisted or compared value), document the change in that caller's test expectations too.
- [Risk] Reordering `NjordOptionsValidator`'s checks into named methods could change which
  error appears first in `failures` for a config that fails multiple checks. → Mitigation:
  keep the exact same calling order in `Validate`, just moved into named methods.

## Migration Plan

Pure refactor + one formula-precision fix, single deploy, no persisted/wire format changes.
Revert is trivial for the structural changes; if the formula-precision fix changes behavior
for any existing test fixture, that test's expectation is updated as part of this change
(not silently) — called out explicitly in the PR description.

## Open Questions

None.
