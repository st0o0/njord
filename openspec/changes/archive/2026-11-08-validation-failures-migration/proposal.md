## Why

Njord has 52 `IValidateOptions<T>` validators. 38 of them repeat the same boilerplate: declare `var failures = new List<string>()`, accumulate error strings, return a ternary `failures.Count == 0 ? Success : Fail(failures)`. Terp is introducing `ValidationFailures` - a purpose-built builder with `Require()`, `Add()`, optional section-prefix, and `ToResult()` - that eliminates the boilerplate and makes validators read as a list of rules rather than imperative plumbing.

Blocked until Terp ships the `ValidationFailures` API (not in 0.2.1).

## What Changes

- **Validator bodies migrate to `ValidationFailures`**: the 38 validators using the `List<string>` accumulation pattern switch to `new ValidationFailures(section?)` with `Require()` / `Add()` / `ToResult()`.
- **`ThresholdValidation.Violation` callers drop the section prefix**: `ValidationFailures` adds the section prefix itself, so callers pass the bare property name (`"Thresholds"` instead of `$"{section}:Thresholds"`). The 16 alert validators calling `ThresholdValidation` change together.
- **14 single-ternary / early-return validators are deferred**: converted opportunistically when next touched, not in this change.
- **Registration unchanged**: `AddNjordOptions<T>`, `AddNjordOptions<T, TValidator>`, `AddFeature<TRule, TConfig, TValidator>`, `IConfigSection`, `IHasArrayDefaults` all stay as-is.
- **Cross-options validators unchanged**: `PollingOptionsValidator` (reads other `IOptions<>`, catches `OptionsValidationException`) keeps its current shape.

## Non-goals

- Changing how options are registered or bound.
- Introducing new validators or validation rules.
- Migrating the 14 non-`List<string>` validators (deferred to when-touched).
- Modifying `ValidationAssert` in `Njord.Tests.Shared` (it operates on `ValidateOptionsResult`, unchanged).
- API budget impact: none (pure internal refactor, no polling changes).

## Capabilities

### New Capabilities
- `validation-failures-api`: Migration of validator bodies from `List<string>` to Terp's `ValidationFailures`, including the `ThresholdValidation` path-parameter adjustment.

### Modified Capabilities
- `options-validation-hygiene`: The shared `ThresholdValidation` helper's callers change how they pass the path parameter (bare name instead of section-prefixed).

## Impact

- **52 validator files** across Njord.Features (44), Njord.OpenMeteo (6), Njord.Core (2), Njord.Polling (1); 38 converted, 14 deferred.
- **`ThresholdValidation.cs`** in `Njord.Features/Alerts/Shared/`: no code change to the helper itself, but all 16 callers adjust the path argument.
- **Test files**: existing `*ValidatorTests` assert on message strings. Messages stay identical (section prefix produced by `ValidationFailures` matches the hand-written prefix). No test changes expected unless a message was subtly different.
- **Dependency**: Terp package bump in `Directory.Packages.props` (both `Terp` and `Terp.Akka` pins).
