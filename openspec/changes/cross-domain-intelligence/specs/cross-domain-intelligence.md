# Cross-Domain Intelligence Spec

## CompoundHeatStress

**Output:** Alert | **Category:** Health | **Default:** off
**Input:** `ForecastAqInput` (Forecast + AirQuality)

### Requirements

- Forecast (required): `ApparentTemperature | WetBulbTemperature | UvIndex | BoundaryLayerHeight | WindSpeed | Humidity | Rain`
- AirQuality (required): `Pm25`

### Trigger Paths

Three independent trigger paths — the highest-severity match wins:

| Path | Condition | DominantStressor |
|------|-----------|-----------------|
| Wet-bulb | WetBulb >= WetBulbWarningThreshold | WetBulb |
| UV + pollution | UV >= 8 AND BoundaryLayer < 500m AND PM2.5 >= 15 | UvPollution |
| Heat + pollution | ApparentTemp >= 40 AND PM2.5 >= 25 | HeatInversion |

### Severity Mapping

| Level | Wet-bulb | UV + pollution | Heat + pollution |
|-------|----------|---------------|-----------------|
| Yellow | 28-32°C | UV 8-10 OR BL 300-500m | AppTemp 40-42 OR PM2.5 25-50 |
| Orange | 32-35°C | UV > 10 AND BL < 300m | AppTemp > 42 OR PM2.5 > 50 |
| Red | >= 35°C | — | AppTemp > 45 AND PM2.5 > 75 |

### Amplifier

BoundaryLayerHeight < 500m lowers all temperature thresholds by 2°C.

### Result

`CompoundHeatStressAlertResult : AlertFeatureResult` with:
- `DominantStressor` enum: `WetBulb`, `UvPollution`, `HeatInversion`
- `BoundaryLayerHeight` (double)
- `Pm25` (double)

### Config

`CompoundHeatConfig` at `Njord:Features:Health:CompoundHeat`:
- `Enabled` (false)
- `WetBulbWarningThreshold` (28.0)
- `WetBulbSevereThreshold` (32.0)
- `WetBulbExtremeThreshold` (35.0)
- `BoundaryLayerAmplifierThreshold` (500.0)

### Coexistence with WetBulbDanger

Both can be enabled. WetBulbDanger is Forecast-only (pure temperature). CompoundHeatStress adds the AirQuality dimension. No overlap suppression needed.

---

## AllergySeverity

**Output:** Score | **Category:** AirQuality | **Default:** off
**Input:** `ForecastAqInput` (AirQuality + Forecast)

### Requirements

- AirQuality (required): `BirchPollen | GrassPollen | Alder | Mugwort | RagweedPollen`
- Forecast (required): `WindSpeed | Humidity | Rain | BoundaryLayerHeight`

### Scoring

1. **Base score (0-100):** `max(birch * BirchWeight, grass * GrassWeight, alder, mugwort, ragweed * RagweedWeight)` normalized to 0-100 using species-specific EAN scaling factors, multiplied by `SensitivityMultiplier`.

2. **Atmospheric modifiers (additive, clamped 0-100):**

| Modifier | Condition | Effect |
|----------|-----------|--------|
| WindDispersal | WindSpeed > 4 m/s AND no rain | +15 |
| InversionTrap | BoundaryLayer < 800m | +15 |
| LowHumidity | Humidity < 40% | +10 |
| RainWashout | Rain > 0 in current or previous 2 points | -30 |
| HighHumidity | Humidity > 80% | -15 |

3. **BestStart/BestHours:** The window with the lowest score in the next 24h forecast.

### Result

`AllergySeverityScoreResult : ScoreFeatureResult` with:
- `DominantAllergen` enum: `Birch`, `Grass`, `Alder`, `Mugwort`, `Ragweed`
- `ActiveModifiers` (ImmutableArray\<AllergyModifier\>): `WindDispersal`, `RainWashout`, `InversionTrap`, `LowHumidity`, `HighHumidity`
- `BaseScore` (int): raw score before modifiers
- Per-species values: `BirchPollen`, `GrassPollen`, `AlderPollen`, `MugwortPollen`, `RagweedPollen` (doubles)

### Config

`AllergySeverityConfig` at `Njord:Features:AirQuality:AllergySeverity`:
- `Enabled` (false)
- `SensitivityMultiplier` (1.0, range 0.5-2.0)
- `BirchWeight` (1.0)
- `GrassWeight` (1.0)
- `RagweedWeight` (1.0)

### Coexistence with Pollen

Pollen is a simple per-species threshold alert. AllergySeverity is a composite cross-domain score. No overlap suppression needed.

---

## Proto Messages

### Enums

```
DominantStressor: UNSPECIFIED=0, WET_BULB=1, UV_POLLUTION=2, HEAT_INVERSION=3
DominantAllergen: UNSPECIFIED=0, BIRCH=1, GRASS=2, ALDER=3, MUGWORT=4, RAGWEED=5
AllergyModifier: UNSPECIFIED=0, WIND_DISPERSAL=1, RAIN_WASHOUT=2, INVERSION_TRAP=3, LOW_HUMIDITY=4, HIGH_HUMIDITY=5
```

### Messages

`CompoundHeatStress`: AlertData alert, DominantStressor dominant_stressor, double boundary_layer_height, double pm25
Oneof slot: 48 (next alert)

`AllergySeverity`: ScoreData score, DominantAllergen dominant_allergen, repeated AllergyModifier active_modifiers, int32 base_score, double birch/grass/alder/mugwort/ragweed_pollen
Oneof slot: 54 (next score)

### ha-njord Entities

CompoundHeatStress → binary_sensor (alert) + event transitions
AllergySeverity → sensor (score 0-100) with attributes: dominant_allergen, active_modifiers, base_score, per-species values, best_start, best_hours

All names and enum states via translation_key in strings.json + de.json.
