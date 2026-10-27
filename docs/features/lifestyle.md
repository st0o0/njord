# Lifestyle Features

Lifestyle features help you make daily decisions -- from commuting to gardening to laundry. They translate raw weather data into actionable advice.

## Commute

Rates how safe and comfortable your commute will be based on rain, wind, temperature, humidity, and ice risk. The result is a score from 0 (dangerous) to 100 (perfect conditions).

**Entity:** `sensor.<location>_commute_safety` (score 0--100)
**Domains:** Weather | **Default:** On

**Automation ideas:**
- Morning TTS announcement: "Commute safety is 45 -- take extra care on the road"
- Adjust departure time when score is below 50
- Display on a bedside dashboard

---

## Exercise

Advises whether outdoor exercise is safe, considering heat stress, humidity, air quality (PM2.5 and ozone), and UV exposure. Reports a recommendation: ideal, moderate, caution, or avoid.

**Entity:** `sensor.<location>_exercise`
**Domains:** Weather + AirQuality | **Default:** Off

```yaml
Njord__Features__Lifestyle__Exercise__Enabled: "true"
```

**Automation ideas:**
- Suggest indoor workout when rating is "avoid"
- Pair with a fitness tracker to recommend outdoor activity windows
- Display exercise advisory on morning dashboard

---

## Laundry

Scores how well your laundry will dry outdoors today (0--100). Considers temperature, wind, humidity, sunshine, rain probability, and cloud cover. A score above 70 means good drying conditions.

**Entity:** `sensor.<location>_laundry` (score 0--100)
**Domains:** Weather | **Default:** Off

```yaml
Njord__Features__Lifestyle__Laundry__Enabled: "true"
```

**Automation ideas:**
- Morning notification: "Great laundry day! Score: 85"
- Automate a reminder to bring laundry in when rain probability spikes

---

## Outdoor

An overall outdoor activity score (0--100) combining temperature, wind, rain, cloud cover, UV, and air quality. Think of it as "how pleasant is it outside right now?"

**Entity:** `sensor.<location>_outdoor` (score 0--100)
**Domains:** Weather + AirQuality | **Default:** Off

```yaml
Njord__Features__Lifestyle__Outdoor__Enabled: "true"
```

---

## Bbq

Scores barbecue weather (0--100). Considers temperature (warm enough?), wind (grill management), rain probability, and cloud cover. A score above 70 means fire up the grill.

**Entity:** `sensor.<location>_bbq` (score 0--100)
**Domains:** Weather | **Default:** Off

```yaml
Njord__Features__Lifestyle__Bbq__Enabled: "true"
```

**Automation ideas:**
- Weekend notification: "BBQ score for Saturday: 92!"
- Compare scores across the next few days to pick the best day

---

## NightVentilation

Advises when nighttime temperatures are cool enough to ventilate your home for passive cooling. Useful in summer when you want to avoid air conditioning.

**Entity:** `binary_sensor.<location>_night_ventilation`
**Domains:** Weather | **Default:** Off

```yaml
Njord__Features__Lifestyle__NightVentilation__Enabled: "true"
```

**Automation ideas:**
- Automatically open motorized windows when ventilation is recommended
- Send a "time to open windows" notification at sunset

---

## Ventilation

Finds the best time window to air out rooms, considering temperature, rain, PM2.5, and pollen. Avoids recommending ventilation during high-pollen or high-pollution periods.

**Entity:** `binary_sensor.<location>_ventilation_window`
**Domains:** Weather + AirQuality | **Default:** Off

```yaml
Njord__Features__Lifestyle__Ventilation__Enabled: "true"
```

**Automation ideas:**
- "Open windows now" notification when the ventilation window starts
- Pair with indoor CO2 sensor to prioritize ventilation when CO2 is high

---

## Gardening

Advises on gardening conditions based on temperature (not too hot, not freezing), wind (not too strong for delicate plants), and rain (save the watering).

**Entity:** `binary_sensor.<location>_planting_safe`
**Domains:** Weather | **Default:** Off

```yaml
Njord__Features__Lifestyle__Gardening__Enabled: "true"
```

**Automation ideas:**
- Morning notification on good gardening days
- Disable automatic irrigation when rain is expected

---

## SoilMoisture

Reports the topsoil (0--1 cm) moisture status. Tells you whether the ground is too dry (needs watering), optimal, or too wet (stay off the lawn).

| Status | Moisture Level | Meaning |
|--------|---------------|---------|
| Dry | < 0.15 m3/m3 | Needs watering |
| Optimal | 0.15 -- 0.35 | Good condition |
| Wet | > 0.35 m3/m3 | Too wet, avoid foot traffic |

**Entity:** `sensor.<location>_soil_moisture`
**Domains:** Weather | **Default:** Off

```yaml
Njord__Features__Lifestyle__SoilMoisture__Enabled: "true"
Njord__Features__Lifestyle__SoilMoisture__DryThreshold: "0.15"
Njord__Features__Lifestyle__SoilMoisture__WetThreshold: "0.35"
```

**Automation ideas:**
- Trigger garden irrigation when status is "dry" for 24+ hours
- Display soil status on garden dashboard
- Warn lawn care service when soil is too wet

---

## Evapotranspiration

Reports daily water loss from the ground through evaporation and plant transpiration (in mm/day). Guides how much irrigation your garden or crops need to replace lost moisture.

| Level | Daily ET0 | Meaning |
|-------|-----------|---------|
| Low | < 2 mm | Minimal watering needed |
| Moderate | 2 -- 5 mm | Normal watering |
| High | > 5 mm | Heavy irrigation needed |

**Entity:** `sensor.<location>_evapotranspiration`
**Domains:** Weather | **Default:** Off

```yaml
Njord__Features__Lifestyle__Evapotranspiration__Enabled: "true"
Njord__Features__Lifestyle__Evapotranspiration__LowMm: "2.0"
Njord__Features__Lifestyle__Evapotranspiration__HighMm: "5.0"
```

**Automation ideas:**
- Set irrigation duration proportional to daily ET0
- Dashboard chart showing water balance (rain minus evapotranspiration)

---

## SnowDepthWatch

Reports snow accumulation depth. Useful for winter road maintenance, ski conditions, and school closures.

| Level | Default Threshold | Meaning |
|-------|------------------|---------|
| None | 0 cm | No snow cover |
| Light | > 2 cm | Light dusting |
| Moderate | > 10 cm | Significant snow |
| Heavy | > 25 cm | Heavy snow cover |
| Extreme | > 50 cm | Exceptional accumulation |

**Entity:** `sensor.<location>_snow_depth_watch`
**Domains:** Weather | **Default:** Off

```yaml
Njord__Features__Weather__SnowDepthWatch__Enabled: "true"
Njord__Features__Weather__SnowDepthWatch__LightThreshold: "2"
Njord__Features__Weather__SnowDepthWatch__ModerateThreshold: "10"
Njord__Features__Weather__SnowDepthWatch__HeavyThreshold: "25"
Njord__Features__Weather__SnowDepthWatch__ExtremeThreshold: "50"
```

**Automation ideas:**
- Trigger snow plow or salt spreader scheduling
- Notify about school closure risk when depth exceeds "heavy"

---

## ElevatedWind

Reports wind speed and direction at 80 m and 120 m hub height -- the altitudes where wind turbines operate. Useful for monitoring small wind installations or assessing wind energy potential.

**Entity:** `sensor.<location>_elevated_wind`
**Domains:** Weather | **Default:** Off

```yaml
Njord__Features__Energy__ElevatedWind__Enabled: "true"
```

**Automation ideas:**
- Track wind energy production potential
- Compare 80 m wind to 10 m ground wind on a dashboard
- Alert when hub-height wind exceeds turbine cut-off speed
