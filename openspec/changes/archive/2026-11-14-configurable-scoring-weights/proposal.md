## Why

Nine scoring and condition features use hardcoded numeric constants for weights, band/ramp parameters, score cutoffs and reference values. Operators cannot tune these to match their local climate, personal comfort, or agricultural needs without rebuilding from source. Extracting them into the existing per-feature Config classes makes the scoring system as configurable as the alert thresholds already are.

## What Changes

- **Bbq, Outdoor, Exercise, Laundry**: extract component weights, band/ramp parameters, score cutoffs and rain thresholds into named Config properties with a validator that checks weights sum to 1.0.
- **SleepQuality**: extract ~12 penalty constants (temperature thresholds, humidity, PM2.5, pollen, gust, CAPE limits and penalty point values) and PreSleepHours into the existing Config.
- **AllergySeverity**: extract PollenReference, GoodHourScore and the five weather modifier values into the existing Config.
- **AsthmaRisk**: extract four reference values (Pm25, Ozone, Pollen, GoodHourScore), ColdAirFactor, RapidChangeFactor/Degrees/Hours into the existing Config (weights are already exposed).
- **SprayWindow**: extract RainProbabilityLimit, HumidityMin, HumidityMax, HumidityFalloff and TemperatureFalloff into the existing Config.
- **SurfConditions**: extract period thresholds, height ranges, offshore tolerance, choppy shares, score cap and window hours into SurfConditionsConfig; the shared SurfConditionsRule reads them from the config passed through.
- **features.json**: add the new options to each feature entry so the Config Builder can render them.
- **Config Builder**: group complex features into sections (Scoring, Thresholds, Window); validate weight sums client-side.

## Non-goals

- Tier D scientific constants (AtmosphericStability, CloudCeiling, InversionDetection) are not in scope; they use meteorological classification standards.
- Simple-threshold features (Commute, Ventilation, NightVentilation, Gardening, ApproachingFront, PollenTrend, ConfidenceTrend, Swimming) are handled by a separate change (configurable-simple-thresholds).
- Gap fills in partially-configured alerts (CompoundHeatStress, SmogRisk, CoastalFlood, PollenBlast, FireRisk, MigraineRisk, FrostDepth, SolarYield, IrrigationNeed, HeatPumpCop) are handled by a third change.
- No API budget impact: this change only restructures how existing constants are stored, no new polling.

## Capabilities

### New Capabilities
- `scoring-config-weights`: Config properties and validators for scoring/condition features with component weights, band/ramp parameters and reference values.

### Modified Capabilities
- `per-feature-config`: New requirement that multi-parameter scoring configs expose component weights as named properties with a sum-to-1.0 validator, and band/ramp parameters as named properties.
- `config-builder`: New requirement that the builder groups complex feature options into sections and validates weight sums client-side.
- `feature-config-validation-coverage`: New requirement that all nine features have validators covering the newly exposed properties.

## Impact

- **Njord.Features**: 9 Config classes gain properties; 9 FeatureRule classes read from config instead of constants. SurfConditionsRule gains a config parameter.
- **Njord.Features.Tests**: new validator tests, updated scoring tests to verify config-driven behavior.
- **docs/data/features.json**: 9 entries gain options arrays.
- **docs/.vitepress/theme/builder**: types.ts, ConfigBuilder.vue gain section grouping and weight-sum validation.
- **No breaking change**: defaults reproduce current behavior exactly.
