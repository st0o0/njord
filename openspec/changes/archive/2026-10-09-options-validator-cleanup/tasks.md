## 1. Resolve the budget-formula discrepancy

- [x] 1.1 Grep all callers of `BudgetCalculator.Validate` and `BudgetCalculator.CountHourlyParameters` outside `NjordOptionsValidator` to know what else is affected by precision changes.
- [x] 1.2 Update `BudgetCalculator.Validate` (`src/Njord.Core/Configuration/BudgetCalculator.cs`) to use `resolved.ApiCallWeight` (via `ParameterRegistry.Resolve`) instead of the `Math.Ceiling(hourlyCount / 10.0)` approximation, and the exact `Month.TotalMinutes / options.PollInterval.TotalMinutes` cycle count instead of `cyclesPerDay * 30`.
- [x] 1.3 Update/add `BudgetCalculatorSpec` cases in `src/Njord.Core.Tests/Configuration/` covering the corrected formula against known inputs; update any existing case whose expected `ProjectedMonthlyCalls`/`UsagePercent` changes as a result, and note the change explicitly.

## 2. NjordOptionsValidator uses BudgetCalculator

- [x] 2.1 Replace `NjordOptionsValidator`'s inline `projected`/`guard`/`cyclesPerMonth` calculation with a call to `BudgetCalculator.Validate(options)`, using its `UsagePercent` against the existing 80% guard (`MonthlyBudgetGuardFactor`).
- [x] 2.2 Keep the existing failure message content (projected count, model-location pairs, poll interval, weight, guard amount) by deriving the message from `BudgetCalculator.Validate`'s result fields plus `BudgetCalculator.GetEffectiveBudget(options)`.
- [x] 2.3 Update/confirm `NjordOptionsValidatorSpec` budget-guard test cases in `src/Njord.Core.Tests/Configuration/` still pass (or update expectations per the formula-precision fix from task 1).

## 3. IndexOptionsValidator range-check helper

- [x] 3.1 Add `ValidateRange(double? value, string propertyName, string path, double min, double max, List<string> errors)` as a private static helper in `src/Njord.Core/Configuration/IndexOptionsValidator.cs`.
- [x] 3.2 Replace the four `HeatSensitivity`/`HumiditySensitivity`/`WindSensitivity`/`RainSensitivity` checks in `ValidatePreferences` with four calls to the helper, preserving the exact error-message format.
- [x] 3.3 Confirm existing `IndexOptionsValidatorSpec` cases in `src/Njord.Core.Tests/Configuration/` pass unmodified.

## 4. Separate concerns in NjordOptionsValidator.Validate

- [x] 4.1 Extract the filesystem-writability probe (lines ~116-136) into a named private method (e.g. `ValidatePersistenceWritable`).
- [x] 4.2 Extract the `ModelCoverageRegistry` plausibility check into a named private method (e.g. `ValidateModelCoverage`).
- [x] 4.3 Confirm `Validate` calls the named methods in the same order as today, and existing `NjordOptionsValidatorSpec` cases pass unmodified.

## Validation

- [x] 5.1 `dotnet build Njord.slnx` (run from `src/`)
- [x] 5.2 `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj --no-build`
- [x] 5.3 `dotnet slopwatch analyze -d . --fail-on warning` (run from repo root)
