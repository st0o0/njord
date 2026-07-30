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

Frost warning SHALL evaluate temperature from `ConsensusSnapshot.Hourly` consensus medians instead of iterating raw model data. Confidence SHALL be derived from the consensus agreement score.

#### Scenario: All models predict frost
- **WHEN** consensus median temperature is <= 0 degrees C with agreement >= 0.8
- **THEN** a frost alert is produced with high confidence

#### Scenario: No model predicts frost
- **WHEN** consensus median temperature is > 2 degrees C across all horizons
- **THEN** no frost alert is produced

#### Scenario: Partial agreement
- **WHEN** consensus median temperature is <= 0 degrees C but agreement is < 0.5
- **THEN** a frost alert is produced with low confidence

### Requirement: Heat warning evaluates apparent temperature max with tiered severity

Heat warning SHALL use consensus median apparent temperature from `ConsensusSnapshot.Hourly`.

#### Scenario: Extreme heat
- **WHEN** consensus median apparent temperature exceeds the extreme threshold
- **THEN** a heat alert with severity "extreme" is produced

#### Scenario: Moderate heat
- **WHEN** consensus median apparent temperature exceeds the moderate threshold but not extreme
- **THEN** a heat alert with severity "moderate" is produced

### Requirement: Storm warning evaluates wind gusts against threshold
`AlertEvaluator.EvaluateStorm` SHALL accept a `ModelSnapshot`, a location, a gust threshold (default 16.7 m/s ≈ 60 km/h), and a `TimeProvider`. It SHALL scan `wind_gusts_10m` in the next 24 h per model. Confidence is the fraction of models with max gust ≥ threshold. Attributes include `expected_max_gust` (median of max gusts).

#### Scenario: Storm expected
- **WHEN** 6 of 8 models show gusts ≥ 16.7 m/s
- **THEN** severity is Yellow, confidence is 0.75

#### Scenario: No storm
- **WHEN** no model shows gusts ≥ 16.7 m/s
- **THEN** severity is None, confidence is 0.0

### Requirement: Heavy rain warning evaluates hourly and daily precipitation

Heavy rain warning SHALL use hourly precipitation from `ConsensusSnapshot.Hourly` and daily precipitation sum from `ConsensusSnapshot.Daily`.

#### Scenario: Hourly heavy rain
- **WHEN** consensus median hourly precipitation exceeds the threshold
- **THEN** a heavy rain alert is produced

#### Scenario: Daily heavy rain
- **WHEN** consensus median daily precipitation sum exceeds the threshold
- **THEN** a heavy rain alert is produced with daily severity

#### Scenario: Daily sum from DailyConsensus
- **WHEN** `ConsensusSnapshot.Daily` contains `precipitation_sum` consensus
- **THEN** the daily heavy rain evaluation uses that median value directly

### Requirement: UV warning evaluates UV index at WHO levels

UV warning SHALL use daily UV max from `ConsensusSnapshot.Daily`.

#### Scenario: High UV
- **WHEN** consensus median daily `uv_index_max` exceeds the threshold
- **THEN** a UV alert is produced

#### Scenario: Low UV
- **WHEN** consensus median daily `uv_index_max` is below the threshold
- **THEN** no UV alert is produced

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

`AlertEvaluator.EvaluateAll` SHALL accept a `ConsensusSnapshot` instead of `ModelSnapshot`. The location is taken from `ConsensusSnapshot.Location`.

#### Scenario: Serialization to MQTT messages
- **WHEN** alerts are serialized
- **THEN** each alert produces one MQTT message on its sub-topic

#### Scenario: None severity still publishes
- **WHEN** no threshold is exceeded for an alert type
- **THEN** a "none" severity alert is published

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

### Requirement: Alert record serialization
`AlertResult` and `Alert` records SHALL have `[property: JsonProperty("...")]` on all positional parameters producing camelCase wire names. The `Alert` record's `Attributes` dictionary property SHALL retain its `IReadOnlyDictionary<string, object?>` type with a pinned wire name.

#### Scenario: AlertResult round-trips through JSON with pinned wire names
- **WHEN** an `AlertResult` with alerts is serialized to JSON and deserialized back
- **THEN** all properties (Location, Alerts including nested Alert fields: Type, Severity, Confidence, Attributes, TriggerValue, Threshold, PeakValue, HoursUntil, DurationHours) round-trip correctly with camelCase wire names
