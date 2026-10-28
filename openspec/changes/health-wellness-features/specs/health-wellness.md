# Health & Wellness Features Spec

## MigraineRisk

### Behavior

- Output: Alert (binary_sensor in HA, on when active)
- Category: Health
- Config section: `Njord:Features:Health:Migraine`
- Default enabled: false
- Domain: Forecast only
- PollDepth: 6

### Requirements

Required Forecast params: `Pressure`, `Temperature`, `Humidity`, `WindDirection`, `DewPoint`

### Evaluation

The rule evaluates pressure trends from the ring buffer (last 6 polls) and temperature/humidity trends from hourly forecast data.

**Primary triggers:**
- 6h pressure drop: current pressure minus pressure from 6 polls ago > `PressureThreshold6h` (default 5.0 hPa)
- 24h pressure drop: pressure drop over the next 24h forecast window > `PressureThreshold24h` (default 10.0 hPa)

**Secondary triggers (each adds +1 to severity score):**
- Temperature swing > `TempSwingThreshold` (default 8.0°C) over 12h forecast window
- Humidity swing > `HumiditySwingThreshold` (default 30%) over 12h forecast window
- Warm dry wind: WindDirection 150-210° AND Temperature rising AND Humidity falling AND Pressure rising simultaneously across consecutive forecast hours (+2 to severity score)

**Severity:**
- Score 1 (one primary trigger alone) → `AlertSeverity.Yellow`, Confidence medium
- Score 2 (primary + 1 secondary, or Föhn alone) → `AlertSeverity.Orange`, Confidence medium-high
- Score 3+ (primary + 2+ secondary, or primary + Föhn) → `AlertSeverity.Red`, Confidence high
- Score 0 → no alert (return null)

**Result fields:**
- Standard AlertFeatureResult fields (Severity, Confidence, TriggerValue, Threshold, HoursUntil, DurationHours, Level)
- `MigraineTrigger DominantTrigger` — which trigger contributed most
- `double PressureChangeHpa` — the measured/forecast pressure change
- `double TemperatureChangeC` — the measured/forecast temperature change

### Proto

```
MigraineTrigger enum: UNSPECIFIED, PRESSURE_DROP, TEMPERATURE_SWING, FOEHN, HUMIDITY_SWING
MigraineRisk message: AlertData + dominant_trigger + pressure_change_hpa + temperature_change_c
EnrichmentEvent oneof: MigraineRisk migraine_risk = <next alert field number>
```

### Config

| Property | Type | Default |
|----------|------|---------|
| Enabled | bool | false |
| PressureThreshold6h | double | 5.0 |
| PressureThreshold24h | double | 10.0 |
| TempSwingThreshold | double | 8.0 |
| HumiditySwingThreshold | double | 30.0 |

---

## SleepQuality

### Behavior

- Output: Score (sensor 0-100 in HA, with BestStart/BestHours for ventilation window)
- Category: Lifestyle
- Config section: `Njord:Features:Lifestyle:Sleep`
- Default enabled: false
- Domains: Forecast + AirQuality

### Requirements

Required Forecast: `Temperature`, `ApparentTemperature`, `Humidity`, `WindGusts`, `Cape`, `Precipitation`, `DewPoint`
Required AirQuality: `Pm25`
Optional AirQuality: `BirchPollen`, `GrassPollen`, `Alder`, `Mugwort`, `RagweedPollen`

### Evaluation

Evaluates overnight comfort for the night starting at `NightStartHour` (default 22 UTC).

**Base score: 100. Penalties from worst hour in night window:**

| Factor | Condition | Penalty | SleepFactor |
|--------|-----------|---------|-------------|
| Tropical heat | min night temp > 25°C | -30 | Heat |
| Warm night | min night temp > 22°C (and ≤25) | -15 | Heat |
| Cold night | min night temp < 5°C | -10 | Cold |
| High humidity | max humidity > 80% | -10 | Humidity |
| Poor air quality | PM2.5 > 25 µg/m³ | -15 | AirQuality |
| Pollen (sensitive) | high pollen + PollenSensitive=true | -20 | Pollen |
| Pollen (general) | high pollen, not sensitive | -5 | Pollen |
| Wind noise | max gusts > 40 km/h (11.1 m/s) | -10 | Wind |
| Thunderstorm | CAPE > 1000 + Precipitation > 0 | -15 | Storm |

Penalties stack. Score clamped to [0, 100]. DominantFactor is the factor with the largest penalty.

**Ventilation recommendation:**
- `WINDOW_OPEN`: outdoor temp < 22°C AND DewPoint < 16°C AND Pollen low AND PM2.5 < 15
- `AC_RECOMMENDED`: outdoor temp > 30°C or humidity > 85%
- `WINDOW_CLOSED`: otherwise

**BestStart/BestHours:** optimal pre-sleep ventilation window — the consecutive hours before NightStartHour where WINDOW_OPEN conditions hold.

**Result fields:**
- Standard ScoreFeatureResult fields (Score, BestStart, BestHours)
- `SleepRecommendation Recommendation`
- `SleepFactor DominantFactor`
- `double MinNightTemperature`

### Proto

```
SleepRecommendation enum: UNSPECIFIED, WINDOW_OPEN, WINDOW_CLOSED, AC
SleepFactor enum: UNSPECIFIED, HEAT, HUMIDITY, POLLEN, WIND, STORM, AIR_QUALITY, COLD
SleepQuality message: ScoreData + recommendation + dominant_factor + min_night_temperature_c
EnrichmentEvent oneof: SleepQuality sleep_quality = <next score field number>
```

### Config

| Property | Type | Default |
|----------|------|---------|
| Enabled | bool | false |
| PollenSensitive | bool | false |
| NightStartHour | int | 22 |
| NightEndHour | int | 6 |

---

## AsthmaRisk

### Behavior

- Output: Score (sensor 0-100 in HA)
- Category: Health
- Config section: `Njord:Features:Health:Asthma`
- Default enabled: false
- Domains: Forecast + AirQuality

### Requirements

Required Forecast: `Temperature`, `Humidity`, `BoundaryLayerHeight`
Required AirQuality: `Pm25`, `Ozone`
Optional AirQuality: `BirchPollen`, `GrassPollen`, `Alder`, `Mugwort`, `RagweedPollen`

### Evaluation

**Component scores (each normalized 0-100):**

| Component | Weight | Normalization |
|-----------|--------|---------------|
| PM2.5 | 0.35 | Linear: 0 at 0 µg/m³ → 100 at 75 µg/m³ |
| Ozone | 0.25 | Linear: 0 at 0 µg/m³ → 100 at 180 µg/m³ |
| Pollen | 0.20 | Max of all species, normalized by species scales |
| Humidity | 0.10 | U-curve: 0 at 40-60%, 100 at ≤20% or ≥80% |
| Boundary layer | 0.10 | Inverted linear: 0 at 2000m → 100 at 200m |

**Composite = Σ(component × weight), then amplified:**

| Amplifier | Condition | Multiplier |
|-----------|-----------|------------|
| Cold air asthma | Temperature < 5°C AND Humidity > 80% | ×1.3 |
| Rapid temp change | > 10°C swing in 6h (ring buffer) | ×1.2 |

Final score clamped to [0, 100]. DominantFactor is the component or amplifier with the highest contribution.

**Result fields:**
- Standard ScoreFeatureResult fields (Score, BestStart, BestHours)
- `AsthmaDominantFactor DominantFactor`
- `int Pm25Component` (0-100)
- `int OzoneComponent` (0-100)
- `int PollenComponent` (0-100)

### Proto

```
AsthmaDominantFactor enum: UNSPECIFIED, PM25, OZONE, POLLEN, HUMIDITY, BOUNDARY_LAYER, COLD_AIR, TEMP_CHANGE
AsthmaRisk message: ScoreData + dominant_factor + pm25_component + ozone_component + pollen_component
EnrichmentEvent oneof: AsthmaRisk asthma_risk = <next score field number>
```

### Config

| Property | Type | Default |
|----------|------|---------|
| Enabled | bool | false |
| Pm25Weight | double | 0.35 |
| OzoneWeight | double | 0.25 |
| PollenWeight | double | 0.20 |
| HumidityWeight | double | 0.10 |
| BoundaryLayerWeight | double | 0.10 |
| ColdAirAmplifier | bool | true |
| TempChangeAmplifier | bool | true |

`AsthmaConfigValidator` ensures the five weights sum to 1.0 (±0.01 tolerance).
