# Agriculture & Garden Features Spec

## IrrigationNeed

**Output:** Condition | **Category:** Agriculture | **Default:** off | **Domain:** Forecast

### Required Parameters

`ForecastParams.Evapotranspiration | ForecastParams.SoilMoisture0To1cm | ForecastParams.VapourPressureDeficit | ForecastParams.Rain | ForecastParams.PrecipitationProbability | ForecastParams.Temperature`

### Behavior

The feature evaluates whether irrigation is needed by balancing water loss (ET, VPD) against expected water supply (rain forecast) and current soil state.

**Decision matrix (evaluated top-to-bottom, first match wins):**

| Condition | Advice | SkipReason |
|-----------|--------|------------|
| SoilMoisture > WetThreshold | SoilWet | SoilWet |
| Temperature < 5°C | Skip | TemperatureLow |
| Rain24h > 5mm AND avg probability > 60% | Skip | RainExpected |
| Rain24h > 2mm AND avg probability > 40% | Reduce | — |
| ET > weighted_rain_48h AND SoilMoisture < DryThreshold | Water | — |
| Otherwise | Skip | Adequate |

Where:
- Rain24h = sum of hourly `Rain` for next 24h, weighted by `PrecipitationProbability / 100`
- weighted_rain_48h = same for 48h
- Thresholds vary by SoilType: Sandy (Wet=70, Dry=20), Loam (Wet=80, Dry=30), Clay (Wet=90, Dry=40)

### Result

`IrrigationNeedResult(Location, IrrigationAdvice, SkipReason?, SoilMoisture, RainExpectedMm, EtMm, Vpd)`

### Enums

- `IrrigationAdvice`: Water, Skip, Reduce, SoilWet
- `SkipReason`: RainExpected, SoilWet, TemperatureLow, Adequate

### Config

`IrrigationNeedConfig` at `Njord:Features:Agriculture:IrrigationNeed`

| Option | Type | Default |
|--------|------|---------|
| Enabled | bool | false |
| SoilType | SoilType | Loam |
| PlantType | PlantType | Garden |
| WetThreshold | double | 80.0 |
| DryThreshold | double | 30.0 |

### Proto

```protobuf
message IrrigationNeedCondition {
  ConditionData condition = 1;
  IrrigationAdvice advice = 2;
  SkipReason skip_reason = 3;
  double soil_moisture_percent = 4;
  double rain_expected_mm = 5;
  double et_mm = 6;
  double vpd = 7;
}
```

Enums `IrrigationAdvice` and `SkipReason` with UNSPECIFIED = 0 and prefixed members.

---

## FrostDepth

**Output:** Condition | **Category:** Agriculture | **Default:** off | **Domain:** Forecast

### Required Parameters

`ForecastParams.Temperature | ForecastParams.SoilTemperature | ForecastParams.SnowDepth | ForecastParams.WindSpeed`

### Behavior

Estimates frost penetration depth using a simplified Stefan model over the forecast window.

**Algorithm:**

```
freezing_degree_hours = Σ max(0, -Temperature) for hours where Temperature < 0
soil_factor = SoilType switch { Sandy → 1.2, Loam → 1.0, Clay → 0.8 }
snow_insulation = 1.0 + (SnowDepth_cm / 10.0)
wind_factor = 1.0 + 0.05 × max(0, WindSpeed - 3.0)
frost_depth_cm = 2.0 × √(freezing_degree_hours) × soil_factor × wind_factor / snow_insulation
```

**Risk classification:**

| Range | Level |
|-------|-------|
| < 1 cm or soil temp > 0°C | None |
| 1–5 cm | Surface |
| 5–15 cm | Shallow |
| 15–30 cm | Moderate |
| > 30 cm | Deep |

**Pipe risk:** `frost_depth_cm >= PipeDepthCm × 0.7`

### Result

`FrostDepthResult(Location, FrostDepthRisk, DepthCm, SnowInsulationCm, SoilTemperatureC, PipeRisk)`

### Enums

- `FrostDepthRisk`: None, Surface, Shallow, Moderate, Deep

### Config

`FrostDepthConfig` at `Njord:Features:Agriculture:FrostDepth`

| Option | Type | Default |
|--------|------|---------|
| Enabled | bool | false |
| SoilType | SoilType | Loam |
| PipeDepthCm | int | 60 |

### Proto

```protobuf
message FrostDepthCondition {
  ConditionData condition = 1;
  FrostDepthRisk risk = 2;
  double depth_cm = 3;
  double snow_insulation_cm = 4;
  double soil_temperature_c = 5;
  bool pipe_risk = 6;
}
```

---

## SprayWindow

**Output:** Score | **Category:** Agriculture | **Default:** off | **Domain:** Forecast

### Required Parameters

`ForecastParams.WindSpeed | ForecastParams.Rain | ForecastParams.PrecipitationProbability | ForecastParams.Temperature | ForecastParams.Humidity`

### Behavior

Calculates a per-hour spray suitability score and finds the best spray window.

**Per-hour score (0–100):**

1. **Mandatory checks** (score = 0 if violated):
   - WindSpeed < MaxWindSpeed (default 3 m/s)
   - No rain with >80% probability in next RainFreeHours (default 4h)

2. **Optimal range scoring:**
   - Temperature: 100 in [MinTemp, MaxTemp], linear to 0 at ±5°C outside
   - Humidity: 100 in [40%, 80%], linear to 0 at 20% and 95%
   - Wind: 100 at 0 m/s, linear to 50 at MaxWindSpeed

3. **Combined:** `mandatory_pass ? (temp × 0.4 + humidity × 0.3 + wind × 0.3) : 0`

**Window extraction:**
- Find contiguous hours where score > MinScore
- BestStart = start of highest-scoring contiguous block
- BestHours = length of that block
- Score = minimum score within the block (weakest hour limits)

When score-windows change is available, the feature populates `repeated TimeWindow` with top-3 windows and LimitingFactor per boundary.

### Result

`SprayWindowResult(Location, Score, BestStart, BestHours)` extending `ScoreFeatureResult`

### Config

`SprayWindowConfig` at `Njord:Features:Agriculture:SprayWindow`

| Option | Type | Default |
|--------|------|---------|
| Enabled | bool | false |
| MaxWindSpeed | double | 3.0 |
| RainFreeHours | int | 4 |
| MinTemp | double | 10.0 |
| MaxTemp | double | 25.0 |
| MinScore | int | 50 |

### Proto

```protobuf
message SprayWindowScore {
  ScoreData score = 1;
}
```

Thin wrapper; the TimeWindow details come from ScoreData when score-windows is available.

---

## Shared Enums

These live in `Njord.Messages` alongside the result records:

- `SoilType`: Sandy, Loam, Clay (used by IrrigationNeed and FrostDepth configs, not in results/proto)
- `PlantType`: Lawn, Garden, Vegetables, Trees (IrrigationNeed config only, not in results/proto)

Config-only enums (SoilType, PlantType) do not need proto messages — they are startup configuration, not wire values.

---

## ha-njord Entity Mapping

| Feature | Entity type | State | Key attributes |
|---------|------------|-------|----------------|
| IrrigationNeed | sensor (enum) | advice value | soil_moisture_percent, rain_expected_mm, et_mm, vpd, skip_reason |
| FrostDepth | sensor (enum) | risk level | depth_cm, snow_insulation_cm, soil_temperature_c, pipe_risk |
| SprayWindow | sensor (numeric) | score 0–100 | best_start, best_hours, windows[] (when available) |
| SprayWindow | binary_sensor | ON during best window | — |
