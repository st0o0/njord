# Health & Wellness Features

## Summary

Add three health-oriented features that translate weather and air quality data into actionable health risk assessments: **MigraineRisk** (pressure/temperature-driven alert), **SleepQuality** (overnight comfort score with ventilation advice), and **AsthmaRisk** (weighted multi-factor respiratory score). All three use existing or commonly-enabled Forecast and AirQuality data, output structured enums for i18n, and follow the established `FeatureRule<TInput>` pattern.

## Motivation

Weather significantly impacts health, but raw meteorological data is not actionable for most users. A 6 hPa pressure drop in 6 hours means nothing to a migraine sufferer -- "Migraine risk: moderate, pressure dropping" does. These features turn data that njord already fetches into preventive health intelligence:

- **Migraine**: affects ~15% of adults. Barometric pressure changes are a clinically documented trigger. No HA integration currently correlates pressure trends with migraine risk.
- **Sleep**: tropical nights, humidity, pollen, and noise (wind gusts) all degrade sleep. Users with smart ventilation or AC can automate responses.
- **Asthma**: PM2.5, ozone, pollen, and cold air are established triggers. A composite score beats checking five separate sensors.

## Scope

**In scope:**
- Three new `FeatureRule<TInput>` implementations in `Njord.Features`
- Proto messages for each feature in `weather.proto`
- gRPC mapping extensions
- Config classes with validators
- Registration in `AddFeatures`
- ha-njord entity descriptions (sensor, binary_sensor) with full i18n via `translation_key`
- Unit tests for rules, state logic, and proto mappings
- `docs/data/features.json` entries

**Out of scope:**
- Indoor temperature input from HA sensors (Phase 3)
- Medical-grade accuracy or clinical validation
- Integration with health tracking apps
- New HA entity types (calendar, new events -- covered by other changes)

## Design

### MigraineRisk

**Output:** Alert (binary_sensor on/off + event transitions)
**Category:** Health
**Domain:** Forecast
**Default enabled:** off

#### Input

Uses a Forecast-only input struct. Required params: `Pressure`, `Temperature`, `Humidity`, `WindDirection`, `DewPoint`. PollDepth: 6 (6 hours of history at 60-minute polling).

#### Evaluation logic

Primary triggers (from epidemiological studies -- Mukamal et al. 2009, Kimoto et al. 2011):

| Trigger | Condition | Weight |
|---------|-----------|--------|
| Rapid pressure drop | > `PressureThreshold6h` hPa fall in 6h | Primary |
| Sustained pressure drop | > `PressureThreshold24h` hPa fall in 24h | Primary |

Secondary triggers (amplifying, each adds +1 to severity score):

| Trigger | Condition |
|---------|-----------|
| Temperature swing | > `TempSwingThreshold` °C change in 12h |
| Humidity swing | > `HumiditySwingThreshold` % change in 12h |
| Warm dry wind (Föhn/Chinook/Santa Ana) | WindDirection 150-210° + Temperature rising + Humidity dropping + Pressure rising simultaneously |

The warm-dry-wind detection is generalized beyond Alpine Föhn: any southerly warm dry wind with a rising pressure signature qualifies. The Föhn trigger contributes +2 to the severity score.

#### Severity mapping

| Score | Severity | Confidence |
|-------|----------|------------|
| 1 primary trigger alone | Mild | Medium |
| 1 primary + 1 secondary, or Föhn alone | Moderate | Medium-High |
| 1 primary + 2+ secondary, or primary + Föhn | Severe | High |

`HoursUntil` is derived from the forecast hour where the pressure drop threshold is first crossed. `DurationHours` spans the window where the trigger remains active.

#### Proto message

```protobuf
enum MigraineTrigger {
  MIGRAINE_TRIGGER_UNSPECIFIED = 0;
  MIGRAINE_TRIGGER_PRESSURE_DROP = 1;
  MIGRAINE_TRIGGER_TEMPERATURE_SWING = 2;
  MIGRAINE_TRIGGER_FOEHN = 3;
  MIGRAINE_TRIGGER_HUMIDITY_SWING = 4;
}

message MigraineRisk {
  AlertData alert = 1;
  MigraineTrigger dominant_trigger = 2;
  double pressure_change_hpa = 3;
  double temperature_change_c = 4;
}
```

#### Config

`MigraineConfig : IConfigSection` at `Njord:Features:Health:Migraine`:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| Enabled | bool | false | |
| PressureThreshold6h | double | 5.0 | hPa drop in 6h to trigger |
| PressureThreshold24h | double | 10.0 | hPa drop in 24h to trigger |
| TempSwingThreshold | double | 8.0 | °C change in 12h |
| HumiditySwingThreshold | double | 30.0 | % change in 12h |

---

### SleepQuality

**Output:** Score (sensor 0-100 + BestStart/BestHours for ventilation window)
**Category:** Lifestyle
**Domain:** Forecast + AirQuality
**Default enabled:** off

#### Input

Uses `ForecastAqInput` (existing). Required Forecast params: `Temperature`, `ApparentTemperature`, `Humidity`, `WindGusts`, `Cape`, `Precipitation`, `DewPoint`. Required AirQuality params: `Pm25`. Optional AirQuality params: `BirchPollen`, `GrassPollen`, `Alder`, `Mugwort`, `RagweedPollen`.

#### Evaluation logic

Evaluates overnight comfort for the coming night. The night window defaults to 22:00-06:00 and is configurable.

Base score: 100. Subtract penalties from the worst hour within the night window:

| Factor | Condition | Penalty |
|--------|-----------|---------|
| Tropical heat | Min night temp > 25°C | -30 |
| Warm night | Min night temp > 22°C | -15 |
| Cold night | Min night temp < 5°C | -10 |
| High humidity | Humidity > 80% | -10 |
| Poor air quality | PM2.5 > 25 µg/m³ | -15 |
| Pollen (sensitive) | High pollen + `PollenSensitive` config | -20 |
| Pollen (general) | High pollen, not sensitive | -5 |
| Wind noise | Gusts > 40 km/h | -10 |
| Thunderstorm | CAPE > 1000 J/kg + Precipitation > 0 | -15 |

Score is clamped to 0-100. The dominant penalty determines the `SleepFactor` enum value.

#### Ventilation advice

Compares outdoor conditions against comfort thresholds:

| Condition | Recommendation |
|-----------|---------------|
| Outdoor temp < indoor comfort (22°C) AND DewPoint < 16°C AND Pollen low AND PM2.5 < 15 | `WINDOW_OPEN` |
| Outdoor temp > 30°C or very high humidity | `AC_RECOMMENDED` |
| Otherwise | `WINDOW_CLOSED` |

BestStart/BestHours identify the optimal pre-sleep ventilation window (the hours before `NightStartHour` where open-window conditions are best).

#### Proto message

```protobuf
enum SleepRecommendation {
  SLEEP_RECOMMENDATION_UNSPECIFIED = 0;
  SLEEP_RECOMMENDATION_WINDOW_OPEN = 1;
  SLEEP_RECOMMENDATION_WINDOW_CLOSED = 2;
  SLEEP_RECOMMENDATION_AC = 3;
}

enum SleepFactor {
  SLEEP_FACTOR_UNSPECIFIED = 0;
  SLEEP_FACTOR_HEAT = 1;
  SLEEP_FACTOR_HUMIDITY = 2;
  SLEEP_FACTOR_POLLEN = 3;
  SLEEP_FACTOR_WIND = 4;
  SLEEP_FACTOR_STORM = 5;
  SLEEP_FACTOR_AIR_QUALITY = 6;
  SLEEP_FACTOR_COLD = 7;
}

message SleepQuality {
  ScoreData score = 1;
  SleepRecommendation recommendation = 2;
  SleepFactor dominant_factor = 3;
  double min_night_temperature_c = 4;
}
```

#### Config

`SleepConfig : IConfigSection` at `Njord:Features:Lifestyle:Sleep`:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| Enabled | bool | false | |
| PollenSensitive | bool | false | Amplify pollen penalty |
| NightStartHour | int | 22 | Night window start (UTC) |
| NightEndHour | int | 6 | Night window end (UTC) |

---

### AsthmaRisk

**Output:** Score (sensor 0-100)
**Category:** Health
**Domain:** Forecast + AirQuality
**Default enabled:** off

#### Input

Uses `ForecastAqInput` extended with `BoundaryLayerHeight` (shared requirement with the `cross-domain-intelligence` change). Required Forecast params: `Temperature`, `Humidity`, `BoundaryLayerHeight`. Required AirQuality params: `Pm25`, `Ozone`. Optional AirQuality params: `BirchPollen`, `GrassPollen`, `Alder`, `Mugwort`, `RagweedPollen`.

#### Evaluation logic

Weighted composite of five normalized component scores (each 0-100):

| Component | Weight | Normalization |
|-----------|--------|---------------|
| PM2.5 | 35% | Linear 0→100 over 0-75 µg/m³ |
| Ozone | 25% | Linear 0→100 over 0-180 µg/m³ |
| Pollen | 20% | Max of all species, normalized to 0-100 by species-specific scales |
| Humidity | 10% | U-curve: 0 at 40-60%, rising to 100 at <20% or >80% |
| Boundary layer | 10% | Inverted linear: 0 at 2000m → 100 at 200m |

Composite = Σ(component × weight), then multiplied by amplifiers:

| Amplifier | Condition | Multiplier |
|-----------|-----------|------------|
| Cold air asthma | Temperature < 5°C AND Humidity > 80% | ×1.3 |
| Rapid temp change | > 10°C swing in 6h (from ring buffer) | ×1.2 |

Final score clamped to 0-100. The `DominantFactor` enum reflects which component or amplifier contributed most.

#### Proto message

```protobuf
enum AsthmaDominantFactor {
  ASTHMA_DOMINANT_FACTOR_UNSPECIFIED = 0;
  ASTHMA_DOMINANT_FACTOR_PM25 = 1;
  ASTHMA_DOMINANT_FACTOR_OZONE = 2;
  ASTHMA_DOMINANT_FACTOR_POLLEN = 3;
  ASTHMA_DOMINANT_FACTOR_HUMIDITY = 4;
  ASTHMA_DOMINANT_FACTOR_BOUNDARY_LAYER = 5;
  ASTHMA_DOMINANT_FACTOR_COLD_AIR = 6;
  ASTHMA_DOMINANT_FACTOR_TEMP_CHANGE = 7;
}

message AsthmaRisk {
  ScoreData score = 1;
  AsthmaDominantFactor dominant_factor = 2;
  int32 pm25_component = 3;
  int32 ozone_component = 4;
  int32 pollen_component = 5;
}
```

#### Config

`AsthmaConfig : IConfigSection` at `Njord:Features:Health:Asthma`:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| Enabled | bool | false | |
| Pm25Weight | double | 0.35 | Component weight |
| OzoneWeight | double | 0.25 | Component weight |
| PollenWeight | double | 0.20 | Component weight |
| HumidityWeight | double | 0.10 | Component weight |
| BoundaryLayerWeight | double | 0.10 | Component weight |
| ColdAirAmplifier | bool | true | Enable cold-air amplifier |
| TempChangeAmplifier | bool | true | Enable rapid-temp-change amplifier |

A `AsthmaConfigValidator` ensures the five weights sum to 1.0 (within floating-point tolerance).

---

### Proto Messages

All three messages are added to `weather.proto` as new `oneof feature` cases in `EnrichmentEvent`:

```protobuf
oneof feature {
  // ... existing ...
  // Health: 45-47 (within Alert range for MigraineRisk)
  MigraineRisk migraine_risk = 45;
  
  // Scores: 54-55
  SleepQuality sleep_quality = 54;
  AsthmaRisk asthma_risk = 55;
}
```

Field numbers are illustrative -- actual assignment follows the existing numbering convention in `weather.proto`.

### i18n

All user-facing text is structural:

| Enum | ha-njord translation keys |
|------|--------------------------|
| `MigraineTrigger` | `pressure_drop`, `temperature_swing`, `foehn`, `humidity_swing` |
| `SleepRecommendation` | `window_open`, `window_closed`, `ac` |
| `SleepFactor` | `heat`, `humidity`, `pollen`, `wind`, `storm`, `air_quality`, `cold` |
| `AsthmaDominantFactor` | `pm25`, `ozone`, `pollen`, `humidity`, `boundary_layer`, `cold_air`, `temp_change` |

Entity names use `translation_key` (no hardcoded strings). Each enum value maps to a translation key in ha-njord's `strings.json` and `translations/de.json`. Numeric values (pressure_change_hpa, temperature_change_c, component scores) are passed as-is in entity attributes -- numbers need no translation.

## API Budget Impact

| Feature | Domains | New variables | Extra calls/cycle/location |
|---------|---------|---------------|---------------------------|
| MigraineRisk | Forecast | Pressure, DewPoint (commonly already requested) | 0 |
| SleepQuality | Forecast + AirQuality | CAPE (may be new), PM2.5 (commonly requested) | 0-1 |
| AsthmaRisk | Forecast + AirQuality | BoundaryLayerHeight, Ozone | 0-1 |

Worst case: 1 additional call per cycle per location if the new variables push past a `ceil(variables / 10)` boundary. Shared with the `cross-domain-intelligence` change (both need BoundaryLayerHeight). At 2 locations with 60-minute polling: ~1,440 extra calls/month, well within the 300k free-tier budget.

## Non-goals

- **Medical-grade accuracy.** These are informational scores based on meteorological correlations, not clinical diagnostics. A disclaimer should be shown in the HA integration documentation.
- **Indoor sensor input.** SleepQuality would benefit from indoor temperature and humidity (Phase 3: HA sensor input), but the outdoor-only version is still valuable.
- **Health app integration.** No export to Apple Health, Google Fit, or similar.
- **Per-user health profiles.** Only `PollenSensitive` as a simple boolean. Full allergy profiles are out of scope.

## Dependencies

- **ha-njord-i18n-foundation** -- translation_key pattern must be established before adding new translated entities.
- **cross-domain-intelligence** -- shares the `ForecastAqInput` extension to include `BoundaryLayerHeight`. The input extension should be done once and shared. If cross-domain-intelligence ships first, AsthmaRisk reuses the extended input. If not, this change extends it.
- **score-windows** (optional) -- SleepQuality could populate `windows` for the ventilation window once ScoreData supports `repeated TimeWindow`. Without it, BestStart/BestHours still works.

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Medical liability from users relying on scores | Reputational | Clear disclaimer in docs and HA entity description; features off by default |
| Föhn detection false positives outside mountain regions | UX (unnecessary alerts) | Generalized to "warm dry wind" pattern; configurable thresholds; feature off by default |
| PollDepth 6 requires 6+ polls before MigraineRisk can evaluate | Delayed first evaluation after restart | Expected behavior, documented; first evaluation ~6h after startup |
| AsthmaRisk weight validation rejects user config | Startup failure | Validator emits clear error message naming which weights don't sum to 1.0 |
| Night window in UTC may not match user's local night | Incorrect sleep window | NightStartHour/NightEndHour configurable; documented that values are UTC offsets |
