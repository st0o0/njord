## MODIFIED Requirements

### Requirement: Repeated range checks are factored into a shared helper
When a validator checks the same numeric range against multiple properties of the same
shape (e.g. several `0.0-5.0` sensitivity properties), it SHALL use one shared
range-validation helper called once per property, rather than repeating the comparison and
error-message construction for each property.

`ThresholdValidation.Violation` callers SHALL pass a bare property name as the path argument when the calling validator uses `ValidationFailures` with a section prefix. The helper itself is unchanged; only the call sites adjust.

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

#### Scenario: ThresholdValidation path argument is bare when ValidationFailures prefixes
- **WHEN** a validator constructs `ValidationFailures` with a section prefix and calls `ThresholdValidation.Violation`
- **THEN** the path argument SHALL be the bare property name (e.g. `"Thresholds"`) so the section appears exactly once in the final message
