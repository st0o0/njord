# Agriculture & Garden Features

## Summary

Add three agriculture and garden features to njord that turn currently unused Forecast variables (Evapotranspiration, SoilMoisture, VapourPressureDeficit, SoilTemperature, SnowDepth) into actionable intelligence for Home Assistant users with gardens, lawns, or smallholdings.

- **IrrigationNeed** -- should I water today or will rain do the job?
- **FrostDepth** -- how deep is frost penetrating and are my pipes/roots at risk?
- **SprayWindow** -- when is the next safe window to spray?

All three features use only Forecast-domain data and require no new API domains.

## Motivation

Five Forecast variables sit completely unused today: `Evapotranspiration`, `SoilMoisture0To1cm`, `VapourPressureDeficit`, `SoilTemperature`, and `SnowDepth`. These are high-value signals for anyone managing plants, lawns, or outdoor infrastructure. HA has a large gardening/agriculture community (irrigation controllers, soil sensors, greenhouse automations) but no integration combines weather forecast data into irrigation or frost-depth decisions. njord can fill that gap with zero additional API domains.

## Scope

| Feature | OutputKind | Domains | Default |
|---------|-----------|---------|---------|
| IrrigationNeed | Condition | Forecast | Off |
| FrostDepth | Condition | Forecast | Off |
| SprayWindow | Score (with windows) | Forecast | Off |

All three are opt-in features in `Njord:Features:Agriculture:<Name>`.

### Repositories touched

- **njord**: new FeatureRules in `Njord.Features/Conditions/` and `Njord.Features/Scores/`, new proto messages in `weather.proto`, new config classes, new enums, registration in `AddFeatures`, `docs/data/features.json` entries
- **ha-njord**: entity descriptions for 3 new sensors, translation entries in `strings.json` + `translations/de.json`

## Design

### IrrigationNeed

**Category:** `Agriculture`, **OutputKind:** `Condition`, **Default:** off

A condition that evaluates whether irrigation is needed by balancing water loss against expected water supply.

**Input:** Single-domain Forecast input requiring `Evapotranspiration`, `SoilMoisture0To1cm`, `VapourPressureDeficit`, `Rain`, `PrecipitationProbability`, `Temperature`.

**Logic:**

```
Water loss estimate:
  ET (Evapotranspiration) from API directly (mm/day)
  VPD as secondary signal for transpiration rate

Water supply expected:
  Weighted rain = Σ (hourly Rain × PrecipitationProbability / 100) for next 24h and 48h

Soil status:
  SoilMoisture 0-1cm as surface proxy (%)

Decision matrix:
  SoilMoisture > WetThreshold                          → SOIL_WET (skip)
  Temperature < 5°C                                     → skip (TEMPERATURE_LOW dormancy)
  Rain24h > 5mm with avg probability > 60%              → SKIP (RAIN_EXPECTED)
  Rain24h > 2mm with avg probability > 40%              → REDUCE
  ET > weighted_rain_48h AND SoilMoisture < DryThreshold → WATER
  else                                                   → SKIP (adequate)
```

Thresholds vary by soil type (sandy drains faster, clay retains more) and plant type (trees tolerate more, vegetables need consistent moisture).

**Config:** `IrrigationConfig`

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| Enabled | bool | false | |
| SoilType | enum | Loam | Sandy, Loam, Clay -- affects wet/dry thresholds |
| PlantType | enum | Garden | Lawn, Garden, Vegetables, Trees -- affects sensitivity |
| WetThreshold | double | 80.0 | SoilMoisture % above which skip irrigation |
| DryThreshold | double | 30.0 | SoilMoisture % below which irrigation is urgent |

**Enums:**

- `IrrigationAdvice`: `Water`, `Skip`, `Reduce`, `SoilWet`
- `SkipReason`: `RainExpected`, `SoilWet`, `TemperatureLow`, `Adequate`
- `SoilType`: `Sandy`, `Loam`, `Clay`
- `PlantType`: `Lawn`, `Garden`, `Vegetables`, `Trees`

**Proto:**

```protobuf
message IrrigationNeed {
  ConditionData condition = 1;
  IrrigationAdvice advice = 2;
  SkipReason skip_reason = 3;
  double soil_moisture_percent = 4;
  double rain_expected_mm = 5;
  double et_mm = 6;
  double vpd = 7;
}
```

**HA entity:** `sensor.njord_<loc>_irrigation` with state = advice enum (translated), attributes expose all numeric values.

### FrostDepth

**Category:** `Agriculture`, **OutputKind:** `Condition`, **Default:** off

Estimates frost penetration depth using air temperature, soil temperature, snow cover, and wind.

**Input:** Single-domain Forecast input requiring `Temperature`, `SoilTemperature`, `SnowDepth`, `WindSpeed`.

**Logic:**

```
Simplified Stefan model:
  freezing_degree_hours = Σ max(0, -Temperature) for forecast hours where Temp < 0
  
  soil_factor:
    Sandy: 1.2 (conducts cold faster)
    Loam:  1.0 (baseline)
    Clay:  0.8 (retains heat better)
  
  snow_insulation = 1.0 + (SnowDepth_cm / 10.0)
  
  wind_factor = 1.0 + 0.05 × max(0, WindSpeed - 3.0)
  
  frost_depth_cm = k × √(freezing_degree_hours) × soil_factor × wind_factor / snow_insulation
  where k ≈ 2.0 (empirical constant for temperate soils)

Risk classification:
  NONE:     depth < 1cm or soil temp > 0°C
  SURFACE:  1-5cm
  SHALLOW:  5-15cm
  MODERATE: 15-30cm
  DEEP:     > 30cm

Pipe alert: depth_cm >= PipeDepthCm × 0.7 (safety margin)
```

**Config:** `FrostDepthConfig`

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| Enabled | bool | false | |
| SoilType | enum | Loam | Affects thermal conductivity factor |
| PipeDepthCm | int | 60 | Depth of water pipes -- alert when frost approaches |

**Enums:**

- `FrostDepthRisk`: `None`, `Surface`, `Shallow`, `Moderate`, `Deep`

**Proto:**

```protobuf
message FrostDepth {
  ConditionData condition = 1;
  FrostDepthRisk risk = 2;
  double depth_cm = 3;
  double snow_insulation_cm = 4;
  double soil_temperature_c = 5;
  bool pipe_risk = 6;
}
```

**HA entity:** `sensor.njord_<loc>_frost_depth` with state = risk enum (translated), attributes expose depth_cm, snow cover, soil temp, pipe_risk boolean for automations.

### SprayWindow

**Category:** `Agriculture`, **OutputKind:** `Score` (with windows), **Default:** off

Finds optimal windows for applying pesticides, herbicides, or foliar treatments. Spraying in wrong conditions wastes product (drift, washoff) and risks environmental damage.

**Input:** Single-domain Forecast input requiring `WindSpeed`, `Rain`, `PrecipitationProbability`, `Temperature`, `Humidity`.

**Logic:**

```
Per-hour score (0-100):
  Mandatory (score = 0 if violated):
    WindSpeed < MaxWindSpeed (default 3 m/s) -- drift prevention
    No rain with >80% probability in next RainFreeHours (default 4h) -- washoff prevention
    
  Optimal range scoring:
    Temperature: 100 in 10-25°C, linear falloff to 0 at 5°C and 30°C
    Humidity: 100 in 40-80%, linear falloff to 0 at 20% and 95%
    Wind: 100 at 0 m/s, linear to 50 at MaxWindSpeed

  Combined: min(mandatory_pass, temp_score × 0.4 + humidity_score × 0.3 + wind_score × 0.3)

Sliding window:
  Find contiguous hours where score > MinScore (default 50)
  Window score = min score within the window (weakest hour limits)
  Return top 3 windows with LimitingFactor at each boundary
```

Uses `ScoreData` with `repeated TimeWindow` from the score-windows change.

**Config:** `SprayConfig`

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| Enabled | bool | false | |
| MaxWindSpeed | double | 3.0 | m/s, above this score is 0 |
| RainFreeHours | int | 4 | Hours of dry weather needed after application |
| MinTemp | double | 10.0 | °C, lower bound of optimal range |
| MaxTemp | double | 25.0 | °C, upper bound of optimal range |
| MinScore | int | 50 | Minimum score to consider a window viable |

**Proto:**

```protobuf
message SprayWindow {
  ScoreData score = 1;
}
```

SprayWindow is a thin wrapper; the TimeWindow details (start, end, per-window score, LimitingFactor) are carried by ScoreData.windows from the score-windows change.

**HA entities:**
- `sensor.njord_<loc>_spray_score` -- current score (0-100), attributes include windows[]
- `binary_sensor.njord_<loc>_spray_window` -- ON when inside a viable window
- Calendar event in `calendar.njord_<loc>_activity_windows` (via score-windows)

### i18n

All enums are translation keys in ha-njord. No human-readable strings leave njord.

**strings.json additions:**

```json
{
  "entity": {
    "sensor": {
      "irrigation": {
        "name": "Irrigation",
        "state": {
          "water": "Water now",
          "skip": "Skip",
          "reduce": "Reduce amount",
          "soil_wet": "Soil wet"
        },
        "state_attributes": {
          "skip_reason": {
            "name": "Skip reason",
            "state": {
              "rain_expected": "Rain expected",
              "soil_wet": "Soil wet",
              "temperature_low": "Too cold",
              "adequate": "Adequate moisture"
            }
          }
        }
      },
      "frost_depth": {
        "name": "Frost depth",
        "state": {
          "none": "No frost",
          "surface": "Surface frost",
          "shallow": "Shallow frost",
          "moderate": "Moderate frost",
          "deep": "Deep frost"
        }
      },
      "spray_score": {
        "name": "Spray conditions"
      }
    },
    "binary_sensor": {
      "spray_window": {
        "name": "Spray window"
      }
    }
  }
}
```

German translations in `translations/de.json` follow the same structure.

## API Budget Impact

All three features use Forecast domain only. The additional variables requested:

| Variable | Currently used | Features needing it |
|----------|---------------|-------------------|
| Evapotranspiration | No | IrrigationNeed |
| SoilMoisture0To1cm | Yes (SoilMoisture feature) | IrrigationNeed |
| VapourPressureDeficit | No | IrrigationNeed |
| SoilTemperature | No | FrostDepth |
| SnowDepth | Yes (SnowDepthWatch) | FrostDepth |
| WindSpeed | Yes (many) | SprayWindow |
| Rain | Yes (many) | SprayWindow |
| PrecipitationProbability | Yes (many) | SprayWindow |
| Temperature | Yes (many) | All three |
| Humidity | Yes (many) | SprayWindow |

Net new variables: Evapotranspiration, VapourPressureDeficit, SoilTemperature (3 new).

Cost model: `ceil(variables / 10) * ceil(forecastDays / 14)`. Adding 3 variables may push past a `ceil` boundary depending on how many other features are enabled.

**Worst case per location:** +1-2 calls/cycle x 24 cycles/day = +24-48 calls/day = +720-1440/month.
**For 3 locations:** +2160-4320/month. Well within the 300k monthly budget.

## Non-goals

- Integration with specific irrigation controllers, sprinkler systems, or valve actuators (HA automations handle the actuation side)
- Crop-specific growing models or phenology tracking
- Precision agriculture (field-level soil moisture mapping, drone data)
- Real soil sensor integration (Phase 3, requires HA sensor input)

## Dependencies

| Dependency | Required for | Hard/Soft |
|-----------|-------------|-----------|
| ha-njord-i18n-foundation | Translation pattern for all entities | Hard |
| score-windows | SprayWindow TimeWindow + Calendar support | Hard (SprayWindow only) |

IrrigationNeed and FrostDepth can ship independently of score-windows.

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| SoilMoisture 0-1cm is a coarse proxy | Irrigation advice may not match actual root-zone moisture | Document as "surface indicator"; recommend pairing with HA soil sensors for precision |
| Frost depth model is simplified | Real frost depth depends on soil moisture, organic content, groundwater | Use conservative safety margin for pipe alerts; document as estimate |
| SprayWindow regulatory differences | Spray regulations vary by country/region | Feature provides weather suitability only, not regulatory compliance; document clearly |
| VPD unfamiliar to casual gardeners | Users may not understand the attribute | Translate to actionable advice (Water/Skip), not raw VPD; expose VPD as attribute for power users |
