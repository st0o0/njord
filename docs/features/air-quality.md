# Air Quality Features

njord monitors air quality using the Open-Meteo Air Quality API. Features in this category track particulate matter, pollen, and atmospheric pollution.

## Pollen

Warns when pollen concentrations reach levels that affect allergy sufferers. Checks birch, grass, alder, and mugwort pollen and alerts on the highest reading.

| Severity | Threshold (grains/m3) | Meaning |
|----------|-----------------------|---------|
| Yellow | 20 | Moderate pollen |
| Orange | 50 | High pollen |
| Red | 100 | Very high pollen |

**Entity:** `binary_sensor.<location>_pollen_alert`
**Domains:** AirQuality | **Default:** On

```yaml
Njord__Features__AirQuality__Pollen__Enabled: "true"
```

**Automation ideas:**
- Close windows when pollen is high
- Remind to take allergy medication
- Turn on air purifier

---

## Pm25

Warns when fine particulate matter (PM2.5) exceeds the WHO air quality guideline. PM2.5 particles penetrate deep into the lungs and are linked to respiratory and cardiovascular disease.

| Severity | Threshold (ug/m3) | Meaning |
|----------|-------------------|---------|
| Yellow | 15 | Above WHO guideline |
| Orange | 25 | Unhealthy for sensitive groups |
| Red | 50 | Unhealthy for everyone |

**Entity:** `binary_sensor.<location>_pm2_5_alert`
**Domains:** AirQuality | **Default:** On

**Automation ideas:**
- Switch ventilation to recirculate mode
- Display air quality on a dashboard
- Send health advisory notifications

---

## Dust

Warns about Saharan dust events that can significantly degrade air quality, especially in southern Europe and the Mediterranean.

| Severity | Default Threshold (ug/m3) | Meaning |
|----------|--------------------------|---------|
| Yellow | 50 | Elevated dust |
| Orange | 150 | High dust |
| Red | 500 | Severe dust storm |

**Entity:** `binary_sensor.<location>_dust_alert`
**Domains:** AirQuality | **Default:** Off

```yaml
Njord__Features__AirQuality__Dust__Enabled: "true"
Njord__Features__AirQuality__Dust__Thresholds: "[50, 150, 500]"
```

---

## PollenTrend

Tracks how pollen concentrations are changing over multiple poll cycles. Reports whether pollen is rising, stable, or falling. Useful for planning outdoor activities in the coming days.

**Entity:** `sensor.<location>_pollen_trend`
**Domains:** AirQuality | **Default:** Off

```yaml
Njord__Features__AirQuality__PollenTrend__Enabled: "true"
```

---

## SmogRisk

Warns when atmospheric conditions trap pollution near the ground. Combines two signals:

1. **Boundary layer height** below 500 m (from the Weather API) -- the atmospheric mixing layer is shallow, trapping pollutants
2. **PM2.5** above 25 ug/m3 (from the Air Quality API) -- pollution is already elevated

Both conditions must be present for the alert to fire.

**Entity:** `binary_sensor.<location>_smog_risk_alert`
**Domains:** Weather + AirQuality | **Default:** Off | **Confidence:** Medium

```yaml
Njord__Features__AirQuality__SmogRisk__Enabled: "true"
Njord__Features__AirQuality__SmogRisk__BoundaryLayerThresholdM: "500"
Njord__Features__AirQuality__SmogRisk__Pm25ThresholdUgm3: "25"
```

**Automation ideas:**
- Keep windows closed during smog events
- Activate indoor air filtration
- Warn joggers and cyclists to exercise indoors

---

## RagweedPollen

Adds a ragweed pollen concentration sensor. Ragweed is a major allergen in North America and is increasingly found in parts of Europe. Follows the same pattern as the existing birch, grass, alder, and mugwort pollen sensors.

**Entity:** `sensor.<location>_ragweed_pollen`
**Domains:** AirQuality | **Default:** Off

```yaml
Njord__Features__AirQuality__RagweedPollen__Enabled: "true"
```

---

## GranularAqi

Adds five per-pollutant European Air Quality Index sensors. While the main `european_aqi` sensor gives an overall rating, these sub-indices show which specific pollutant is driving poor air quality.

| Sensor | What it measures |
|--------|-----------------|
| `sensor.<location>_european_aqi_pm2_5` | PM2.5 contribution to AQI |
| `sensor.<location>_european_aqi_pm10` | PM10 contribution |
| `sensor.<location>_european_aqi_nitrogen_dioxide` | NO2 contribution |
| `sensor.<location>_european_aqi_ozone` | Ozone contribution |
| `sensor.<location>_european_aqi_sulphur_dioxide` | SO2 contribution |

**Domains:** AirQuality | **Default:** Off

```yaml
Njord__Features__AirQuality__GranularAqi__Enabled: "true"
```

**Automation ideas:**
- Display a breakdown dashboard showing which pollutant is worst today
- Alert specifically on high ozone days (avoid afternoon outdoor activity)
- Track NO2 near busy roads
