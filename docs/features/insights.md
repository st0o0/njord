# Insight and Forecast Features

Insight features provide meta-information about the forecast itself -- how confident it is, how models compare, what the seasonal outlook looks like, and agricultural planning data.

## ConfidenceTrend

Tracks how forecast confidence changes over successive poll cycles. When the ensemble temperature spread is narrowing, models are converging and confidence is increasing. When it is widening, uncertainty is growing.

Uses the last 9 poll cycles (PollDepth=8) to compute the trend direction.

**Entity:** `sensor.<location>_confidence_trend`
**Domains:** Ensemble | **Default:** Off

```yaml
Njord__Features__ConfidenceTrend__Enabled: "true"
```

**Automation ideas:**
- Weight alert severity higher when confidence trend is "increasing"
- Display confidence trend alongside weather forecasts on a dashboard
- Delay major weather-dependent decisions when confidence is "decreasing"

---

## ModelComparison

Shows the daily temperature and precipitation forecast from every configured ensemble model side by side. Useful for seeing where models agree (high confidence) and where they diverge.

Requires at least two models configured in `Njord:Weather:Models`.

**Entity:** `sensor.<location>_model_temperature_spread`
**Domains:** Ensemble | **Default:** Off

```yaml
Njord__Features__ModelComparison__Enabled: "true"
Njord__Weather__Models__0: "ecmwf_ifs025_ensemble_mean"
Njord__Weather__Models__1: "ecmwf_aifs025_ensemble_mean"
Njord__Weather__Models__2: "ukmo_global_ensemble_mean_20km"
```

Each configured model also creates its own `weather.<location>_<model>` entity with full forecast data.

::: tip Budget impact
Each additional comparison model multiplies the ensemble API cost. Three models with 31 variables cost about 12 API calls per cycle. Use the [Config Builder](/builder) to check your budget.
:::

---

## SeasonalOutlook

Provides a monthly temperature anomaly and precipitation anomaly outlook for the next 3 months. Based on the Open-Meteo Seasonal API, which delivers ensemble-mean seasonal forecasts.

- **Temperature anomaly**: deviation from the climatological normal in Kelvin
- **Precipitation anomaly**: percentage of the normal monthly precipitation

**Entity:** `sensor.<location>_seasonal_temperature_anomaly`
**Domains:** Seasonal | **Default:** Off

```yaml
Njord__Features__SeasonalOutlook__Enabled: "true"
Njord__Seasonal__ForecastMonths: "3"
```

**Automation ideas:**
- Plan heating/cooling strategy based on the seasonal temperature outlook
- Adjust irrigation schedules based on precipitation outlook
- Display 3-month outlook on a home dashboard

---

## FrontApproaching

Detects approaching weather fronts by monitoring pressure trends, temperature changes, and wind direction shifts. An early warning that conditions are about to change significantly.

**Entity:** `sensor.<location>_front_approaching`
**Domains:** Forecast | **Default:** Off

```yaml
Njord__Features__FrontApproaching__Enabled: "true"
```

**Automation ideas:**
- "Weather change expected in 6 hours" notification
- Prepare for incoming storm systems
- Pair with pressure drop alert for comprehensive front tracking

---

## GrowingDegree

Calculates Growing Degree Days (GDD) -- an agricultural metric that tracks heat accumulation above a base temperature. Plants need a certain number of GDD to reach maturity, flower, or fruit.

**How it works:** For each forecast day, GDD = max(0, mean_temperature - base_temperature). The result accumulates over the forecast horizon.

**Entity:** `sensor.<location>_growing_degree`
**Domains:** Forecast | **Default:** Off

```yaml
Njord__Features__GrowingDegree__Enabled: "true"
Njord__Features__GrowingDegree__BaseTemperature: "5.0"
```

Common base temperatures:
- **5 °C**: General agriculture, grass growth
- **10 °C**: Corn, warm-season crops
- **4.5 °C**: Wheat, cool-season crops

**Automation ideas:**
- Track accumulated GDD for crop planning
- Predict harvest timing based on GDD accumulation rate
- Alert when specific GDD milestones are reached (e.g., 100 GDD = first mowing)

---

## PollenBlast

Predicts sudden pollen explosions that occur when warm, dry, windy weather follows a rain period. During rain, pollen accumulates on plants; when conditions turn dry and warm, it is all released at once.

Uses trend data from the last 8 poll cycles to detect the rain-to-dry transition.

| Condition | Threshold |
|-----------|-----------|
| Temperature | > 15 °C |
| Wind speed | > 1.4 m/s (5 km/h) |
| Pollen increase | > 10 grains/m3 delta |
| Prior rain | Detected in poll history |

**Entity:** `binary_sensor.<location>_pollen_blast_alert`
**Domains:** Forecast + AirQuality | **Default:** Off | **Confidence:** Medium

```yaml
Njord__Features__PollenBlast__Enabled: "true"
Njord__Features__PollenBlast__TemperatureThreshold: "15"
Njord__Features__PollenBlast__WindSpeedThreshold: "1.4"
Njord__Features__PollenBlast__PollenDelta: "10"
```

**Automation ideas:**
- Proactive allergy medication reminder before the blast
- Close windows and switch to recirculated air before pollen spikes
- Alert allergy-sensitive household members

---

## InversionDetection

Detects temperature inversions by comparing temperature at the surface, 80 m and 120 m. Reports strength (`mild`, `strong`, `extreme`) and type (`radiation`, `subsidence`, `frontal`). Inversions trap pollution and fog near the ground.

**Entity:** `sensor.<location>_inversion_detection`
**Domains:** Forecast | **Default:** Off

```yaml
Njord__Features__InversionDetection__Enabled: "true"
```

**Automation ideas:**
- Avoid burning in fireplaces or gardens during a strong inversion
- Expect fog and poor air quality
- Combine with SmogRisk for ventilation decisions

---

## AtmosphericStability

Assesses convection from CAPE, CIN (convective inhibition) and Lifted Index. Reports a class: `stable`, `marginal`, `capped`, `unstable` or `explosive`.

**Entity:** `sensor.<location>_atmospheric_stability`
**Domains:** Forecast | **Default:** Off

```yaml
Njord__Features__AtmosphericStability__Enabled: "true"
```

**Automation ideas:**
- Secure outdoor items when the class is `unstable` or `explosive`
- Cross-check thunderstorm alerts
- Show on a dashboard for paragliding and sailing

---

## CloudCeiling

Estimates the cloud base height from the low, mid and high cloud layers. Reports a category: `very_low`, `low`, `moderate`, `high`, `very_high` or `unlimited`.

**Entity:** `sensor.<location>_cloud_ceiling`
**Domains:** Forecast | **Default:** Off

```yaml
Njord__Features__CloudCeiling__Enabled: "true"
```

**Automation ideas:**
- Flight and drone go/no-go indicator
- Plan astronomy sessions when the sky is `unlimited`
- Warn about low ceilings for hill walking
