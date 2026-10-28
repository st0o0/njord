# Weather Alerts

njord includes 22 weather alert features. Each creates a `binary_sensor` in Home Assistant that turns **on** when the condition is detected, and carries severity, confidence, and timing attributes. Alerts fire on the deterministic forecast from the Forecast API and optionally use the Ensemble API for confidence.

## How Weather Alerts Work

Every alert scans the forecast horizon (up to 4 days by default) looking for conditions that cross a threshold. When found, it reports:

- **Severity**: `yellow`, `orange`, or `red` depending on intensity
- **Confidence**: `high`, `medium`, or `low` (requires the Ensemble domain)
- **Timing**: `hours_until` the event and `duration_hours` of the event
- **Values**: `trigger_value` (what was detected) and `threshold` (what the limit is)

::: tip
All thresholds use metric units: Celsius for temperature, m/s for wind, mm for rain, hPa for pressure, metres for visibility.
:::

## Alert Entity Pattern

Each alert creates:
- `binary_sensor.<location>_<name>_alert` -- on/off with attributes
- One shared `event.<location>_weather_alert` for alert lifecycle events (started, escalated, de-escalated, cleared)

---

## Temperature Alerts

### Frost

Warns when the forecast temperature drops below freezing. Critical for protecting pipes, plants, and planning winter driving.

| Severity | Default Threshold | Meaning |
|----------|------------------|---------|
| Yellow | 0 °C | Light frost expected |
| Orange | -5 °C | Moderate frost |
| Red | -15 °C | Severe frost |

**Entity:** `binary_sensor.<location>_frost_alert`
**Domains:** Forecast + Ensemble | **Default:** On | **Confidence:** From ensemble spread

```yaml
Njord__Features__Forecast__Frost__Enabled: "true"
Njord__Features__Forecast__Frost__Thresholds: "[0, -5, -15]"
```

**Automation ideas:**
- Turn on pipe heating when frost alert turns on
- Send a notification to cover tender plants
- Adjust thermostat setpoint when orange or red severity

---

### Heat

Warns when air or apparent temperature exceeds dangerous levels. Uses the higher of actual and feels-like temperature.

| Severity | Default Threshold | Meaning |
|----------|------------------|---------|
| Yellow | 30 °C | Hot day |
| Orange | 35 °C | Dangerous heat |
| Red | 40 °C | Extreme heat |

**Entity:** `binary_sensor.<location>_heat_alert`
**Domains:** Forecast + Ensemble | **Default:** On | **Confidence:** From ensemble spread

```yaml
Njord__Features__Forecast__Heat__Thresholds: "[30, 35, 40]"
```

**Automation ideas:**
- Close blinds and awnings when heat alert fires
- Activate cooling systems
- Remind to hydrate when orange severity

---

### WetBulbDanger

Warns when the wet-bulb temperature -- a measure combining heat and humidity -- reaches levels where the human body cannot cool itself. This is the most medically significant heat metric: heat stroke can occur even in shade.

| Severity | Default Threshold | Meaning |
|----------|------------------|---------|
| Yellow | 26 °C | Uncomfortable, limit strenuous activity |
| Orange | 28 °C | Dangerous for outdoor work |
| Red | 32 °C | Life-threatening, avoid all outdoor exertion |

**Entity:** `binary_sensor.<location>_wet_bulb_danger_alert`
**Domains:** Forecast + Ensemble | **Default:** Off | **Confidence:** From ensemble spread

```yaml
Njord__Features__Forecast__WetBulbDanger__Enabled: "true"
Njord__Features__Forecast__WetBulbDanger__Thresholds: "[26, 28, 32]"
```

**Automation ideas:**
- Cancel outdoor exercise plans when wet-bulb exceeds 28 °C
- Alert elderly household members at yellow severity
- Trigger emergency cooling at red severity

---

### TropicalNight

Warns when the nighttime low stays above 20 °C, making sleep difficult. Checked only during dark hours.

| Severity | Default Threshold | Meaning |
|----------|------------------|---------|
| Yellow | 20 °C | Warm night |
| Orange | 23 °C | Uncomfortably hot night |
| Red | 25 °C | Tropical night |

**Entity:** `binary_sensor.<location>_tropical_night_alert`
**Domains:** Forecast + Ensemble | **Default:** On | **Confidence:** From ensemble spread

```yaml
Njord__Features__Forecast__TropicalNight__Thresholds: "[20, 23, 25]"
```

**Automation ideas:**
- Start bedroom fan before bedtime when alert fires
- Send a bedtime reminder to open windows
- Track tropical nights per month for health monitoring

---

### WindChill

Warns when the apparent temperature drops to dangerous wind-chill levels. Relevant in cold, windy conditions.

| Severity | Default Threshold | Meaning |
|----------|------------------|---------|
| Yellow | -10 °C | Cold, dress warmly |
| Orange | -20 °C | Frostbite risk |
| Red | -30 °C | Extreme danger |

**Entity:** `binary_sensor.<location>_wind_chill_alert`
**Domains:** Forecast + Ensemble | **Default:** On | **Confidence:** From ensemble spread

```yaml
Njord__Features__Forecast__WindChill__Thresholds: "[-10, -20, -30]"
```

---

## Wind and Storm Alerts

### Storm

Warns when wind gusts are expected to exceed storm thresholds.

| Severity | Default Threshold | Meaning |
|----------|------------------|---------|
| Yellow | 17 m/s (61 km/h) | Strong wind |
| Orange | 25 m/s (90 km/h) | Storm |
| Red | 33 m/s (119 km/h) | Violent storm |

**Entity:** `binary_sensor.<location>_storm_alert`
**Domains:** Forecast + Ensemble | **Default:** On | **Confidence:** From ensemble spread

```yaml
Njord__Features__Forecast__Storm__GustThresholds: "[17, 25, 33]"
```

**Automation ideas:**
- Retract awnings and close roller shutters
- Send a "secure outdoor furniture" notification
- Disable automatic window opening

---

### Thunderstorm

Detects convective storm potential from CAPE (atmospheric energy), precipitation, and wind gusts. Enhanced with Convective Inhibition (CIN) and Lifted Index for precision.

| Severity | Condition |
|----------|-----------|
| Yellow | CAPE > 1000 J/kg with precipitation > 5 mm |
| Orange | Strong gusts (> 15 m/s) during storm conditions |
| Red | Extreme instability with severe gusts |

**Entity:** `binary_sensor.<location>_thunderstorm_alert`
**Domains:** Forecast + Ensemble | **Default:** On | **Confidence:** From ensemble spread

```yaml
Njord__Features__Forecast__Thunderstorm__CapeThreshold: "1000"
Njord__Features__Forecast__Thunderstorm__PrecipThreshold: "5"
Njord__Features__Forecast__Thunderstorm__GustThreshold: "15"
```

**Automation ideas:**
- Unplug sensitive electronics
- Close windows and skylights
- Postpone outdoor events

---

## Precipitation Alerts

### HeavyRain

Warns when significant rainfall is expected -- either in a single hour or accumulated over 24 hours.

| Severity | Condition |
|----------|-----------|
| Yellow | Hourly > 10 mm or daily > 25 mm |
| Orange | Hourly > 20 mm or daily > 50 mm |
| Red | Hourly > 40 mm or daily > 100 mm |

**Entity:** `binary_sensor.<location>_heavy_rain_alert`
**Domains:** Forecast + Ensemble | **Default:** On | **Confidence:** From ensemble spread

```yaml
Njord__Features__Forecast__HeavyRain__HourlyThreshold: "10"
Njord__Features__Forecast__HeavyRain__DailyThreshold: "25"
```

**Automation ideas:**
- Close roof windows
- Activate sump pump
- Warn about potential basement flooding

---

### RainProbability

Warns when the chance of rain is high in the next few hours. Uses the native `precipitation_probability` from the Forecast API -- a direct ensemble-derived probability, not a threshold on predicted rainfall amount.

| Severity | Default Threshold | Meaning |
|----------|------------------|---------|
| Yellow | 50% | Rain likely |
| Orange | 70% | Rain very likely |
| Red | 90% | Rain almost certain |

**Entity:** `binary_sensor.<location>_rain_probability_alert`
**Domains:** Forecast + Ensemble | **Default:** On | **Confidence:** From ensemble spread

```yaml
Njord__Features__Forecast__RainProbability__WindowHours: "6"
Njord__Features__Forecast__RainProbability__Thresholds: "[50, 70, 90]"
```

**Automation ideas:**
- "Take an umbrella" notification in the morning
- Retract outdoor awnings
- Postpone automatic garden watering

---

### Shower

Warns about sudden convective showers -- intense, localized rainfall that can start abruptly. Different from frontal rain (HeavyRain): showers are harder to predict and often catch people off guard.

| Severity | Default Threshold | Meaning |
|----------|------------------|---------|
| Yellow | 3 mm/h | Light showers |
| Orange | 5 mm/h | Moderate showers |
| Red | 10 mm/h | Heavy downpour |

**Entity:** `binary_sensor.<location>_shower_alert`
**Domains:** Forecast + Ensemble | **Default:** Off | **Confidence:** From ensemble spread

```yaml
Njord__Features__Forecast__Shower__Enabled: "true"
Njord__Features__Forecast__Shower__WindowHours: "6"
Njord__Features__Forecast__Shower__Thresholds: "[3, 5, 10]"
```

---

### Snow

Warns when significant snowfall is expected, considering the freezing level height to confirm snow reaches the ground.

**Entity:** `binary_sensor.<location>_snow_alert`
**Domains:** Forecast + Ensemble | **Default:** On | **Confidence:** From ensemble spread

---

### Ice

Warns about black ice conditions: temperatures near or below freezing combined with rain or wet surfaces. Checks soil temperature when available.

| Severity | Condition |
|----------|-----------|
| Yellow | Temperature below 2 °C with precipitation |
| Orange | Temperature below 0 °C with rain |
| Red | Temperature below -2 °C with wet surfaces |

**Entity:** `binary_sensor.<location>_ice_alert`
**Domains:** Forecast + Ensemble | **Default:** On | **Confidence:** From ensemble spread

```yaml
Njord__Features__Forecast__Ice__Threshold: "2"
```

**Automation ideas:**
- Activate driveway heating
- Morning notification about icy road conditions
- Adjust commute departure time

---

## Atmospheric Alerts

### PressureDrop

Warns when atmospheric pressure drops rapidly -- a reliable indicator of approaching storms and weather fronts.

| Severity | Default Threshold | Meaning |
|----------|------------------|---------|
| Yellow | 5 hPa / 3h | Front approaching |
| Orange | 10 hPa / 3h | Severe weather system |

**Entity:** `binary_sensor.<location>_pressure_drop_alert`
**Domains:** Forecast | **Default:** On | **Confidence:** Medium (pressure is inherently stable)

```yaml
Njord__Features__Forecast__PressureDrop__Threshold: "5"
Njord__Features__Forecast__PressureDrop__SevereThreshold: "10"
```

---

### Fog

Warns when conditions favor fog formation: temperature close to dew point, low wind, and high humidity persisting for several hours.

**Entity:** `binary_sensor.<location>_fog_alert`
**Domains:** Forecast | **Default:** On | **Confidence:** Medium (fog is a local phenomenon)

```yaml
Njord__Features__Forecast__Fog__PersistentHours: "4"
```

**Automation ideas:**
- Turn on fog lights reminder
- Adjust morning alarm earlier for longer commute
- Activate outdoor lighting

---

### Visibility

Warns when forecast visibility drops below safe distances.

| Severity | Default Threshold | Meaning |
|----------|------------------|---------|
| Yellow | 1000 m | Reduced visibility |
| Orange | 200 m | Very poor visibility |
| Red | 50 m | Near-zero visibility |

**Entity:** `binary_sensor.<location>_visibility_alert`
**Domains:** Forecast | **Default:** On | **Confidence:** Medium

```yaml
Njord__Features__Forecast__Visibility__Thresholds: "[1000, 200, 50]"
```

---

### Humidity

Warns when daytime dew point creates muggy, uncomfortable conditions.

| Severity | Default Threshold | Meaning |
|----------|------------------|---------|
| Yellow | 16 °C dew point | Noticeable humidity |
| Orange | 21 °C dew point | Uncomfortable |
| Red | 24 °C dew point | Oppressive |

**Entity:** `binary_sensor.<location>_humidity_alert`
**Domains:** Forecast | **Default:** On | **Confidence:** Medium

```yaml
Njord__Features__Forecast__Humidity__Thresholds: "[16, 21, 24]"
```

---

## UV Alerts

### UV

Warns when the UV index reaches levels that can cause sunburn.

**Entity:** `binary_sensor.<location>_uv_alert`
**Domains:** Forecast | **Default:** On | **Confidence:** Medium (UV depends on solar angle, not ensemble)

---

### ClearSkyUv

Warns about the maximum possible UV index **without clouds**. Even on partly cloudy days, brief sun breaks can deliver intense UV. This alert tells you the UV you would get if the clouds part.

| Severity | Default Threshold | Meaning |
|----------|------------------|---------|
| Yellow | 6 | High UV potential |
| Orange | 8 | Very high UV potential |
| Red | 10 | Extreme UV potential |

**Entity:** `binary_sensor.<location>_clear_sky_uv_alert`
**Domains:** Forecast | **Default:** Off | **Confidence:** Medium

```yaml
Njord__Features__Lifestyle__ClearSkyUv__Enabled: "true"
Njord__Features__Lifestyle__ClearSkyUv__Thresholds: "[6, 8, 10]"
```

**Automation ideas:**
- Remind to apply sunscreen even on cloudy days
- Close UV-sensitive blinds when clear-sky UV is extreme

---

## Cross-Domain Alerts

### FireRisk

Assesses wildfire risk by combining multiple weather factors into a weighted score:

| Factor | Weight | Threshold |
|--------|--------|-----------|
| Temperature | 25% | > 30 °C |
| Low humidity | 25% | < 30% |
| Wind speed | 20% | > 5.6 m/s (20 km/h) |
| Dry soil | 20% | < 0.10 m3/m3 |
| No rain for | 10% | 48+ hours |

| Severity | Combined Score |
|----------|---------------|
| Yellow | > 0.4 |
| Orange | > 0.6 |
| Red | > 0.8 |

**Entity:** `binary_sensor.<location>_fire_risk_alert`
**Domains:** Forecast + Ensemble | **Default:** Off | **Confidence:** From ensemble spread

```yaml
Njord__Features__Lifestyle__FireRisk__Enabled: "true"
Njord__Features__Lifestyle__FireRisk__TemperatureThreshold: "30"
Njord__Features__Lifestyle__FireRisk__HumidityThreshold: "30"
Njord__Features__Lifestyle__FireRisk__WindSpeedThreshold: "5.6"
Njord__Features__Lifestyle__FireRisk__SoilMoistureThreshold: "0.10"
Njord__Features__Lifestyle__FireRisk__DryHours: "48"
```

**Automation ideas:**
- Alert to clear dry brush near property
- Restrict outdoor open flames
- Prepare evacuation checklist at red severity

---

### CoastalFlood

Warns about storm surge risk by combining sea level deviation, wave height, and onshore wind conditions. Only relevant for coastal locations.

| Factor | Default Threshold |
|--------|------------------|
| Sea level deviation | > 0.3 m above normal |
| Wave height | > 1.5 m |
| Onshore wind | > 4.2 m/s (15 km/h) |

**Entity:** `binary_sensor.<location>_coastal_flood_alert`
**Domains:** Marine + Forecast + Ensemble | **Default:** Off | **Confidence:** From ensemble spread

```yaml
Njord__Features__Marine__CoastalFlood__Enabled: "true"
Njord__Features__Marine__CoastalFlood__SeaLevelThresholdM: "0.3"
Njord__Features__Marine__CoastalFlood__WaveHeightThresholdM: "1.5"
Njord__Features__Marine__CoastalFlood__WindSpeedThreshold: "4.2"
```

**Automation ideas:**
- Close flood barriers
- Notify marine and harbor operations
- Activate coastal monitoring cameras

---

### CompoundHeatStress

Warns when heat and air pollution combine into a danger greater than either alone. Fires when any one of these triggers is met:

| Trigger | Condition |
|---------|-----------|
| Wet bulb | Wet-bulb temperature >= 28 °C |
| Heat + PM2.5 | Apparent temperature >= 40 °C and PM2.5 >= 25 ug/m3 |
| UV + stagnant air | UV index >= 8, boundary layer < 500 m and PM2.5 >= 15 ug/m3 |

| Severity | Meaning |
|----------|---------|
| Yellow | Warning: one trigger met |
| Orange | Severe: several triggers met or values well above the limits |
| Red | Extreme: wet-bulb temperature >= 35 °C |

**Entity:** `binary_sensor.<location>_compound_heat_stress_alert`
**Domains:** Forecast + AirQuality | **Default:** Off | **Confidence:** From ensemble spread

```yaml
Njord__Features__Forecast__CompoundHeatStress__Enabled: "true"
```

Thresholds live in the `Njord:Features:Forecast:CompoundHeatStress` section.

**Automation ideas:**
- Keep windows closed and run cooling with filtered air
- Cancel outdoor work and exercise at orange or red severity
- Notify vulnerable household members early

---

### MigraineRisk

Warns when weather patterns known to trigger migraines are forecast. Uses the poll history (ring buffer, PollDepth 6) to judge changes.

| Signal | Condition |
|--------|-----------|
| Pressure drop (primary) | > 5 hPa in 6 h or > 10 hPa in 24 h |
| Temperature swing | > 8 °C in 12 h |
| Humidity swing | > 30 % in 12 h |
| Foehn wind | Foehn conditions detected |

| Severity | Meaning |
|----------|---------|
| Yellow | Mild risk |
| Orange | Moderate risk |
| Red | Severe risk |

**Entity:** `binary_sensor.<location>_migraine_risk_alert`
**Domains:** Forecast | **Default:** Off | **Confidence:** Medium

```yaml
Njord__Features__Forecast__MigraineRisk__Enabled: "true"
```

Thresholds live in the `Njord:Features:Forecast:MigraineRisk` section.

**Automation ideas:**
- Send a reminder to take preventive medication
- Dim lights and reduce notifications at orange or red severity
- Log alert days next to a symptom diary
