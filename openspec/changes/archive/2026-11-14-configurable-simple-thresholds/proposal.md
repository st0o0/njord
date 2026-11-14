## Why

Eight features have hardcoded numeric constants (thresholds, limits, window sizes) that operators cannot tune without modifying source code. These are subjective or environment-dependent values (e.g., what counts as "heavy rain" for commute safety, or "cool enough" for night ventilation) that should be operator-configurable like the existing alert thresholds. This is Change 1 of 3: it covers the simple scalar-threshold features; Changes 2 and 3 will cover scoring/weight features and gap-fills in partially-configured features.

## What Changes

- Extract ~30 hardcoded constants from 8 feature rules into their existing `*Config` classes as named properties with current values as defaults
- Promote 6 features from Tier 1 (Enabled-only) to Tier 2 (scalar thresholds): CommuteSafety, VentilationWindow, NightVentilation, Gardening, PollenTrend, ConfidenceTrend
- Promote 2 features from Tier 1 to Tier 3 (with validators): ApproachingFront (WindowHours >= 1), SwimmingConditions (WindowHours >= 1, wave/temp ordering)
- Add validators for SwimmingConditions and ApproachingFront configs
- Update `features.json` with the new options and their defaults for each affected feature
- Update the Config Builder to display and validate the new options

## Non-goals

- Scoring features with component weights (Bbq, Outdoor, Exercise, etc.) - deferred to Change 2
- Filling gaps in already-partially-configured features (SleepQuality, AsthmaRisk, etc.) - deferred to Change 3
- Scientific/meteorological classification constants (AtmosphericStability, CloudCeiling, InversionDetection) - intentionally kept as code constants
- No API budget impact: this change does not alter polling, domains, or request structure

## Capabilities

### New Capabilities

- `simple-threshold-configs`: Extraction of hardcoded constants into per-feature config properties for 8 features (CommuteSafety, VentilationWindow, NightVentilation, Gardening, ApproachingFront, SwimmingConditions, PollenTrend, ConfidenceTrend)

### Modified Capabilities

- `per-feature-config`: Features promoted from Tier 1 to Tier 2/3 with new config properties and validators
- `config-builder`: New options displayed and validated in the interactive builder

## Impact

- **Config classes**: 8 files in `src/Njord.Features/` gain new properties
- **Feature rules**: 8 rule files read from config instead of inline constants
- **Validators**: 2 new validators (SwimmingConditionsConfigValidator, ApproachingFrontConfigValidator)
- **Registration**: 2 features switch from `AddFeature<TRule, TConfig>()` to `AddFeature<TRule, TConfig, TValidator>()`
- **features.json**: 8 entries updated with options arrays
- **Config Builder**: `types.ts` and `ConfigBuilder.vue` updated to render new option types
- **Tests**: New config/validator tests for each affected feature; existing rule tests updated to verify config is read
