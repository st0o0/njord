## Context

Eight features in `Njord.Features` use hardcoded numeric constants for thresholds, limits, and window sizes. Their config classes currently contain only `Enabled`. The established pattern for configurable features (Tier 2-3) is well-proven across ~30 alert features: named properties with defaults as initializers, `IOptions<T>` injection, optional validators via `IValidateOptions<T>`.

## Goals / Non-Goals

**Goals:**
- Extract ~30 hardcoded constants into config properties with identical defaults
- Zero behavior change for operators who do not set custom values
- Validators for features with WindowHours or ordered threshold pairs
- Updated features.json and Config Builder for all new options

**Non-Goals:**
- Scoring/weight features (Change 2)
- Gap-fills in partially-configured features (Change 3)
- Changing any feature's logic or default behavior
- Hot reload of config values (remains IOptions, not IOptionsMonitor)

## Decisions

### Config property naming: match the domain concept, include units
Properties use descriptive names with unit suffixes where ambiguous: `IceTemperatureCelsius`, `MaxPrecipitationMmH`, `WindLimitMs`, `PressureDropThresholdHpa`. This matches the existing pattern (e.g., `StrongCurrentThresholdKmh` in OceanCurrentsConfig). Plain `WindowHours` has no suffix (hours is in the name).

**Alternative**: Short names without units (e.g., `IceTemp`). Rejected because config files are read by operators who may not know the codebase.

### Tier promotion: 6 features to Tier 2, 2 features to Tier 2 with validators
Features with only scalar thresholds and no ordering constraints stay at Tier 2 (no validator needed): CommuteSafety, VentilationWindow, NightVentilation, Gardening, PollenTrend, ConfidenceTrend. Features with WindowHours or ordered pairs get validators and move to Tier 2 with validation: ApproachingFront, SwimmingConditions.

**Alternative**: Add validators to all 8 features. Rejected because Tier 2 features with unconstrained scalars have no invariants to validate; a validator that only checks nothing is noise.

### Registration change: AddFeature 2-param to 3-param
ApproachingFront and SwimmingConditions switch from `AddFeature<TRule, TConfig>()` to `AddFeature<TRule, TConfig, TValidator>()` in `ServiceCollectionExtensions.AddFeatures`. Other 6 features stay with the 2-param call.

### features.json: options array per feature
Each affected feature's entry in `docs/data/features.json` gains an `"options"` array with objects: `{ "name": "<PropertyName>", "type": "number", "default": <value>, "description": "<text>" }`. The Config Builder reads this array to render option inputs.

### Config Builder: option rendering
New options appear as numeric inputs under each feature's toggle in the builder. Validation rules (WindowHours >= 1, ordered pairs) are enforced client-side. Options at their default value are excluded from the exported config to keep exports minimal. Features with 5+ options get a collapsible "Thresholds" group.

## Risks / Trade-offs

- [Operator confusion from too many options] -> Defaults match current behavior; options are hidden until the feature is expanded in the builder; features.json descriptions explain each option's purpose.
- [Config Builder divergence from runtime validation] -> Builder validation mirrors the validator logic (WindowHours >= 1, ordered pairs). Both are simple enough that divergence is unlikely.

## Open Questions

None - the pattern is established and the scope is well-defined.
