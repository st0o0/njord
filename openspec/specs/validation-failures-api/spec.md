# validation-failures-api Specification

## Purpose
TBD - created by archiving change validation-failures-migration. Update Purpose after archive.
## Requirements
### Requirement: Validators use ValidationFailures instead of List accumulation
Validators that accumulate error messages SHALL use `ValidationFailures` from Terp instead of `List<string>` with a closing ternary. The validator body declares a `ValidationFailures` instance (with or without a section prefix), adds failures via `Require()` or `Add()`, and returns `ToResult()`.

#### Scenario: Validator with section prefix
- **WHEN** a feature config validator (e.g. `HeatConfigValidator`) validates options for a config section
- **THEN** it SHALL construct `new ValidationFailures(IConfigSection.SectionOf<T>())` and use `Require()` for boolean checks and `Add()` for pre-formatted messages
- **AND** the resulting error messages SHALL be identical to the pre-migration output

#### Scenario: Validator without section prefix
- **WHEN** a non-feature validator (e.g. `PollingOptionsValidator`, `HealthOptionsValidator`) validates options without a config section prefix
- **THEN** it SHALL construct `new ValidationFailures()` without a section argument
- **AND** the resulting error messages SHALL be identical to the pre-migration output

#### Scenario: Single-ternary validators are deferred
- **WHEN** a validator uses a single-ternary or early-return pattern (not `List<string>` accumulation)
- **THEN** it SHALL NOT be converted in this change but MAY be converted opportunistically in future changes

### Requirement: ThresholdValidation callers pass bare property names
Validators calling `ThresholdValidation.Violation` SHALL pass the bare property name (e.g. `"Thresholds"`) instead of the section-prefixed path (e.g. `$"{section}:Thresholds"`), because `ValidationFailures` adds the section prefix.

#### Scenario: No double-prefixed threshold messages
- **WHEN** a validator using `ValidationFailures` with a section prefix calls `ThresholdValidation.Violation`
- **THEN** the path argument SHALL be the bare property name
- **AND** the final error message SHALL contain the section prefix exactly once

#### Scenario: All threshold callers change together
- **WHEN** the `ValidationFailures` migration is applied to alert validators
- **THEN** every validator that calls `ThresholdValidation.Violation` SHALL switch to bare property names in the same commit as the `ValidationFailures` adoption

### Requirement: Error messages are preserved
The migration SHALL NOT change the content or format of any validation error message. Existing test assertions on message strings SHALL pass without modification.

#### Scenario: Feature validator messages unchanged
- **WHEN** any feature config with a known-invalid value is validated after migration
- **THEN** the `ValidateOptionsResult.Failures` collection SHALL contain exactly the same strings as before migration

#### Scenario: Non-feature validator messages unchanged
- **WHEN** any non-feature options class with a known-invalid value is validated after migration
- **THEN** the `ValidateOptionsResult.Failures` collection SHALL contain exactly the same strings as before migration

