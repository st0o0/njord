## Context

njord has 7 unused Forecast parameters for upper-atmosphere data: Temperature80m, Temperature120m, ConvectiveInhibition, LiftedIndex, CloudCoverLow, CloudCoverMid, CloudCoverHigh. These are available from Open-Meteo but no feature requests them. The existing Thunderstorm alert uses CAPE (and optionally CIN/LiftedIndex for severity modulation) but does not expose the stability profile as a user-facing feature.

All three features are Forecast-only Conditions using `ForecastInput`. They follow the established pattern: `FeatureRule<ForecastInput>` with `OutputKind.Condition`, a `<Name>Config : IConfigSection`, registration in `AddFeatures`, a result record extending `ConditionFeatureResult`, a proto message, and a gRPC mapping extension.

## Goals / Non-Goals

**Goals:**
- Make Temperature80m/120m productive via InversionDetection
- Make CIN/LiftedIndex productive via AtmosphericStability (complementing Thunderstorm's optional use)
- Make CloudCoverLow/Mid/High productive via CloudCeiling
- All three off by default (Insight category)
- Follow established feature patterns exactly (ElevatedWind is the closest reference — Forecast-only Condition)

**Non-Goals:**
- Aviation-grade ceiling reporting; radar/METAR integration
- Modifying the Thunderstorm alert (it already optionally uses CIN/LI)
- New input types — `ForecastInput` suffices since all data comes from Forecast domain

## Decisions

### 1. Use ForecastInput (not a new input type)

All three features need only Forecast domain data. `ForecastInput(DomainForecast<ForecastPoint> Forecast)` already exists and is used by features like ElevatedWind. Each feature declares its specific `ForecastParams` flags in `DomainRequirements` — no need for a dedicated struct.

**Why:** Adding a new input struct per feature creates unnecessary types. The params flags already express what each feature needs. Thunderstorm already requests CIN/LI as optional — AtmosphericStability requests them as required, and `FeatureResolution` OR-merges the two.

### 2. InversionDetection: dual classification (Strength + Type)

The feature reports both a strength enum (NONE through EXTREME — the primary, reliable output) and a best-effort type classification (RADIATION, SUBSIDENCE, FRONTAL). Strength drives the sensor state; type is an informational attribute.

**Why:** Strength is physically grounded (temperature difference + boundary layer height). Type classification is heuristic and can be wrong (e.g., a radiation inversion might be misclassified as subsidence during a persistent high-pressure system). Making type informational avoids false confidence.

### 3. InversionDetection PollDepth = 3

Three history entries (3h at 60min poll) give enough data to check pressure trends for SUBSIDENCE/FRONTAL classification and confirm persistence. RADIATION inversions need only the current data point.

**Why:** Minimal depth that enables the heuristic type classification without excessive memory use.

### 4. AtmosphericStability: five-tier StabilityClass

STABLE → CAPPED → MARGINAL → UNSTABLE → EXPLOSIVE. The CAPPED class is the key differentiator from a simple CAPE reading: high CAPE with strong CIN means "energy is present but suppressed" — a different message from "unstable."

**Why:** The CAPPED state has direct user value: "loaded but lidded" means storms are possible if the lid breaks (e.g., afternoon heating or a front). No other njord feature expresses this nuance.

### 5. CloudCeiling: use BoundaryLayerHeight for low-cloud refinement

Within the low-cloud layer, BoundaryLayerHeight (already a Forecast param used by other features) refines the ceiling estimate. This avoids needing a separate temperature-profile-to-LCL calculation.

**Why:** Open-Meteo's BoundaryLayerHeight is a direct model output that correlates well with cloud base in boundary-layer cloud regimes. It's simpler and more reliable than computing the lifting condensation level from surface temperature and humidity.

### 6. Coexistence with Thunderstorm alert

AtmosphericStability and Thunderstorm coexist. Thunderstorm is an Alert that fires when convection is imminent (CAPE + active precipitation + gusts above thresholds). AtmosphericStability is a Condition that describes the atmospheric profile — it can show EXPLOSIVE hours before any precipitation materializes. Different questions, different OutputKinds, no overlap.

CIN and LiftedIndex are `OptionalForecast` in Thunderstorm but `Required` Forecast in AtmosphericStability. `FeatureResolution` handles this correctly: the merged requirements include the required flags.

### 7. Proto field numbers 76/77/78 in Conditions range

The highest condition field number is 75 (elevated_wind). The three new features take 76 (InversionDetection), 77 (AtmosphericStability), 78 (CloudCeiling).

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Temperature80m/120m nullability | `Project` returns null when these aren't present; feature skips silently |
| CIN/LI null in some models | Same: null-check in Evaluate, return null to skip |
| Cloud ceiling is an estimate, not a measurement | CeilingCategory enum is categorical (VERY_LOW through UNLIMITED); `estimated_ceiling_m` is labeled "estimated" in the entity name |
| 7 additional Forecast params may add 1 call/cycle | 0.7% of free-tier budget for 3 locations — BudgetGuard accounts for it automatically |
