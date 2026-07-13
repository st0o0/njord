## Context

M1–M4 established the enrichment consumer pattern: pure static computation class, typed result record with `ToMqttMessages`, delta publishing, consumer-stream materialization gated by config. M5 follows the same architecture for daily-life indices.

Indices differ from previous consumers in that they combine *multiple* weather parameters into *single composite scores*. Each index is a weighted formula over a subset of: temperature, humidity, wind speed, precipitation probability, cloud cover, shortwave radiation, evapotranspiration, VPD. The formulas are meteorologically grounded but intentionally simple — they produce 0–100 scores, not scientific measurements.

## Goals / Non-Goals

**Goals:**
- 11 independently computable daily-life indices
- All pure functions, no external state
- One index device per location with one sensor per index
- Configurable base temperatures for degree days and indoor temp for ventilation
- Disabled by default (`Indices.Enabled = false`)

**Non-Goals:**
- Machine-learned or adaptive scoring (hardcoded formulas for v1)
- Per-horizon index breakdown (indices summarize the next 24h, not per-hour)
- Indices from non-weather data (air quality, pollen, UV action spectrum)

## Decisions

### D1: 24h summary indices, not per-horizon

**Decision:** Each index summarizes the forecast over the next 24 hours into a single score. Not per-horizon like consensus or derived values.

**Why:** "Can I hang laundry today?" is a day-level question. Per-hour scores would multiply the sensor count (11 × 6 = 66) for little dashboard value. Users who need hourly detail already have the raw model/consensus sensors.

**Exception:** Degree days are computed for the current day (midnight to midnight), matching the standard meteorological definition.

### D2: Score range 0–100 with clamping

**Decision:** All score indices (laundry, outdoor, running, cycling, BBQ, irrigation, solar, ventilation) output integers 0–100. Sub-scores for each contributing parameter are computed on 0–100, then combined with weights, then clamped to [0, 100].

**Why:** Consistent range makes HA dashboard gauges trivial. Integer precision is sufficient for a qualitative score.

### D3: Parameter extraction uses consensus median across models

**Decision:** Indices extract values from the `ModelSnapshot` by computing the median across all models at each hour in the 24h window, then summarizing (mean, min, max, sum as appropriate per index).

**Why:** Using consensus values rather than a single model makes the indices more robust. The median is already available from `ConsensusComputer.ComputeMedian`.

### D4: Index formulas

All scores are `(weighted_sum / max_possible) × 100`, clamped to [0, 100]:

**Laundry drying:** `0.3×temp_score + 0.25×humidity_score + 0.2×wind_score + 0.15×rain_score + 0.1×sunshine_score`. Temp: 0 at ≤5 °C, 100 at ≥25 °C. Humidity: 100 at ≤40%, 0 at ≥90%. Wind: 100 at ≥4 m/s, 0 at 0. Rain: 100 at 0% prob, 0 at ≥60%. Sunshine: direct from sunshine_pct.

**Outdoor:** `0.35×temp_comfort + 0.25×rain_score + 0.2×wind_score + 0.2×cloud_score`. Temp comfort: bell curve peaking at 22 °C, 0 at ≤5 or ≥38. Rain/wind/cloud as above.

**Running:** `0.3×temp_score + 0.25×humidity_score + 0.2×wind_score + 0.25×rain_score`. Temp: bell curve 5–20 °C optimal. Humidity: 100 at ≤50%, 0 at ≥85%. Wind: 100 at ≤3 m/s. Rain: as above.

**Cycling:** `0.25×temp_score + 0.15×humidity_score + 0.3×wind_score + 0.3×rain_score`. Wind penalized more heavily (headwind effect).

**BBQ:** `0.3×temp_score + 0.1×humidity_score + 0.25×wind_score + 0.35×rain_score`. Temp: 100 at ≥22 °C. Wind: prefer light (100 at 1–3 m/s, lower at calm or gusty). Rain: critical (0 at ≥30%).

**Irrigation:** `0.3×rain_inverse + 0.25×temp_score + 0.25×humidity_inverse + 0.2×et_score`. Rain: 100 when 0% prob. Temp: 100 at ≥30 °C. Humidity: 100 at ≤40%. ET: 100 at high evapotranspiration.

**Solar yield:** `0.5×radiation_score + 0.3×cloud_inverse + 0.2×temp_efficiency`. Radiation: from shortwave_radiation. Cloud: 100 at 0%. Temp efficiency: panels lose ~0.4%/°C above 25 °C.

**Ventilation:** `0.3×temp_delta + 0.25×humidity_score + 0.25×wind_score + 0.2×rain_score`. Temp delta: 100 when outdoor is ≥5 °C cooler than indoor (22 °C). Humidity: 100 at ≤50%. Wind: 100 at 2–5 m/s. Rain: 100 at 0%.

### D5: Non-score indices

**Degree days (HDD/CDD):** Standard formulas. HDD = max(0, base − mean_temp), CDD = max(0, mean_temp − base). Base temps configurable (default 18 °C / 24 °C).

**Frost protection:** Hours until first frost risk (temperature ≤ 0 °C) and confidence (fraction of models agreeing). Reuses the same hourly scan as `AlertEvaluator.EvaluateFrost` but returns timing, not alert.

**VPD:** Computed from temperature and humidity using the Magnus formula. Categories: low (< 0.4 kPa), optimal (0.4–1.2), high (1.2–2.0), critical (> 2.0).

### D6: Single index topic per location

**Decision:** Topic `njord/{location}/indices` with one flat JSON containing all 11 indices. Device id `njord_{location}_indices`, model `indices`.

### D7: Configuration

```json
{
  "Njord": {
    "Enrichment": {
      "Indices": {
        "Enabled": false,
        "HeatingBaseTemp": 18.0,
        "CoolingBaseTemp": 24.0,
        "IndoorTemp": 22.0
      }
    }
  }
}
```

## Risks / Trade-offs

**[Formulas are opinionated]** → Different climates may need different weights. v1 uses hardcoded Central European defaults. Configurable weights could come later.

**[Missing parameters degrade gracefully]** → If a parameter (e.g. evapotranspiration, shortwave_radiation) isn't in the active parameter set, the sub-score for that parameter is neutral (50/100) and the weight is redistributed. The index still produces a value.

**[Sensor count]** → 11 indices per location + degree days (2) + frost protection (2) + VPD (1) = 16 sensors per location. Manageable.
