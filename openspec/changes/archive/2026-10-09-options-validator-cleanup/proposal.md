## Why

A cross-codebase review against the sibling FunkArr project found that njord's startup
options validation is less focused than FunkArr's. Verified concretely:
`NjordOptionsValidator.Validate` (142 lines) recomputes a budget projection
(`totalModelsPerCycle * cyclesPerMonth * resolved.ApiCallWeight` against an 80% guard) using
a different formula than `BudgetCalculator.Validate` (`callsPerCycle * cyclesPerDay * 30`
against 100%) — two independent, slightly different approximations of the same quantity
that could disagree, instead of the validator calling the one authoritative calculator.
`IndexOptionsValidator.ValidatePreferences` repeats the identical `< 0.0 or > 5.0` range
check four times (HeatSensitivity, HumiditySensitivity, WindSensitivity, RainSensitivity).
`NjordOptionsValidator` also interleaves pure options validation with infrastructure probes
(a filesystem write-probe, an external `ModelCoverageRegistry` lookup) in the same method.
FunkArr's validators stay narrowly scoped (24-64 lines, one concern each).

## What Changes

- `NjordOptionsValidator`: replace the inline budget-projection calculation with a call to
  `BudgetCalculator.Validate(options)`, using its `UsagePercent`/`WithinBudget` result
  instead of recomputing `projected`/`guard` with a second formula. Resolve the formula
  discrepancy in `BudgetCalculator.Validate` itself if the two produce materially different
  numbers for the same input (design decision: which formula is authoritative).
- `IndexOptionsValidator.ValidatePreferences`: replace the four copy-pasted
  `< 0.0 or > 5.0` checks with one `ValidateRange(double? value, string propertyName, string
  path, double min, double max, List<string> errors)` helper called four times.
- `NjordOptionsValidator`: split the filesystem-writability probe and the
  `ModelCoverageRegistry` plausibility check into clearly separated private methods (or, if
  warranted, their own validator classes) so the main `Validate` method reads as a sequence
  of named checks rather than one long interleaved block. No behavior change to what is
  checked or when.

## Capabilities

### New Capabilities

- `options-validation-hygiene`: the implementation-pattern requirement (mirroring the
  `actor-state-pattern`/`grpc-mapping-layer` style already used in this project) that
  `IValidateOptions<T>` implementations avoid duplicated range checks and avoid
  recomputing a value that another component already computes authoritatively.

### Modified Capabilities

None. The observable validation outcomes (which configurations pass/fail, and why) are
unchanged except where this proposal's "Why" explicitly calls out the budget-formula
discrepancy to resolve (see design.md for the resolution).

## Impact

- `src/Njord.Core/Configuration/NjordOptionsValidator.cs`
- `src/Njord.Core/Configuration/IndexOptionsValidator.cs`
- `src/Njord.Core/Configuration/BudgetCalculator.cs`
- Matching tests: `src/Njord.Core.Tests/Configuration/`

## Non-goals

- No change to the actual budget guard percentage (80%) or any sensitivity range (0.0-5.0)
  unless the two divergent budget formulas are found to disagree materially for realistic
  configurations — in that case, document the chosen authoritative formula and its impact
  in design.md rather than silently picking one.
- No change to `NjordOptionsValidator`'s other checks (MQTT host presence, parameter
  resolution errors) beyond relocating them into named methods.
- Not addressing the `Njord.Grpc`, `Njord.Mqtt`, or `Njord.Compute` findings from the same
  review — those are tracked as separate changes.

## API budget impact

None directly — this change only touches how the existing budget guard is calculated
internally, not any polling behavior. If the formula-discrepancy resolution (see
Non-goals) changes which configurations are flagged as over-budget, that is a validation
behavior change to call out explicitly during implementation, not a polling change.
