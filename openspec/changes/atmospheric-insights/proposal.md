# Atmospheric Insights

## Summary

Add three Condition features that make currently unused high-altitude and atmospheric stability data from the Forecast domain productive: **InversionDetection** (temperature inversions from 80m/120m profiles), **AtmosphericStability** (convection potential from CAPE/CIN/LiftedIndex), and **CloudCeiling** (estimated cloud base from per-layer cloud cover). All three are Forecast-only, require no new domains, and unlock 7 API variables that are available but currently requested by no feature.

## Motivation

Open-Meteo provides rich upper-atmosphere data that njord already has the infrastructure to request but no feature consumes:

- **Temperature80m, Temperature120m**: vertical temperature profile, essential for detecting inversions that trap pollution and fog
- **ConvectiveInhibition (CIN), LiftedIndex**: together with CAPE (already used by Thunderstorm), these give the full atmospheric stability picture -- not just "is there energy?" but also "is there a lid?" and "how easily can convection start?"
- **CloudCoverLow, CloudCoverMid, CloudCoverHigh**: per-layer cloud cover, far more informative than the single total CloudCover value currently used

These are high-value signals for air quality understanding (inversions trap pollutants), severe weather anticipation (stability profile), and outdoor activity planning (cloud ceiling for drone pilots, photographers, aviators). The existing Thunderstorm alert uses only CAPE + precipitation + gusts for imminent storms; AtmosphericStability gives the convection potential profile hours to days ahead.

## Scope

### njord

- Three new `FeatureRule<TInput>` implementations in `Njord.Features/Conditions/`
- Three new `<Name>Config : IConfigSection` classes (all off by default)
- New `FeatureInputs` struct(s) for the required Forecast fields
- DomainRequirements declaring the new ForecastParams flags
- Proto messages and enums in `weather.proto`
- gRPC mapping in `Njord.Grpc/Mapping/`
- Registration in `AddFeatures`
- `docs/data/features.json` entries

### ha-njord

- Entity descriptions for three new Condition sensors
- Translation entries in `strings.json` and `translations/de.json`

## Design

### InversionDetection

**Output:** Condition
**Trigger:** Forecast domain
**Required ForecastParams:** Temperature, Temperature80m, Temperature120m, BoundaryLayerHeight, Visibility, CloudCover, WindSpeed, IsDay, SurfacePressure

**Logic:**

A temperature inversion occurs when air temperature increases with altitude instead of the normal lapse rate (~-6.5 deg C/km). The feature compares surface temperature against 80m and 120m readings.

Detection:
- Inversion present when `Temperature80m > Temperature` OR `Temperature120m > Temperature80m`
- Temperature difference = `max(Temperature80m - Temperature, Temperature120m - Temperature80m)`

Strength classification (requires both temperature difference AND boundary layer confirmation):

| Strength | Temperature difference | BoundaryLayerHeight |
|----------|----------------------|---------------------|
| NONE     | <= 0 deg C           | any                 |
| MILD     | > 0 deg C            | < 800m              |
| STRONG   | > 2 deg C            | < 500m              |
| EXTREME  | > 4 deg C            | < 300m              |

Type classification (best-effort, based on available surface data):

| Type         | Conditions                                           |
|--------------|------------------------------------------------------|
| RADIATION    | !IsDay AND CloudCover < 30% AND WindSpeed < 3 m/s   |
| SUBSIDENCE   | SurfacePressure > 1020 hPa AND inversion persistent  |
| FRONTAL      | Rapid pressure change (from ring buffer history)     |

The type is informational, not a hard classification. RADIATION is the most common and most reliably detectable. PollDepth of 3 gives 3h of history for persistence and pressure-change checks.

**Dissolution estimate:** For RADIATION inversions, hours until next IsDay transition from the forecast hourly data. For others, hours until BoundaryLayerHeight exceeds 1000m.

**Config:** `InversionConfig` -- `Enabled` (default false). Thresholds are physically grounded and not configurable; the strength tiers follow standard meteorological definitions.

### AtmosphericStability

**Output:** Condition
**Trigger:** Forecast domain
**Required ForecastParams:** Cape, ConvectiveInhibition, LiftedIndex

**Logic:**

Three independent indices are evaluated and combined into a single stability classification:

CAPE (Convective Available Potential Energy):
- < 300 J/kg: Stable
- 300-1000: Marginal
- 1000-2500: Moderate
- \> 2500: Strong

CIN (Convective Inhibition, the "lid"):
- \> 200 J/kg: Strong lid -- convection suppressed even with high CAPE
- 50-200: Moderate lid
- < 50: No lid -- if CAPE is high, convection initiates freely

Lifted Index:
- \> 0: Stable
- 0 to -3: Marginal
- -3 to -6: Very unstable
- < -6: Extreme

Combined StabilityClass:

| Class     | Criteria                                        | Meaning                                    |
|-----------|-------------------------------------------------|--------------------------------------------|
| STABLE    | CAPE < 300                                      | No convective energy                       |
| CAPPED    | CAPE >= 1000 AND CIN > 200                     | Energy present but lid prevents release    |
| MARGINAL  | CAPE 300-1000 OR (CAPE >= 1000 AND CIN 50-200) | Some instability, uncertain outcome        |
| UNSTABLE  | CAPE >= 1000 AND CIN < 50 AND LI < -3          | Convection likely                          |
| EXPLOSIVE | CAPE >= 2500 AND CIN < 50 AND LI < -6          | Severe convection expected                 |

**Relationship to Thunderstorm alert:** Coexist. Thunderstorm fires when convection is imminent (CAPE + active precipitation + gusts). AtmosphericStability describes the atmospheric profile hours ahead -- "the atmosphere is loaded but lidded" vs "explosive convection possible this afternoon." They answer different questions for the user.

**Config:** `AtmosphericStabilityConfig` -- `Enabled` (default false). Thresholds follow standard meteorological practice.

### CloudCeiling

**Output:** Condition
**Trigger:** Forecast domain
**Required ForecastParams:** CloudCoverLow, CloudCoverMid, CloudCoverHigh, FreezingLevelHeight, Visibility

**Logic:**

Estimates the cloud base altitude from per-layer cloud cover data. The ceiling is the lowest layer with significant coverage (>= 50%).

Layer estimation (approximate, from standard atmosphere):
- Low clouds: 300-2000m above ground
- Mid clouds: 2000-6000m (FreezingLevelHeight used as reference for base)
- High clouds: 6000m+ (cirrus, typically not operationally limiting)

Decision:

| Condition                                  | CeilingCategory | Estimated ceiling  |
|--------------------------------------------|-----------------|--------------------|
| All layers < 30%                           | UNLIMITED       | n/a                |
| CloudCoverHigh >= 50%, Mid/Low < 30%       | VERY_HIGH       | 6000m+             |
| CloudCoverMid >= 50%, Low < 30%            | HIGH            | FreezingLevel * 0.8|
| CloudCoverLow >= 50%                       | MODERATE-VERY_LOW| Based on BoundaryLayerHeight and humidity profile |

Within the Low layer, further refinement:
- BoundaryLayerHeight < 300m AND Visibility < 1000m: VERY_LOW
- BoundaryLayerHeight 300-1000m: LOW
- BoundaryLayerHeight > 1000m: MODERATE

CeilingCategory enum values:

| Category  | Range          |
|-----------|----------------|
| VERY_LOW  | < 300m         |
| LOW       | 300-1000m      |
| MODERATE  | 1000-3000m     |
| HIGH      | 3000-6000m     |
| VERY_HIGH | > 6000m        |
| UNLIMITED | Clear sky      |

**Use cases:** Drone pilots (legal ceiling limits, typically 120m in EU), photographers (diffused light planning), VFR pilots (minimum ceiling requirements), general outdoor activity understanding.

**Config:** `CloudCeilingConfig` -- `Enabled` (default false).

### Proto Messages

```protobuf
// Enums

enum InversionStrength {
  INVERSION_STRENGTH_UNSPECIFIED = 0;
  INVERSION_STRENGTH_NONE = 1;
  INVERSION_STRENGTH_MILD = 2;
  INVERSION_STRENGTH_STRONG = 3;
  INVERSION_STRENGTH_EXTREME = 4;
}

enum InversionType {
  INVERSION_TYPE_UNSPECIFIED = 0;
  INVERSION_TYPE_RADIATION = 1;
  INVERSION_TYPE_SUBSIDENCE = 2;
  INVERSION_TYPE_FRONTAL = 3;
}

enum StabilityClass {
  STABILITY_CLASS_UNSPECIFIED = 0;
  STABILITY_CLASS_STABLE = 1;
  STABILITY_CLASS_CAPPED = 2;
  STABILITY_CLASS_MARGINAL = 3;
  STABILITY_CLASS_UNSTABLE = 4;
  STABILITY_CLASS_EXPLOSIVE = 5;
}

enum CeilingCategory {
  CEILING_CATEGORY_UNSPECIFIED = 0;
  CEILING_CATEGORY_VERY_LOW = 1;
  CEILING_CATEGORY_LOW = 2;
  CEILING_CATEGORY_MODERATE = 3;
  CEILING_CATEGORY_HIGH = 4;
  CEILING_CATEGORY_VERY_HIGH = 5;
  CEILING_CATEGORY_UNLIMITED = 6;
}

// Messages

message InversionDetection {
  ConditionData condition = 1;
  InversionStrength strength = 2;
  InversionType inversion_type = 3;
  double temperature_difference_c = 4;
  double boundary_layer_height_m = 5;
  int32 expected_dissolution_hours = 6;
}

message AtmosphericStability {
  ConditionData condition = 1;
  StabilityClass stability_class = 2;
  double cape_j_per_kg = 3;
  double cin_j_per_kg = 4;
  double lifted_index = 5;
}

message CloudCeiling {
  ConditionData condition = 1;
  CeilingCategory ceiling_category = 2;
  int32 estimated_ceiling_m = 3;
  int32 cloud_cover_low_percent = 4;
  int32 cloud_cover_mid_percent = 5;
  int32 cloud_cover_high_percent = 6;
}
```

Field number assignments in `EnrichmentEvent.feature` oneof: three new entries in the Conditions range (60-79).

### i18n

All user-visible text is delivered as enums. ha-njord translates via `translation_key` and `strings.json`:

**Entity names:**
- `inversion_detection` -> EN: "Inversion Detection", DE: "Inversionserkennung"
- `atmospheric_stability` -> EN: "Atmospheric Stability", DE: "Atmosphaerische Stabilitaet"
- `cloud_ceiling` -> EN: "Cloud Ceiling", DE: "Wolkenuntergrenze"

**Enum states (sensor state translations):**

InversionStrength: none/mild/strong/extreme -> DE: Keine/Leicht/Stark/Extrem
InversionType: radiation/subsidence/frontal -> DE: Strahlungsinversion/Absinkinversion/Frontalinversion
StabilityClass: stable/capped/marginal/unstable/explosive -> DE: Stabil/Gedeckelt/Grenzwertig/Instabil/Explosiv
CeilingCategory: very_low/low/moderate/high/very_high/unlimited -> DE: Sehr niedrig/Niedrig/Mittel/Hoch/Sehr hoch/Unbegrenzt

Numeric values (temperature_difference_c, cape_j_per_kg, estimated_ceiling_m) are exposed as sensor attributes with their unit -- no translation needed.

## API Budget Impact

All three features use Forecast domain only.

New variables requested: Temperature80m, Temperature120m, ConvectiveInhibition, LiftedIndex, CloudCoverLow, CloudCoverMid, CloudCoverHigh (7 variables).

Cost formula: `ceil(variables / 10) * ceil(forecastDays / 14)` calls per cycle.

Current Forecast variable count depends on enabled features. Adding 7 variables may push past a `ceil(N/10)` boundary, adding at most 1 extra call per cycle.

Estimate per location (worst case):
- +1 call/cycle x 24 cycles/day = +24 calls/day
- +720 calls/month per location
- 3 locations: +2,160/month

Against the 300k/month free-tier budget, this is 0.7% -- negligible. BudgetGuard will account for these through the standard FeatureResolution -> DomainRequirements -> call weight calculation.

## Non-goals

- Weather radar integration or real-time observation data
- METAR/TAF parsing or aviation-standard reporting formats
- Aviation-grade ceiling reporting (this is an estimate from NWP model data, not observed)
- Sounding/radiosonde profile visualization
- Convection timing prediction (when exactly storms will fire)

## Dependencies

- **ha-njord-i18n-foundation**: translation_key pattern must be in place before adding new entities
- No dependency on other feature changes (score-windows, enrichment-events)
- No dependency on new input types -- all three features use Forecast domain only

## Risks

| Risk | Mitigation |
|------|------------|
| Temperature80m/120m accuracy varies by model | Open-Meteo best_match blends models; inversions are large-scale features well captured by NWP |
| Cloud ceiling estimation is approximate | Clearly label as "estimated" in entity name; CeilingCategory provides categorical safety rather than false precision |
| CIN/LiftedIndex may be null in some model outputs | Project returns null when required fields are missing; feature silently skips |
| Inversion type classification is heuristic | Type is informational, strength is the primary output; misclassified type has no safety impact |
| 7 new variables may interact with other features' variable requests | FeatureResolution OR-merges all requirements; BudgetGuard validates the total at startup |
