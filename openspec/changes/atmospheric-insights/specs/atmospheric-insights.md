# Atmospheric Insights Spec

## InversionDetection

**Output:** Condition | **Category:** Insight | **Trigger:** Forecast | **Default:** off

### Behavior

- Detects temperature inversions by comparing surface temperature against 80m and 120m readings
- Reports an InversionStrength: NONE, MILD, STRONG, EXTREME
- Reports a best-effort InversionType: RADIATION, SUBSIDENCE, FRONTAL
- Reports temperature_difference_c (max of Temp80m - Temp, Temp120m - Temp80m)
- Reports boundary_layer_height_m
- Reports expected_dissolution_hours (hours until the inversion is expected to lift)
- Returns null (no output) when Temperature80m data is missing

### Strength classification

| Strength | Temp difference | BoundaryLayerHeight |
|----------|----------------|---------------------|
| NONE | <= 0°C | any |
| MILD | > 0°C | < 800m |
| STRONG | > 2°C | < 500m |
| EXTREME | > 4°C | < 300m |

Both conditions must be met. If temp difference > 0 but BoundaryLayer >= 800m, strength is NONE (inversion present but shallow/high-altitude, not trapping surface air).

### Type classification (informational)

| Type | Conditions |
|------|-----------|
| RADIATION | Not daytime AND CloudCover < 30% AND WindSpeed < 3 m/s |
| SUBSIDENCE | SurfacePressure > 1020 hPa AND strength persists (from history) |
| FRONTAL | Rapid pressure change detected in ring buffer history |

When no type matches, the type field is UNSPECIFIED.

### Dissolution estimate

- RADIATION: hours until next IsDay = true in the forecast hourly data
- Other types: hours until BoundaryLayerHeight > 1000m in the forecast data
- If no dissolution visible in the forecast window, field is 0

### Required ForecastParams

Required: Temperature, Temperature80m, Temperature120m, BoundaryLayerHeight
Optional: Visibility, CloudCover, WindSpeed, IsDay, SurfacePressure

### Config

Section: `Njord:Features:Insight:InversionDetection`
- `Enabled` (default: false)

No threshold overrides — the tiers follow standard meteorological definitions.

### PollDepth

3 (3h lookback for persistence and pressure-change checks)

---

## AtmosphericStability

**Output:** Condition | **Category:** Insight | **Trigger:** Forecast | **Default:** off

### Behavior

- Evaluates three atmospheric indices (CAPE, CIN, LiftedIndex) and combines them into a single StabilityClass
- Reports: stability_class, cape_j_per_kg, cin_j_per_kg, lifted_index
- Returns null when CAPE data is missing

### StabilityClass

| Class | Criteria | Meaning |
|-------|---------|---------|
| STABLE | CAPE < 300 | No convective energy |
| CAPPED | CAPE >= 1000 AND CIN > 200 | Energy present but lid prevents release |
| MARGINAL | CAPE 300-1000, OR (CAPE >= 1000 AND CIN 50-200) | Some instability, uncertain |
| UNSTABLE | CAPE >= 1000 AND CIN < 50 AND LI < -3 | Convection likely |
| EXPLOSIVE | CAPE >= 2500 AND CIN < 50 AND LI < -6 | Severe convection expected |

When CIN or LI are null, they are not considered — classification uses CAPE alone (STABLE if < 300, MARGINAL if 300-1000, UNSTABLE if > 1000 with no CIN data).

### Required ForecastParams

Required: Cape
Optional: ConvectiveInhibition, LiftedIndex

### Config

Section: `Njord:Features:Insight:AtmosphericStability`
- `Enabled` (default: false)

### PollDepth

0 (no history needed — current-snapshot evaluation)

---

## CloudCeiling

**Output:** Condition | **Category:** Insight | **Trigger:** Forecast | **Default:** off

### Behavior

- Estimates cloud base altitude from per-layer cloud cover data
- Reports: ceiling_category (enum), estimated_ceiling_m (int), cloud_cover_low_percent, cloud_cover_mid_percent, cloud_cover_high_percent
- Returns null when all three cloud-cover layers are missing

### CeilingCategory

| Category | Range | Condition |
|----------|-------|-----------|
| UNLIMITED | clear | All layers < 30% |
| VERY_HIGH | > 6000m | Only CloudCoverHigh >= 50% |
| HIGH | 3000-6000m | CloudCoverMid >= 50%, Low < 30% |
| MODERATE | 1000-3000m | CloudCoverLow >= 50%, BoundaryLayer > 1000m |
| LOW | 300-1000m | CloudCoverLow >= 50%, BoundaryLayer 300-1000m |
| VERY_LOW | < 300m | CloudCoverLow >= 50%, BoundaryLayer < 300m AND Visibility < 1000m |

The lowest significant layer (>= 50% cover) determines the category. Within the low layer, BoundaryLayerHeight and Visibility refine the estimate.

### estimated_ceiling_m

- UNLIMITED: 0 (sentinel)
- VERY_HIGH: 8000
- HIGH: FreezingLevelHeight * 0.8 (mid-cloud base approximation), clamped 3000-6000
- MODERATE: BoundaryLayerHeight, clamped 1000-3000
- LOW: BoundaryLayerHeight, clamped 300-1000
- VERY_LOW: BoundaryLayerHeight, clamped 0-300

### Required ForecastParams

Required: CloudCoverLow, CloudCoverMid, CloudCoverHigh
Optional: FreezingLevelHeight, BoundaryLayerHeight, Visibility

### Config

Section: `Njord:Features:Insight:CloudCeiling`
- `Enabled` (default: false)

### PollDepth

0 (current-snapshot evaluation)
