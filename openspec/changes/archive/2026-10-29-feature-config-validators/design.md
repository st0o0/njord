## Context

20 feature configs have properties with invariants but no `IValidateOptions<T>` validator. Invalid values are only caught at runtime during feature evaluation, not at startup. The existing validators (e.g. `StormConfigValidator`, `FrostConfigValidator`) demonstrate the established pattern.

Additionally, three domain options validators (`ForecastOptionsValidator`, `EnsembleOptionsValidator`, `FloodOptionsValidator`) live in `Njord.OpenMeteo` while their option classes live in `Njord.Core`. This is functionally correct (OpenMeteo references Core) but inconsistent with the other domain option validators (AirQuality, Marine, Seasonal, Satellite) which are co-located with their option classes.

## Goals / Non-Goals

**Goals:**
- Every feature config with properties beyond `Enabled` has a startup validator
- Validators co-located with the classes they validate
- Consistent validation patterns across all features

**Non-Goals:**
- No changes to config shapes, defaults, or feature logic
- No new config properties
- No validation of Tier-1 (Enabled-only) configs

## Decisions

### Validator pattern follows existing template

All 20 new validators follow the same pattern as `StormConfigValidator`:

```csharp
public sealed class XxxConfigValidator : IValidateOptions<XxxConfig>
{
    public ValidateOptionsResult Validate(string? name, XxxConfig options)
    {
        var section = IConfigSection.SectionOf<XxxConfig>();
        var failures = new List<string>();
        // property checks, each adding to failures
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
```

**Why:** Consistency with existing validators. The `IConfigSection.SectionOf<T>()` prefix gives clear error messages. Collecting all failures before returning shows all issues at once.

### ThresholdValidation reused for array thresholds

Configs with `double[]` thresholds and `IHasArrayDefaults` (like `SnowDepthWatchConfig`) reuse `ThresholdValidation.Violation()` from `Njord.Features/Alerts/Shared/` where it fits the ascending-order + correct-count pattern.

**Why:** Avoids duplicating the ascending-order check. The helper already handles both `ScanDirection.Max` (ascending) and `ScanDirection.Min` (descending).

### Window parameter validation is inline

Configs with `MinWindowScore`, `MinWindowHours`, `MaxWindows` validate each property >= 1 inline rather than through a shared helper.

**Why:** Three simple `>= 1` checks don't warrant extraction. If more configs adopt WindowScanner in the future, a shared method can be introduced then.

### Registration changes from 2-param to 3-param

In `ServiceCollectionExtensions.AddFeatures()`, each of the 20 features changes from `AddFeature<TRule, TConfig>()` to `AddFeature<TRule, TConfig, TValidator>()`.

**Why:** This is how validators are registered for features per the existing pattern. The 3-param overload registers both the options binding and the validator.

### Domain options validators move to Core

`ForecastOptionsValidator`, `EnsembleOptionsValidator`, `FloodOptionsValidator` move from `Njord.OpenMeteo/Configuration/` to `Njord.Core/Configuration/`. Registration moves from `OpenMeteoSetupContainer` to `CoreSetupContainer` (using `AddNjordOptions<T, TValidator>()`).

**Why:** The option classes are in Core because multiple sibling projects consume them (Polling, Grpc). The validators should live beside the classes they validate, consistent with how AirQuality/Marine/Seasonal/Satellite options and validators are co-located in OpenMeteo.

**Alternative considered:** Leave them in OpenMeteo. Works fine, but breaks the pattern. Moving is low-risk since no logic changes.

## Risks / Trade-offs

- [Validators reject previously accepted configs] -> Defaults are validated first; only custom configs with genuinely invalid values are rejected. This is the intended behavior — catching errors early.
- [High number of files changed] -> Each validator is a small, self-contained file. The pattern is mechanical and well-tested by the existing validators.
