## Why

20 feature configs have properties with invariants (thresholds, ranges, ordering) but no `IValidateOptions<T>` validator, so invalid configuration is only caught at runtime when the feature evaluates. Additionally, the validators for `ForecastOptions`, `EnsembleOptions`, and `FloodOptions` live in `Njord.OpenMeteo` instead of beside their classes in `Njord.Core`, creating an unnecessary separation.

## What Changes

- Add `IValidateOptions<T>` validators for 20 feature configs that currently lack them:
  - **Tier 4 (multi-parameter):** CoastalFlood, HeavyRain, MigraineRisk, PollenBlast, AsthmaRisk, SprayWindow, SleepQuality, SnowDepthWatch
  - **Tier 2 (scalar thresholds):** Ice, Fog, SmogRisk, Evapotranspiration, IrrigationNeed, SoilMoisture, HeatPumpCop, AllergySeverity, Laundry, OceanCurrents, FrostDepth, GrowingDegree
- Change registration from `AddFeature<TRule, TConfig>()` to `AddFeature<TRule, TConfig, TValidator>()` for all 20 features
- Move `ForecastOptionsValidator`, `EnsembleOptionsValidator`, `FloodOptionsValidator` from `Njord.OpenMeteo/Configuration/` to `Njord.Core/Configuration/` and re-register in `CoreSetupContainer`

## Non-goals

- No changes to config shapes, defaults, or section names
- No new config properties
- No changes to feature logic or results
- No API budget impact (validation is startup-only, no polling changes)

## Capabilities

### New Capabilities

- `feature-config-validation-coverage`: Ensure every feature config with properties beyond `Enabled` has a startup validator

### Modified Capabilities

- `options-validation-hygiene`: Move domain options validators to live beside their option classes
- `per-feature-config`: All feature configs with thresholds or bounds now validated on startup

## Impact

- `Njord.Features/` — 20 new validator classes, 20 registration changes in `ServiceCollectionExtensions`
- `Njord.Core/Configuration/` — receives 3 moved validator classes
- `Njord.OpenMeteo/Configuration/` — loses 3 validator classes
- `Njord/Configuration/` (Host) — setup container registration adjustments for the moved validators
