## Context

njord already has 6 cross-domain input types and features like SmogRisk (Forecast + AirQuality) and CoastalFlood (Marine + Forecast + Ensemble). The pattern is proven: declare multi-domain `DomainRequirements`, use a typed input struct, return `null` from `Project` when not all domains are populated yet.

Both new features use Forecast + AirQuality data (`ForecastAqInput`), which several existing features (SmogRisk, PollenBlast, Exercise) already use.

## Goals / Non-Goals

**Goals:**
- CompoundHeatStress alert combining WetBulb/UV/Heat with PM2.5 and BoundaryLayer
- AllergySeverity score combining pollen species with atmospheric modifiers
- Both features follow the established `FeatureRule<ForecastAqInput>` pattern
- Proto messages with enums for structured i18n in ha-njord

**Non-Goals:**
- Extending or restructuring `ForecastAqInput` (not needed, see below)
- Replacing WetBulbDanger or Pollen alerts (coexistence)
- Window/calendar support (separate change: score-windows)

## Decisions

### 1. Reuse ForecastAqInput as-is, no extension needed

`ForecastAqInput(DomainForecast<ForecastPoint>, DomainForecast<AirQualityPoint>)` already holds the full `ForecastPoint` and `AirQualityPoint`. `BoundaryLayerHeight` and `WetBulbTemperature` are fields on `ForecastPoint` — they're available if the feature requests them via `DomainRequirements`.

SmogRisk already uses `ForecastParams.BoundaryLayerHeight` in its requirements while sharing `ForecastAqInput` with other features. Same pattern for CompoundHeatStress.

**Why not a new input type:** Adding `ForecastAqExtendedInput` would duplicate the struct for no structural benefit. The gating is in `DomainRequirements`, not the input type.

### 2. DominantStressor as a C# enum mapped to Proto enum

`DominantStressor` identifies which of the three trigger paths fired. It's a closed set — perfect for an enum. The C# enum lives in `Njord.Messages.Alerts`, the Proto enum in `weather.proto`. Mapping via explicit switch in `AlertResultMappingExtensions`.

### 3. AllergyModifier as flags via ImmutableArray, not [Flags] enum

Multiple modifiers can be active simultaneously (wind dispersal + low humidity). Proto doesn't support bitwise enums well. Use `ImmutableArray<AllergyModifier>` in the C# result and `repeated AllergyModifier` in Proto, matching the existing `ImmutableArray<CommuteHazard>` pattern in `CommuteSafetyResult`.

### 4. AllergySeverity uses ScoreFeatureResult with BestStart/BestHours

The score represents current allergy severity (0-100). `BestStart`/`BestHours` point to the lowest-pollen window in the forecast. This uses the existing `ScoreFeatureResult` base without any modifications.

### 5. Proto field numbers: next available alert slot 48, score slot 54

Alerts use 20-47 (CoastalFlood is 47). CompoundHeatStress gets 48.
Scores use 50-53 (SurfConditions is 53). AllergySeverity gets 54.

## Risks / Trade-offs

- **BoundaryLayerHeight may be null in some model outputs**: Features treat it as an amplifier; if absent, the base logic still works without the amplification. SmogRisk already handles this.
- **Pollen thresholds vary by region**: Default scaling factors use EAN thresholds. Config weights let users adjust per species.
- **Compound thresholds are not well-studied**: Conservative defaults, user-configurable, labeled experimental in docs.
