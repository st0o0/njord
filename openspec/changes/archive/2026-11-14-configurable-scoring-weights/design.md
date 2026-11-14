## Context

Nine scoring and condition features (Bbq, Outdoor, Exercise, SleepQuality, AllergySeverity, AsthmaRisk, Laundry, SprayWindow, Surf) use hardcoded numeric constants for weights, band/ramp parameters, score cutoffs and reference values. Alert features already follow a pattern where thresholds live in their Config class, but scoring features were shipped with Enabled-only configs. This change extends the existing per-feature config pattern to cover scoring parameters.

## Goals / Non-Goals

**Goals:**
- Extract all hardcoded scoring constants into named Config properties
- Add validators that enforce weight sums, band ordering and positive references
- Update features.json and the Config Builder to expose the new options
- Defaults reproduce current behavior exactly (no behavioral change)

**Non-Goals:**
- Simple threshold features (Commute, Ventilation, etc.) are a separate change
- Gap fills in partially-configured alerts are a separate change
- No new features, no API budget impact, no proto/ha-njord changes

## Decisions

### D1: Named properties over arrays for weights

Component weights are individual `double` properties (`TemperatureWeight`, `WindWeight`, etc.) rather than a `double[] Weights` array.

**Rationale:** Weights are heterogeneous named components, not ordered severity tiers. Named properties are self-documenting, individually overridable via environment variables (`Njord__Features__Bbq__TemperatureWeight=0.4`), and impossible to get the index wrong. The existing `IHasArrayDefaults` pattern is for ascending threshold arrays.

**Alternative considered:** Array with index convention. Rejected: order-dependent, unnamed, poor env-var ergonomics.

### D2: Validator per feature with weight-sum check

Each feature gets its own `<Name>ConfigValidator : IValidateOptions<T>` registered via `AddFeature<TRule, TConfig, TValidator>()`. Weight validators check `Math.Abs(sum - 1.0) < 0.001`. Band validators check strict ordering (low < ideal < warm < hot). Reference values must be positive.

**Rationale:** Follows the existing pattern (FrostConfigValidator, SprayWindowConfigValidator). Per-feature validators are simple and self-contained. The epsilon handles floating-point rounding from config binding.

### D3: SurfConditionsRule accepts config as parameter

`SurfConditionsRule.Evaluate` and `SurfConditionsRule.Assess` gain a `SurfConditionsConfig` parameter instead of reading public constants. The `SurfConditions` feature passes `config.Value` through.

**Rationale:** The rule is a static helper shared between the feature and tests. Passing config avoids a static mutable state pattern and keeps the rule pure-functional.

**Alternative considered:** Making the rule instance-based with DI. Rejected: the rule is intentionally static and shared; adding a parameter is less invasive.

### D4: Config Builder groups via `group` field in features.json

Each option in `features.json` gains an optional `"group": "Weights"` (or "Scoring", "Thresholds") field. The builder renders grouped options under collapsible section headers. Weight groups show a live sum indicator.

**Rationale:** Features like Bbq and Outdoor have 15+ options. Without grouping, the UI becomes overwhelming. The group field is pure UI metadata with no backend impact.

### D5: Existing Config properties preserved as-is

Features that already expose some properties (SleepQuality: PollenSensitive/NightStart/NightEnd/Window*, AsthmaRisk: weights/Window*, SprayWindow: thresholds/Window*, Laundry/AllergySeverity: Window*) keep those properties unchanged. New properties are added alongside them.

**Rationale:** No breaking change for existing configurations.

## Risks / Trade-offs

- **Config surface area**: SleepQualityConfig goes from 7 to ~30 properties. Most users will never touch them. **Mitigation**: Config Builder groups, sane defaults, only non-default values exported.
- **Floating-point weight sums**: Users entering weights in JSON/env-vars may hit rounding. **Mitigation**: 0.001 epsilon in validator; Config Builder normalizes.
- **SurfConditionsRule API change**: Tests that call the static methods directly need updating. **Mitigation**: Mechanical change, pass `new SurfConditionsConfig()` for defaults.

## Open Questions

None remaining.
