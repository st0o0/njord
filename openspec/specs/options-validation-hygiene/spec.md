# options-validation-hygiene Specification

## Purpose

Keeps options validators lean: they reuse authoritative calculations (such as `BudgetGuard`) instead of recomputing them, and share a helper for repeated range checks.

## Requirements
### Requirement: Validators reuse authoritative calculations instead of recomputing them
When an `IValidateOptions<T>` implementation needs a projected value that another
component in the codebase already computes authoritatively (e.g. projected monthly API
usage, computed by `BudgetGuard`), the validator SHALL call that component rather than
recomputing the value with a separate formula.

#### Scenario: Budget validation uses one formula
- **WHEN** `NjordOptionsValidator` checks projected API usage against the monthly budget
- **THEN** it uses `BudgetGuard`'s result rather than an independent
  inline calculation

### Requirement: Repeated range checks are factored into a shared helper
When a validator checks the same numeric range against multiple properties of the same
shape (e.g. several `0.0-5.0` sensitivity properties), it SHALL use one shared
range-validation helper called once per property, rather than repeating the comparison and
error-message construction for each property.

#### Scenario: Index preference sensitivities share one range check
- **WHEN** `IndexOptionsValidator` validates `HeatSensitivity`, `HumiditySensitivity`,
  `WindSensitivity`, and `RainSensitivity`
- **THEN** each call goes through the same range-validation helper, and the helper's
  error-message format is used for all four

#### Scenario: Validation outcomes are unchanged
- **WHEN** any existing valid or invalid configuration from `Njord.Core.Tests` is validated
  after this refactor
- **THEN** the validation result (success/failure and the set of error messages) is
  identical to before the refactor, except for any formula-discrepancy resolution
  explicitly documented in the change's design.md

### Requirement: Domain options validators live beside their option classes

`ForecastOptionsValidator`, `EnsembleOptionsValidator`, and `FloodOptionsValidator` SHALL reside in `Njord.Core/Configuration/` beside the option classes they validate, not in `Njord.OpenMeteo`.

#### Scenario: Validators co-located with options
- **WHEN** `ForecastOptions` is defined in `Njord.Core/Configuration/`
- **THEN** `ForecastOptionsValidator` SHALL also reside in `Njord.Core/Configuration/`

#### Scenario: Registration moves to Core
- **WHEN** validators are registered for ForecastOptions, EnsembleOptions, and FloodOptions
- **THEN** registration SHALL happen in `CoreSetupContainer` (or an extension method in Core), not in `OpenMeteoSetupContainer`

#### Scenario: Validation behavior unchanged
- **WHEN** any existing valid or invalid configuration is validated after the move
- **THEN** the validation result SHALL be identical to before the move

### Requirement: Validator for dead Model property is removed
The OpenMeteoOptionsValidator SHALL NOT contain a validation branch for the removed ForecastOptions.Model property.

#### Scenario: No migration hint for Model
- **WHEN** ForecastOptions is validated
- **THEN** no validation message references a `Model` property or migration hint

