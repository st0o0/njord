## Context

Njord has 52 `IValidateOptions<T>` validators. 38 use the `List<string>` accumulation pattern, 14 use single-ternary or early-return. Terp is adding `ValidationFailures` - a builder that replaces the `List<string>` + ternary boilerplate with `Require()` / `Add()` / `ToResult()`.

Current Terp version is 0.2.1. `ValidationFailures` is not shipped yet. This change is blocked on a Terp release that includes it.

Validator distribution by project:

| Project | Count | List pattern | Other |
|---------|-------|-------------|-------|
| Njord.Features/Alerts | 30 | 22 | 8 |
| Njord.Features/Conditions | 8 | 7 | 1 |
| Njord.Features/Scores | 5 | 4 | 1 |
| Njord.Features/Forecasts | 1 | 1 | 0 |
| Njord.OpenMeteo | 6 | 4 | 2 |
| Njord.Core | 2 | 0 | 2 |
| Njord.Polling | 1 | 0 | 1 |

16 alert validators call `ThresholdValidation.Violation` and need coordinated path-argument changes.

## Goals / Non-Goals

**Goals:**
- Replace `List<string>` boilerplate with `ValidationFailures` in all 38 validators that use the pattern.
- Keep all error messages identical (existing test assertions pass without change).
- Coordinate `ThresholdValidation` callers so section prefixes appear exactly once.

**Non-Goals:**
- Changing the 14 single-ternary/early-return validators (deferred to when-touched).
- Modifying `AddNjordOptions`, `IConfigSection`, `IHasArrayDefaults`, or cross-options validators.
- Changing `ValidationAssert` in test shared (it operates on `ValidateOptionsResult`, unchanged).
- Changing `ThresholdValidation` itself (only call sites change).

## Decisions

### D1: Section prefix strategy - match existing behavior exactly

Feature config validators currently build `IConfigSection.SectionOf<T>()` and manually prefix each message with `$"{section}:"`. `ValidationFailures` can take a section in its constructor and auto-prefix.

**Decision**: Pass the section to the `ValidationFailures` constructor, drop the manual prefix from each message. This produces identical output.

**Alternative considered**: No section in constructor, keep manual prefixes. Rejected because it keeps the boilerplate the migration aims to remove.

### D2: ThresholdValidation - change call sites, not the helper

`ThresholdValidation.Violation(path, values, direction)` builds its message from the `path` argument. Today callers pass `$"{section}:Thresholds"`. With `ValidationFailures` adding the section, callers must pass `"Thresholds"` (bare).

**Decision**: Change only the call sites (16 validators). `ThresholdValidation` stays untouched - it's a pure string-in/string-out helper that doesn't know about sections.

**Alternative considered**: Make `ThresholdValidation` section-aware. Rejected because it couples a general helper to `ValidationFailures` semantics.

### D3: Conversion order - pilot, then by folder

**Decision**: Convert `HeatConfigValidator` first as a pilot (includes `ThresholdValidation` + `WindowHours` - representative of the most common shape). Then Alerts folder (all 22 `List<string>` validators including the 16 `ThresholdValidation` callers in one commit), then Conditions, Scores, Forecasts, then OpenMeteo/Polling/Core. One commit per folder/project.

**Rationale**: Alerts is the largest and most uniform group. Converting them together ensures `ThresholdValidation` path changes are atomic. Smaller groups follow naturally.

### D4: Registration stays in AddNjordOptions

`AddNjordOptions<T, TValidator>` currently registers the validator as `IValidateOptions<T>`. This works with `ValidationFailures` because the return type is still `ValidateOptionsResult`.

**Decision**: No change to registration. The cosmetic internal switch to `AddOptionsWithValidateOnStart` (mentioned in Migration.md) is not worth the churn for no behavioral change.

## Risks / Trade-offs

- **[Blocked on Terp]** Cannot start until Terp ships `ValidationFailures`. No mitigation needed - the change waits. -> Track Terp releases.
- **[Message drift]** If `ValidationFailures` formats the section prefix differently than the hand-written `$"{section}:"` pattern, messages will differ and tests will fail. -> The pilot conversion of `HeatConfigValidator` catches this immediately.
- **[52-file touch]** Large diff across many files, all mechanical. Risk of merge conflicts if other changes touch validators concurrently. -> Folder-per-commit limits conflict scope. Convert in a quiet window.

## Open Questions

- What is the Terp release timeline for `ValidationFailures`? The change is ready to execute as soon as it ships.
