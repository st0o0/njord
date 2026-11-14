## Why

Ten features have partially configurable Config classes but still contain hardcoded constants in their rule logic. Operators cannot tune these values without rebuilding the service. Extracting them into the existing Config classes completes the "every tunable is an option" principle established by `per-feature-config`.

## What Changes

- **CompoundHeatStress**: expose UV/PM2.5 combination thresholds and apparent-temperature margin as config properties
- **SmogRisk**: expose severity-tier boundary-layer/PM2.5 combination thresholds
- **CoastalFlood**: expose severity-tier sea-level thresholds
- **PollenBlast**: expose severity-tier pollen thresholds
- **FireRisk**: expose cold-cutoff and humid-cutoff gate thresholds
- **MigraineRisk**: expose Foehn wind-direction range and lookback hours
- **FrostDepth**: expose Stefan alpha coefficient, pipe-approach fraction, and risk-depth thresholds
- **SolarYield**: expose sunnier/cloudier comparison factors
- **IrrigationNeed**: expose heavy-rain and high-probability thresholds
- **HeatPumpCop**: expose COP formula coefficients and recommendation thresholds
- **features.json**: add option metadata for all new properties
- **Config Builder**: render new options with appropriate input types and grouping

## Non-goals

- Features with Enabled-only configs that need entirely new Config classes (covered by Change 1: configurable-simple-thresholds)
- Score/weight features needing weight-sum validation (covered by Change 2: configurable-scoring-weights)
- Scientific classification constants (AtmosphericStability, CloudCeiling, InversionDetection) - these are meteorological definitions, not operator tunables
- Observations and pass-through features (Weather, ForecastModelComparison, etc.) - no meaningful thresholds

## Capabilities

### New Capabilities

- `config-gap-fills`: extraction of ~30 hardcoded constants from 10 feature rules into their existing Config classes, with validators and documentation

### Modified Capabilities

- `per-feature-config`: extends the requirement that alert features include thresholds to also cover condition, score, and forecast features that have tunable constants
- `config-builder`: adds rendering for the new config properties in the builder UI and features.json

## Impact

- **Code**: 10 `*Config.cs` files gain new properties; 10 `*Rule.cs` files read from config instead of inline constants; up to 6 existing validators extended
- **Config Builder**: `docs/data/features.json` gains option entries; `docs/.vitepress/theme/builder/` components may need grouping for features with many options
- **Tests**: existing config/rule tests updated to cover new properties; new validator tests where validators are added
- **No API budget impact**: no polling changes, no new domains, no proto changes
