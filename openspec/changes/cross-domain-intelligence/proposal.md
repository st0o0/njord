# Cross-Domain Intelligence Features

## Summary

Add two features that combine Forecast and AirQuality domain data to produce insights neither domain can deliver alone: **CompoundHeatStress** (Alert) detects dangerous heat-plus-pollution combinations, and **AllergySeverity** (Score) produces a composite allergy index weighted by atmospheric conditions.

## Motivation

Existing features operate within a single domain. The most actionable weather intelligence comes from cross-domain correlation:

- A WetBulb of 30°C is dangerous; combined with PM2.5 > 25 and a low boundary layer it is an emergency. No single-domain alert captures this.
- Birch pollen "High" is useful but incomplete. Wind dispersal, rain washout, and boundary-layer trapping change the real exposure by a factor of 2-3x. A composite score with a "best low-pollen window" is far more actionable than a raw concentration number.

The cross-domain input pattern is already proven (CoastalFlood uses 3 domains, SmogRisk uses Forecast + AirQuality). These features extend that pattern to health-critical scenarios.

## Scope

### In scope

- `CompoundHeatStress` feature (Alert, category Health)
- `AllergySeverity` feature (Score, category AirQuality)
- Extension of `ForecastAqInput` with `BoundaryLayerHeight`
- Proto messages and enums for both features
- ha-njord entity descriptions (using `translation_key` pattern)
- EN + DE translations for all new entities, states, and enum values
- Config classes with validators
- Registration in `AddFeatures`
- docs/data/features.json entries
- Config Builder updates

### Out of scope

- CoastalStormSurge enhancement (needs investigation of existing CoastalFlood)
- HA sensor input (Phase 3)
- New OutputKind or WindowResult (separate change: score-windows)

## Design

### CompoundHeatStress

**Output:** Alert (category: Health, default: off)

**Input type:** `ForecastAqInput` (extended)

**Requirements:**
- Forecast (required): ApparentTemperature, WetBulbTemperature, UvIndex, BoundaryLayerHeight, WindSpeed, Humidity, Rain
- AirQuality (required): Pm25

**Evaluation logic:**

Three independent trigger paths, any of which fires the alert:

| Path | Condition | Rationale |
|------|-----------|-----------|
| Wet-bulb heat | WetBulb >= threshold (default 28°C) | Direct physiological danger |
| UV + pollution | UV >= 8 AND BoundaryLayer < 500m AND PM2.5 >= 15 | UV exposure compounded by trapped pollutants |
| Heat + pollution | ApparentTemp >= 40 AND PM2.5 >= 25 | Heat stress worsened by particulate load |

**Severity mapping:**

| Level | Wet-bulb path | UV+pollution path | Heat+pollution path |
|-------|--------------|-------------------|---------------------|
| Warning | 28-32°C | UV 8-10 or BL 300-500m | AppTemp 40-42 or PM2.5 25-50 |
| Severe | 32-35°C | UV > 10 and BL < 300m | AppTemp > 42 or PM2.5 > 50 |
| Extreme | >= 35°C | -- | AppTemp > 45 and PM2.5 > 75 |

**Amplifier:** BoundaryLayerHeight < 500m lowers all temperature thresholds by 2°C (trapped heat + pollutants).

**Result fields:**
- `AlertData` (severity, confidence, trigger_value, threshold, peak_value, hours_until, duration_hours)
- `DominantStressor` enum: which trigger path fired
- `boundary_layer_height`: current BL in meters (context for the user)
- `pm25`: current PM2.5 value

**Relationship to WetBulbDanger:** Coexist. WetBulbDanger is a pure Forecast-only alert on wet-bulb temperature. CompoundHeatStress adds the AirQuality dimension. A user who enables both gets a simple WetBulb warning AND a compound assessment. They serve different audiences (general safety vs. pollution-aware health).

**Config (`CompoundHeatConfig`):**
- `Enabled` (default: false)
- `WetBulbWarningThreshold` (default: 28°C)
- `WetBulbSevereThreshold` (default: 32°C)
- `WetBulbExtremeThreshold` (default: 35°C)
- `BoundaryLayerAmplifierThreshold` (default: 500m)

### AllergySeverity

**Output:** Score (category: AirQuality, default: off)

**Input type:** `ForecastAqInput` (extended)

**Requirements:**
- AirQuality (required): BirchPollen, GrassPollen, Alder, Mugwort, RagweedPollen
- Forecast (required): WindSpeed, Humidity, Rain, BoundaryLayerHeight
- Forecast (optional): Precipitation

**Evaluation logic:**

1. **Base score (0-100):** Normalize `max(birch, grass, alder, mugwort, ragweed)` to 0-100 using species-specific scaling factors (each species has a different "high" threshold from the European Aeroallergen Network).

2. **Atmospheric modifiers (additive, clamped to 0-100):**

   | Modifier | Condition | Effect |
   |----------|-----------|--------|
   | Wind dispersal | WindSpeed > 4 m/s AND no rain | +15 |
   | Inversion trap | BoundaryLayer < 800m | +15 |
   | Low humidity | Humidity < 40% | +10 |
   | Rain washout | Rain > 0 in current or previous 2 data points | -30 |
   | High humidity | Humidity > 80% | -15 |

3. **Result fields:**
   - `ScoreData` (score 0-100, best_start, best_hours for lowest-pollen window)
   - `DominantAllergen` enum: which species contributes most
   - `ActiveModifier` enum (repeated/flags): which atmospheric modifiers are active
   - `base_score`: raw pollen score before modifiers
   - Per-species values for attribute display

**Config (`AllergySeverityConfig`):**
- `Enabled` (default: false)
- `SensitivityMultiplier` (default: 1.0, range 0.5-2.0) -- personal sensitivity scaling
- `BirchWeight`, `GrassWeight`, `RagweedWeight` etc. (default: 1.0 each) -- per-species weight for users who know their triggers

### Input Type Extension

`ForecastAqInput` currently holds a `ForecastPoint` and an `AirQualityPoint`. Both already contain all fields -- the question is whether `BoundaryLayerHeight` is populated in the `ForecastPoint`.

The input struct itself does not filter fields. The gating mechanism is `DomainRequirements`: if a feature declares `ForecastParams.BoundaryLayerHeight` as required, `FeatureResolution` ensures the API requests it, and `HasFields` skips the rule if it is missing.

No structural change to `ForecastAqInput` is needed. The new features simply declare `BoundaryLayerHeight` in their `DomainRequirements.Forecast` flags. If other features sharing `ForecastAqInput` do not require it, it will be `null` in their data but present when these features run.

### Proto Messages

```protobuf
// In weather.proto

enum DominantStressor {
  DOMINANT_STRESSOR_UNSPECIFIED = 0;
  DOMINANT_STRESSOR_WET_BULB = 1;
  DOMINANT_STRESSOR_UV_POLLUTION = 2;
  DOMINANT_STRESSOR_HEAT_INVERSION = 3;
}

message CompoundHeatStress {
  AlertData alert = 1;
  DominantStressor dominant_stressor = 2;
  double boundary_layer_height = 3;
  double pm25 = 4;
}

enum DominantAllergen {
  DOMINANT_ALLERGEN_UNSPECIFIED = 0;
  DOMINANT_ALLERGEN_BIRCH = 1;
  DOMINANT_ALLERGEN_GRASS = 2;
  DOMINANT_ALLERGEN_ALDER = 3;
  DOMINANT_ALLERGEN_MUGWORT = 4;
  DOMINANT_ALLERGEN_RAGWEED = 5;
}

enum AllergyModifier {
  ALLERGY_MODIFIER_UNSPECIFIED = 0;
  ALLERGY_MODIFIER_WIND_DISPERSAL = 1;
  ALLERGY_MODIFIER_RAIN_WASHOUT = 2;
  ALLERGY_MODIFIER_INVERSION_TRAP = 3;
  ALLERGY_MODIFIER_LOW_HUMIDITY = 4;
  ALLERGY_MODIFIER_HIGH_HUMIDITY = 5;
}

message AllergySeverity {
  ScoreData score = 1;
  DominantAllergen dominant_allergen = 2;
  repeated AllergyModifier active_modifiers = 3;
  int32 base_score = 4;
  double birch_pollen = 5;
  double grass_pollen = 6;
  double alder_pollen = 7;
  double mugwort_pollen = 8;
  double ragweed_pollen = 9;
}
```

New oneof cases in `EnrichmentEvent`:
```protobuf
CompoundHeatStress compound_heat_stress = <next alert slot>;
AllergySeverity allergy_severity = <next score slot>;
```

### i18n

All user-visible text lives in ha-njord `strings.json` / `translations/de.json`. njord sends only enums and numbers.

**Entity names (translation_key):**

| Key | EN | DE |
|-----|----|----|
| `compound_heat_stress` | Compound Heat Stress | Hitze-Belastungsindex |
| `allergy_severity` | Allergy Severity | Allergie-Schwere |
| `allergy_severity_trigger` | Dominant Allergen | Dominantes Allergen |

**Enum states:**

DominantStressor:

| Key | EN | DE |
|-----|----|----|
| `wet_bulb` | Wet-bulb heat | Feuchtkugeltemperatur |
| `uv_pollution` | UV + air pollution | UV + Luftverschmutzung |
| `heat_inversion` | Heat + inversion | Hitze + Inversion |

DominantAllergen:

| Key | EN | DE |
|-----|----|----|
| `birch` | Birch | Birke |
| `grass` | Grass | Gras |
| `alder` | Alder | Erle |
| `mugwort` | Mugwort | Beifuss |
| `ragweed` | Ragweed | Ambrosia |

AllergyModifier:

| Key | EN | DE |
|-----|----|----|
| `wind_dispersal` | Wind dispersal | Windverteilung |
| `rain_washout` | Rain washout | Regenauswaschung |
| `inversion_trap` | Inversion trapping | Inversions-Falle |
| `low_humidity` | Low humidity | Niedrige Luftfeuchtigkeit |
| `high_humidity` | High humidity | Hohe Luftfeuchtigkeit |

## API Budget Impact

No new domains. Both features use Forecast + AirQuality which are required by existing default-on features (weather alerts need Forecast, Pollen/Pm25 need AirQuality).

Additional Forecast variables requested: `BoundaryLayerHeight` (if not already pulled by another enabled feature). This may push the variable count past a `ceil(variables / 10)` boundary, adding at most 1 call per poll cycle per location.

**Worst case:** +1 call/cycle/location = +24 calls/day/location = +720 calls/month/location. With 5 locations: +3,600/month against 300k limit (1.2%). Negligible.

## Non-goals

- CoastalStormSurge (4-domain feature, needs investigation of existing CoastalFlood first)
- Indoor air quality comparison (requires HA sensor input, Phase 3)
- Personal allergy profiles beyond config weights (would need user accounts)
- Replacing WetBulbDanger or Pollen alerts (coexistence by design)

## Dependencies

- **ha-njord-i18n-foundation**: All new entities must use `translation_key` from the start. The i18n refactor should land first so the pattern is established.
- **Proto field numbers**: Need to pick unused oneof slots in EnrichmentEvent.

## Risks

| Risk | Mitigation |
|------|------------|
| BoundaryLayerHeight data quality varies by model | Use it as amplifier, not primary trigger; features work without it (just less precise) |
| Pollen normalization varies by region/season | Use EAN (European Aeroallergen Network) thresholds as defaults; config weights let users adjust |
| WetBulb + PM2.5 compound thresholds are not well-studied | Conservative defaults; label as "experimental" in docs; user-configurable thresholds |
| ForecastAqInput extension could affect other features | No structural change needed; only DomainRequirements differ per feature |
