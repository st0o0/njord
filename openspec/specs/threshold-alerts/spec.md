# threshold-alerts Specification

## Purpose

Pure evaluation functions that assess multi-model forecast data against configurable thresholds and produce typed alerts with confidence scores, severity levels, and diagnostic attributes. Published as binary/enum sensors per alert type per location.

## Requirements

### Requirement: Alert is a typed record with confidence and severity
An `Alert` SHALL be a record carrying `AlertType` (enum), `Severity` (enum: None/Yellow/Orange/Red), `Confidence` (0.0–1.0, fraction of models agreeing the threshold is crossed), and an `IReadOnlyDictionary<string, object?>` of diagnostic attributes (e.g. expected value, earliest time, worst model). `AlertType` SHALL enumerate: Frost, Heat, Storm, HeavyRain, Uv, Fog, Snow, PressureDrop, Thunderstorm.

#### Scenario: Alert carries all fields
- **WHEN** a frost alert is created with severity Yellow, confidence 0.75, and attributes {expected_low: -2.1, earliest: "2026-07-14T04:00Z"}
- **THEN** the record exposes Type=Frost, Severity=Yellow, Confidence=0.75, and the attributes dictionary

### Requirement: Frost warning evaluates minimum temperature across models
`AlertEvaluator.EvaluateFrost` SHALL accept a `ModelSnapshot`, a location, a threshold (default 0 °C), and a `TimeProvider`. For each model it SHALL scan the hourly forecast series for the next 24 h and find the minimum `temperature_2m`. Confidence SHALL be the fraction of models whose minimum is ≤ threshold. Severity SHALL be Yellow if confidence > 0. Attributes SHALL include `expected_low` (median of minima), `earliest_frost` (earliest time any model predicts ≤ threshold), and `models_agreeing` (count).

#### Scenario: All models predict frost
- **WHEN** 6 models all show min temp ≤ 0 °C in the next 24 h
- **THEN** confidence is 1.0, severity is Yellow, expected_low is the median of the 6 minima

#### Scenario: No model predicts frost
- **WHEN** no model shows min temp ≤ 0 °C
- **THEN** severity is None, confidence is 0.0

#### Scenario: Partial agreement
- **WHEN** 3 of 8 models predict frost
- **THEN** confidence is 0.375, severity is Yellow

### Requirement: Heat warning evaluates apparent temperature max with tiered severity
`AlertEvaluator.EvaluateHeat` SHALL accept a `ModelSnapshot`, a location, tiered thresholds (default [30, 35, 40] °C), and a `TimeProvider`. For each model it SHALL find the maximum `apparent_temperature` in the next 24 h. Severity SHALL be Red if any model exceeds the highest threshold (confidence = fraction ≥ that threshold), Orange if any exceeds the middle threshold, Yellow if any exceeds the lowest. The highest triggered tier wins.

#### Scenario: Extreme heat
- **WHEN** 5 of 8 models predict apparent_temperature_max ≥ 40 °C
- **THEN** severity is Red, confidence is 0.625

#### Scenario: Moderate heat
- **WHEN** all models predict max between 30–35 °C, none ≥ 35
- **THEN** severity is Yellow, confidence is 1.0

### Requirement: Storm warning evaluates wind gusts against threshold
`AlertEvaluator.EvaluateStorm` SHALL accept a `ModelSnapshot`, a location, a gust threshold (default 16.7 m/s ≈ 60 km/h), and a `TimeProvider`. It SHALL scan `wind_gusts_10m` in the next 24 h per model. Confidence is the fraction of models with max gust ≥ threshold. Attributes include `expected_max_gust` (median of max gusts).

#### Scenario: Storm expected
- **WHEN** 6 of 8 models show gusts ≥ 16.7 m/s
- **THEN** severity is Yellow, confidence is 0.75

#### Scenario: No storm
- **WHEN** no model shows gusts ≥ 16.7 m/s
- **THEN** severity is None, confidence is 0.0

### Requirement: Heavy rain warning evaluates hourly and daily precipitation
`AlertEvaluator.EvaluateHeavyRain` SHALL accept a `ModelSnapshot`, a location, an hourly threshold (default 10 mm), a daily threshold (default 25 mm), and a `TimeProvider`. It SHALL check both max hourly `precipitation` in the next 24 h and daily `precipitation_sum` (from both hourly accumulation and `DailyForecastSeries.precipitation_sum`, taking the higher). Confidence is the fraction of models exceeding either threshold. Severity is Yellow for hourly, Orange for daily, Red for both.

#### Scenario: Hourly heavy rain
- **WHEN** 4 of 8 models show an hour with ≥ 10 mm precipitation
- **THEN** severity is Yellow, confidence is 0.5

#### Scenario: Daily heavy rain
- **WHEN** 6 of 8 models show daily sum ≥ 25 mm
- **THEN** severity is Orange, confidence is 0.75

#### Scenario: Daily sum from DailyForecastSeries exceeds threshold
- **WHEN** no single hourly precipitation exceeds hourly threshold, but models' DailyForecastSeries shows precipitation_sum > daily threshold
- **THEN** severity is determined by the daily evaluation

#### Scenario: Daily data unavailable
- **WHEN** DailyForecastSeries is empty or precipitation_sum is not in resolved daily parameters
- **THEN** the daily evaluation uses only hourly accumulation (existing behavior)

### Requirement: UV warning evaluates UV index at WHO levels
`AlertEvaluator.EvaluateUv` SHALL accept a `ModelSnapshot`, a location, and a `TimeProvider`. It SHALL find the maximum `uv_index` across models for the next 24 h from both hourly series and `DailyForecastSeries.uv_index_max` (taking the higher per model). WHO levels: low (0–2), moderate (3–5), high (6–7), very_high (8–10), extreme (11+). Severity maps: low→None, moderate→Yellow, high→Orange, very_high/extreme→Red.

#### Scenario: High UV
- **WHEN** median max UV across models is 7.5
- **THEN** severity is Orange, attribute `uv_level` is "high"

#### Scenario: Low UV
- **WHEN** median max UV is 2.0
- **THEN** severity is None, `uv_level` is "low"

#### Scenario: Daily UV max higher than hourly peak
- **WHEN** hourly UV scan finds max=7 but daily uv_index_max is 9
- **THEN** severity is computed from the daily values (higher)

#### Scenario: Daily UV not available
- **WHEN** uv_index_max is not in resolved daily parameters
- **THEN** UV alert uses only the hourly scan (existing behavior preserved)

### Requirement: Fog risk evaluates combined conditions
`AlertEvaluator.EvaluateFog` SHALL accept a `ModelSnapshot`, a location, and a `TimeProvider`. For each model and each hour in the next 24 h, fog conditions are met when `temperature_2m` − `dew_point_2m` < 2 °C AND `wind_speed_10m` < 3 m/s AND `relative_humidity_2m` > 90 %. Confidence is the fraction of models predicting at least one fog hour. Attributes include `fog_hours` (median count of fog hours).

#### Scenario: Fog likely
- **WHEN** 5 of 8 models predict at least 1 fog hour
- **THEN** severity is Yellow, confidence is 0.625

#### Scenario: No fog risk
- **WHEN** no model meets all 3 conditions in any hour
- **THEN** severity is None, confidence is 0.0

### Requirement: Snow warning evaluates snowfall accumulation
`AlertEvaluator.EvaluateSnow` SHALL accept a `ModelSnapshot`, a location, and a `TimeProvider`. It SHALL sum `snowfall` over the next 24 h per model, and additionally check `DailyForecastSeries.snowfall_sum` (taking the higher per model). Confidence is the fraction of models with sum > 0. Severity is Yellow for any snow, Orange for > 5 cm (median), Red for > 20 cm. Attributes include `expected_accumulation` (median sum) and `freezing_level` (median `freezing_level_height`).

#### Scenario: Light snow
- **WHEN** 4 of 8 models predict snowfall, median sum is 2 cm
- **THEN** severity is Yellow, confidence is 0.5, expected_accumulation is 2.0

#### Scenario: Heavy snow
- **WHEN** 7 of 8 models predict > 20 cm
- **THEN** severity is Red, confidence is 0.875

#### Scenario: Daily snowfall sum increases severity
- **WHEN** hourly accumulation is low but DailyForecastSeries.snowfall_sum is > 5 cm
- **THEN** the daily value is used, producing higher severity

#### Scenario: Daily snowfall not available
- **WHEN** snowfall_sum is not in resolved daily parameters
- **THEN** snow alert uses only the hourly accumulation scan

### Requirement: Pressure drop evaluates rapid pressure change
`AlertEvaluator.EvaluatePressureDrop` SHALL accept a `ModelSnapshot`, a location, a drop threshold (default 5 hPa in 3 h), and a `TimeProvider`. For each model and each 3-hour window in the next 24 h, it SHALL compute the `pressure_msl` delta. Confidence is the fraction of models showing at least one window with a drop ≥ threshold. Attributes include `max_drop` (median of per-model max drops).

#### Scenario: Weather front approaching
- **WHEN** 6 of 8 models show ≥ 5 hPa drop in a 3 h window
- **THEN** severity is Yellow, confidence is 0.75

#### Scenario: Stable pressure
- **WHEN** no model shows ≥ 5 hPa drop
- **THEN** severity is None, confidence is 0.0

### Requirement: Thunderstorm warning evaluates combined instability indicators
`AlertEvaluator.EvaluateThunderstorm` SHALL accept a `ModelSnapshot`, a location, and a `TimeProvider`. For each model, thunderstorm conditions exist when `cape` > 1000 J/kg AND `precipitation` > 5 mm AND `wind_gusts_10m` > 15 m/s in any hour in the next 24 h. Confidence is the fraction of models meeting all 3 conditions. Severity: None (confidence=0), Yellow (confidence < 0.5), Orange (0.5–0.75), Red (> 0.75).

#### Scenario: Thunderstorm likely
- **WHEN** 6 of 8 models meet all 3 conditions
- **THEN** severity is Red, confidence is 0.75

#### Scenario: No thunderstorm risk
- **WHEN** no model meets all 3 conditions
- **THEN** severity is None, confidence is 0.0

### Requirement: AlertResult aggregates all alerts for a location
`AlertResult` SHALL be a record holding a location and a list of `Alert` records. It SHALL expose a `ToMqttMessages(baseTopic, location)` method producing one `MqttMessage` per alert type on topic `{baseTopic}/{location}/alerts/{alert_type}`. The payload SHALL be a flat JSON with `severity`, `confidence`, and all diagnostic attributes. Retain flag SHALL be `true`.

#### Scenario: Serialization to MQTT messages
- **WHEN** an AlertResult with 9 alerts is serialized
- **THEN** 9 MqttMessages are produced, one per alert type

#### Scenario: None severity still publishes
- **WHEN** a frost alert has severity None
- **THEN** an MqttMessage is still published with `{"severity":"none","confidence":0.0}` so HA entities stay current

### Requirement: Alert thresholds are configurable
All alert thresholds SHALL be configurable via `AlertThresholdOptions` bound from `NjordOptions.Enrichment.Alerts`. Defaults: frost 0 °C, heat [30,35,40] °C, storm 16.7 m/s, heavy rain hourly 10 mm / daily 25 mm, pressure drop 5 hPa. An `Enabled` flag (default `true`) SHALL gate the entire alert consumer.

#### Scenario: Custom frost threshold
- **WHEN** `AlertThresholdOptions.FrostThreshold` is set to -5.0
- **THEN** the frost evaluator uses -5.0 instead of the default 0.0

#### Scenario: Alerts disabled
- **WHEN** `AlertThresholdOptions.Enabled` is `false`
- **THEN** no alert consumer stream is materialized

### Requirement: Alert topics use the alerts segment
Alert topics SHALL follow the pattern `{baseTopic}/{location}/alerts/{alert_type}` where `alert_type` is the kebab-case alert type name (e.g. `frost`, `heavy-rain`, `thunderstorm`). The device id SHALL be `njord_{location}_alerts`.

#### Scenario: Alert topic format
- **WHEN** baseTopic is "njord", location is "lucerne", alert type is HeavyRain
- **THEN** the topic is "njord/lucerne/alerts/heavy-rain"

#### Scenario: Alert device id
- **WHEN** location is "lucerne"
- **THEN** the device id is "njord_lucerne_alerts"

### Requirement: Discovery payload for the alerts device
When `DiscoveryEnabled` is `true` and alerts are enabled, one retained device-based discovery payload SHALL be published per location for the alerts device. Each alert type SHALL be a `binary_sensor` component (on when severity > None) with JSON attributes for severity, confidence, and diagnostics.

#### Scenario: Alert discovery component
- **WHEN** the alerts discovery payload for lucerne is built
- **THEN** it contains 9 binary_sensor components (frost, heat, storm, heavy_rain, uv, fog, snow, pressure_drop, thunderstorm)

#### Scenario: Binary sensor is on when alert is active
- **WHEN** the frost alert has severity Yellow
- **THEN** the binary_sensor value template evaluates to "ON"
