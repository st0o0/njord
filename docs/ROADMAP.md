# Feature Roadmap

Explored 2026-10-20. Features ranked by automation value x feasibility, grouped into tiers by API cost.
All features are a `FeatureRule<TInput>` in `Njord.Features/<OutputKind>/<Feature>/` with their own
config (`Enabled` + thresholds), a `FeatureDescriptor` (incl. `PollDepth` for look-back), a typed
result and proto message, a `docs/data/features.json` entry, and a ha-njord entity.

## Tier 1 — Free (no new API calls)

These use variables already fetched by enabled features. Zero marginal budget cost.

### Best-Window Finder

> Planning:BestWindow · Score · Weather

Find the best contiguous N-hour window in the next 4 days for a configurable activity.

- Slide a window over the hourly `LocationForecast` and score each position per activity profile
  (rain probability, wind, temperature range, UV — all already in `WeatherPoint`)
- Activity profiles in a new `BestWindowOptions` section (or one feature per profile)
- Break ties toward earlier or daylight hours
- Output: start, end, score — reuse the `BestWindow` pattern from BBQ/Laundry/Outdoor
- Lives in `Njord.Features/Scores/`

### Forecast Confidence

> Insight:Confidence · Score · Weather

Ensemble spread (p90 − p10 or stddev) as a 0-100 confidence per day and variable.

- Needs ensemble member data through the mapping layer — today only the mean survives
  `WeatherResponseMappings`; either map percentiles from the API (`temperature_2m_p10/p90`) or
  request individual members and compute spread in the feature
- Normalized per variable (1 K spread for temperature ≠ 1 mm spread for rain)
- Foundation for other features (drift tracker, pattern classifier can reuse the score)
- Lives in `Njord.Features/Scores/`

### Plant Frost Protection

> Garden:PlantFrost · Alert · Weather

Radiative frost detection — more precise than the plain Frost alert for gardeners.

- Trigger: temperature near 0 °C AND clear sky (low cloud cover) AND calm wind AND dew point
  below freezing — conditions where radiative cooling drops ground temperature well below air temp
- New `PlantFrostAlert : FeatureRule<WeatherInput>` with a `PlantFrostConfig` for thresholds
- Lead-time attribute ("cover plants by 20:00") from the first hour meeting all conditions
- All required variables (`temperature_2m`, `dew_point_2m`, `cloud_cover`, `wind_speed_10m`) already fetched
- Lives in `Njord.Features/Alerts/`

### Storm Prep

> Safety:StormPrep · Alert · Weather

Compound alert with ETA for automated retraction of awnings, shutters, mower parking.

- Combine Storm (wind gust threshold) + Thunderstorm (probability + CAPE if added) into a single
  alert with an explicit `StartsIn` lead time
- Severity by overlap: gust-only = moderate, gust + thunderstorm = severe
- New `StormPrepAlertRule`, or a composite that reads two existing rule outputs
- The lead time makes this HA-actionable: `trigger: state_attr('sensor.storm_prep', 'starts_in') < 120`
- Lives in `Njord.Features/Alerts/`

### Mold / Condensation Risk

> Home:Mold · Score · Weather

Dew-point comparison for ventilation and dehumidifier control.

- Compare outdoor dew point against a configurable indoor assumption (default 20 °C, 50 % RH)
- When outdoor dew point approaches indoor surface temperature, condensation risk rises
- 0-100 score with a "ventilate" / "do not ventilate" / "dehumidify" condition
- Template: `VentilationWindow` logic
- Lives in `Njord.Features/Scores/`

### Heating Degree Days

> Energy:DegreeDays · Forecast · Weather + Seasonal

HDD/CDD forecast for energy planning and heat pump scheduling.

- HDD = Σ max(0, base − hourly mean) per day, base temperature configurable (default 15.5 °C)
- 4-day hourly forecast from Weather, monthly estimate from Seasonal (`temperature_2m_anomaly`
  applied to a climatological normal or the month mean from `temperature_2m_mean`)
- Output: daily HDD/CDD array + a monthly outlook
- Lives in `Njord.Features/Forecasts/`

---

## Tier 2 — Cheap (adds variables to the Weather block)

These add 2-4 variables to the weather request. Current count is ~31 (4 calls at `ceil(vars/10)`);
adding up to ~35 stays at 4 calls. Beyond 40 it becomes 5.

### Irrigation Advisor

> Garden:Irrigation · Condition · Weather

Skip/run/deficit decision for smart sprinkler systems.

- Needs `et0_fao_evapotranspiration` and `soil_moisture_0_to_1cm` added to `WeatherParam` flags
- 48-hour water balance: ET0 (evaporative demand) minus precipitation, plus soil moisture state
- Output: skip (enough water), run (deficit), with a deficit-mm estimate
- Needs a short look-back: `PollDepth` > 0 and `History` for yesterday's rain accumulation
- Lives in `Njord.Features/Conditions/`

### Blind / Shading Control

> Comfort:Shading · Condition · Weather + Solar

Automated blind and awning control based on solar radiation and gusts.

- Needs `direct_radiation` (or reuse SolarYield GTI with tilt=90° and the user's azimuth)
- Close when direct radiation on the facade exceeds a threshold AND temperature is above comfort
- Open when cloud cover blocks the sun or temperature drops
- Retract awnings when `wind_gusts_10m` exceeds a safety threshold
- Configurable azimuth per blind group in `ShadingOptions`
- Lives in `Njord.Features/Conditions/`

### EV / Battery Charge Window

> Energy:ChargeWindow · Score · Weather + Solar

Best solar window of tomorrow for EV or home battery charging.

- Derived from SolarYield output: find the contiguous window with the highest expected kWh
- Compare against a "charge from grid" threshold (overcast day → charge overnight on cheap tariff)
- Output: best start/end, expected kWh, recommendation (solar/grid)
- Reuses `SolarIrradianceBlend` and `BestWindow` scoring pattern
- Lives in `Njord.Features/Scores/`

### Mowing Window

> Garden:Mowing · Condition · Weather

Safe-to-mow decision for robot mower scheduling.

- Rain-free window of at least 3 hours (no recent precipitation, none forecast)
- Grass dry: relative humidity below threshold, no precipitation in the last 3 hours
- Wind below a configurable limit (clippings scatter)
- Use `History` (`PollDepth` 1) for the "last 3h rain" look-back
- Output: ok / wet / blocked with a next-ok-at estimate
- Lives in `Njord.Features/Conditions/`

---

## Tier 3 — Cross-Domain Compound

These combine data from multiple domains. No new fetches if both domains are already enabled;
the feature's `Requirements` gate evaluation.

### Outdoor Exertion Safety

> Insight:ExertionSafety · Score · Weather + AirQuality

Heat stress × air pollution interaction score for runners and cyclists.

- Heat load: apparent temperature, humidity, UV index (Weather)
- Pollution load: PM2.5, ozone, NO₂ (AirQuality)
- The penalty is multiplicative: moderate heat + moderate ozone is worse than either alone
- Per-hour score, then pick the minimum-risk 2-hour window in the next 24 hours
- Output: 0-100 safety score + best window
- Lives in `Njord.Features/Scores/`

### Allergy Amplifier

> AirQuality:AllergyAmplifier · Condition · AirQuality + Weather

Effective pollen exposure adjusted for weather conditions.

- Raw pollen count × dispersal factor (dry wind lifts pollen) − washout factor (rain)
- Thunderstorm asthma: high humidity + storm bursts pollen grains → spike
- Trend from `History`: rising / stable / falling effective exposure
- Output: effective exposure level + "relief expected at HH:MM"
- Extends or replaces `PollenTrend`
- Lives in `Njord.Features/Conditions/`

### Allergy-Aware Ventilation

> AirQuality:SafeToVent · Condition · AirQuality + Weather

Composite "safe to open windows" check combining air quality and weather.

- Gate on: pollen count, PM2.5, PM10, dust (AQ) + humidity, wind, rain (Weather)
- Output: safe / caution / close-windows, with a next-safe-at estimate
- Triggers: close windows → run air purifier / HVAC recirculation; open when clean
- Combines existing Ventilation, Pollen, and PM2.5 outputs into one feature
- Lives in `Njord.Features/Conditions/`

### Coastal Flood Compound

> Flood:CoastalFlood · Alert · Flood + Marine + Weather

Multi-stressor flood alert: river + tide + storm surge overlap.

- River discharge above P75 (Flood) + high sea level / waves (Marine) + onshore wind + heavy rain
  (Weather) — the dangerous case where the river cannot drain
- Severity by stressor count in a shared time window
- Needs `sea_level_height_msl` from Marine API — **verify availability first**
- Hard: three-domain alignment by hour, threshold calibration
- Lives in `Njord.Features/Alerts/`

---

## Tier 4 — Predictive

Simpler than they look: `History` (bounded by the feature's `PollDepth`) already gives multi-poll
deltas, and most of these need only the current forecast's hourly series.

### Forecast Drift Tracker

> Insight:ForecastDrift · Trend · Weather

"Saturday rain probability rose 20 points since the last update."

- 2-poll version (easy): `PollDepth` 1, compare current vs `History[0]` daily aggregates per target
  day, emit rising / stable / falling per variable
- Full version (medium): `PollDepth` 8, compute slope by linear regression over `History`
  (the `FeatureEngine` ring does not survive restarts)
- The 2-poll version ships first; raising `PollDepth` is additive
- Lives in `Njord.Features/Trends/`

### Weather Pattern Classifier

> Insight:Pattern · Condition · Weather

Human-readable synoptic label: "stable high pressure", "unsettled westerly", "cold front approaching".

- Rule-based on the hourly forecast series (no history needed):
  - Pressure level and 24h tendency (rising / falling / steady)
  - Wind direction persistence and speed
  - Diurnal temperature range
  - Precipitation clustering (scattered vs continuous)
- Output: pattern label + confidence + expected duration
- Rules need fixtures and golden-master tests
- Lives in `Njord.Features/Conditions/`

### Seasonal Prep Brief

> Insight:SeasonalBrief · Condition · Seasonal

Monthly action tags from the seasonal outlook.

- Map temperature anomaly + precipitation anomaly to labels: "warmer than normal", "dry month"
- Derive action tags per domain: "irrigate early", "heating: reduced", "frost risk: low"
- Confidence decreases with lead time (month 1 > month 2 > month 3)
- Builds directly on `SeasonalOutlook` output
- Lives in `Njord.Features/Conditions/`

---

## Not on the roadmap

| Idea | Why deferred |
|------|-------------|
| Climatology (Historical API) | New `DomainKind`, ~30 calls/month/location, no concrete use case yet |
| Elevation Correction | One-time call per location, marginal improvement on grid-cell forecasts |
| Climate Change (CMIP6) | Long-horizon, hard to make HA-actionable |
| Self-Calibrating Bias | Needs a truth source beyond satellite radiation; hard |
