## Context

Ten features already have Config classes with some properties but still use hardcoded constants in their rule logic. This change extracts those constants into the existing configs following the established Tier 2/4 patterns.

## Goals / Non-Goals

**Goals:**
- Extract all hardcoded tunable constants from the 10 features into their Config classes
- Extend existing validators where they exist
- Add validators where cross-field invariants need enforcement (severity ordering, range constraints)
- Update `features.json` and Config Builder to reflect the new options

**Non-Goals:**
- Features that need entirely new Config classes (Change 1: configurable-simple-thresholds)
- Score/weight features needing weight-sum validation (Change 2: configurable-scoring-weights)
- Scientific classification constants (AtmosphericStability, CloudCeiling, InversionDetection)

## Decisions

### Config property naming follows the constant name
New properties use the same name as the existing constant (e.g. `ColdCutoff`, `StefanAlpha`, `FoehnDirectionMin`). This preserves grep-ability and makes the extraction mechanical. Alternative: rename to match a naming convention like `XxxThreshold` everywhere. Rejected because consistency with existing code is more valuable than uniform suffixes.

### Severity-tier thresholds as named scalar properties
SmogRisk, CoastalFlood, and PollenBlast severity tiers become named properties like `RedBoundaryLayerM`, `OrangePm25Ugm3` rather than arrays. This matches the pattern used by FireRisk (`RedThreshold`, `OrangeThreshold`, `YellowThreshold`) and is self-documenting. Alternative: severity arrays with `IHasArrayDefaults`. Rejected because the thresholds are heterogeneous (SmogRisk combines boundary layer + PM2.5 per tier).

### COP formula coefficients exposed directly
HeatPumpCop exposes `CopIntercept` and `CopSlope` as config properties. The linear model `COP = max(1.0, intercept - slope * deltaT)` is simple enough that two coefficients are sufficient. Alternative: a lookup table. Rejected as over-engineered for a single linear formula.

### FireRisk.WeatherScore becomes instance method
`WeatherScore` is currently `internal static` taking a `FireRiskConfig` parameter. After the change it reads `ColdCutoff` and `HumidCutoff` from the same config. The method signature changes from `WeatherScore(double, double, double, FireRiskConfig)` to reading from the injected config directly, or the two new properties are added to the existing config parameter. Prefer keeping it static with the config parameter for testability.

### FrostDepth keeps public static methods with explicit parameters
`DepthCm` and `RiskFor` are `public static` and used in tests directly. They gain parameters for the newly configurable values (alpha, pipe-approach fraction, thresholds). This keeps them pure and testable without config injection.

### Validators extended, not replaced
All 10 features already have validators or the config is simple enough not to need one. New validation rules are added to existing validators. Features gaining their first cross-field rules (PollenBlast severity ordering, FrostDepth ascending thresholds) get validator classes registered via `AddFeature<TRule, TConfig, TValidator>`.

## Risks / Trade-offs

- [Risk: Breaking existing overrides] Adding new properties with defaults matching current behavior means existing configs that only set `Enabled` continue to work unchanged. No migration needed. Mitigation: defaults are the current hardcoded values, verified by tests.
- [Risk: Config Builder complexity] Some features (MigraineRisk, FrostDepth, HeatPumpCop) gain 5+ new options. Mitigation: group options in features.json with a `group` field that the builder renders as collapsible sections.
- [Risk: FrostDepth static method signature changes] External callers of `DepthCm` and `RiskFor` need to pass additional parameters. Mitigation: these are only called in tests and the rule itself; the change is mechanical.

## Open Questions

None.
