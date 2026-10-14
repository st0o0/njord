# activity-indices Specification

## Purpose

Activity and environmental index scores computed by the `Njord.Indices` library from the consensus: one `IIndexScore` class per lifestyle score (laundry drying, outdoor, running, cycling, BBQ, irrigation, solar yield, night ventilation), frost protection and VPD plant stress, aggregated by `IndexAnalyzer` into an `IndexResult` with per-day slices from `DaySlicer` (d0/d1/d2), resolved preferences and score envelopes, and published as per-day MQTT state topics.

## Requirements
### Requirement: Laundry drying score from temperature, humidity, wind, rain, sunshine
`LaundryScore.Compute` SHALL accept mean temperature (°C), mean relative humidity (%), mean wind speed (m/s), mean precipitation probability (%), sunshine percentage (%), and a `ResolvedPreferences`. It SHALL return an `int` score 0–100. The formula SHALL weight: temperature 0.3, humidity 0.25, wind 0.2, rain probability 0.15, sunshine 0.1. Penalty terms SHALL be scaled by the corresponding sensitivity multipliers from `ResolvedPreferences`. Null inputs SHALL use neutral sub-score 50.

#### Scenario: Perfect drying day
- **WHEN** temp is 28°C, humidity 35%, wind 5 m/s, rain prob 0%, sunshine 100%, all sensitivities 1.0
- **THEN** the score is ≥ 90

#### Scenario: Cold rainy day
- **WHEN** temp is 5°C, humidity 90%, wind 1 m/s, rain prob 80%, sunshine 0%, all sensitivities 1.0
- **THEN** the score is ≤ 15

#### Scenario: High humidity sensitivity makes drying score worse
- **WHEN** temp is 20°C, humidity 70%, wind 3 m/s, rain prob 10%, sunshine 60%, HumiditySensitivity 2.0
- **THEN** the score is lower than with HumiditySensitivity 1.0

### Requirement: Outdoor score from temperature comfort, rain, wind, cloud cover
`OutdoorScore.Compute` SHALL accept mean temperature (°C), mean relative humidity (%), mean precipitation probability (%), mean wind speed (m/s), mean cloud cover (%), and a `ResolvedPreferences`. It SHALL return an `int` score 0–100.

Temperature comfort SHALL use a bell curve peaking at `ResolvedPreferences.IdealTemp` (default 22°C). The penalty term SHALL be scaled by `ResolvedPreferences.HeatSensitivity`.

Humidity SHALL be scored such that high humidity (≥80%) significantly reduces the score, scaled by `ResolvedPreferences.HumiditySensitivity`.

Wind SHALL use a bell-curve (breeze score) where the ideal range is 2–4 m/s. Windstill conditions (< 1 m/s) and strong wind (> 8 m/s) SHALL both produce low sub-scores. The penalty SHALL be scaled by `ResolvedPreferences.WindSensitivity`.

Rain probability SHALL be scored with penalty scaled by `ResolvedPreferences.RainSensitivity`.

Sub-score weights SHALL be: temperature 0.25, humidity 0.25, rain 0.15, wind (breeze) 0.20, cloud cover 0.15. Null inputs SHALL use neutral sub-score 50.

#### Scenario: Pleasant spring day
- **WHEN** temp is 22°C, humidity 50%, rain prob 5%, wind 3 m/s, cloud cover 20%, all sensitivities 1.0
- **THEN** the score is ≥ 85

#### Scenario: Stormy winter day
- **WHEN** temp is 2°C, humidity 70%, rain prob 90%, wind 12 m/s, cloud cover 100%, all sensitivities 1.0
- **THEN** the score is ≤ 10

#### Scenario: Hot humid windless day (the schwül fix)
- **WHEN** temp is 33°C, humidity 85%, rain prob 10%, wind 0.5 m/s, cloud cover 30%, all sensitivities 1.0
- **THEN** the score is ≤ 40

#### Scenario: Hot humid windless day with high heat sensitivity
- **WHEN** temp is 33°C, humidity 85%, rain prob 10%, wind 0.5 m/s, cloud cover 30%, HeatSensitivity 1.5, HumiditySensitivity 1.3
- **THEN** the score is ≤ 32

#### Scenario: Ideal outdoor temp shifted to 26°C
- **WHEN** temp is 26°C, humidity 45%, rain prob 5%, wind 3 m/s, cloud cover 20%, IdealTemp 26.0, all sensitivities 1.0
- **THEN** the score is ≥ 85

### Requirement: Running comfort with optimal temperature range
`RunningScore.Compute` SHALL accept mean temperature (°C), mean humidity (%), mean wind speed (m/s), mean precipitation probability (%), and a `ResolvedPreferences`. It SHALL return an `int` score 0–100. Optimal temperature range SHALL be `ResolvedPreferences.IdealTempLow` to `ResolvedPreferences.IdealTempHigh` (defaults 5–20°C). Penalty terms SHALL be scaled by the corresponding sensitivity multipliers. Null inputs SHALL use neutral sub-score 50.

#### Scenario: Ideal running weather
- **WHEN** temp is 12°C, humidity 45%, wind 2 m/s, rain prob 0%, all sensitivities 1.0, IdealTempLow 5, IdealTempHigh 20
- **THEN** the score is ≥ 85

#### Scenario: Hot and humid
- **WHEN** temp is 35°C, humidity 80%, wind 0.5 m/s, rain prob 10%, all sensitivities 1.0
- **THEN** the score is ≤ 20

#### Scenario: Custom running temp range
- **WHEN** temp is 3°C, humidity 50%, wind 2 m/s, rain prob 0%, IdealTempLow 0, IdealTempHigh 15
- **THEN** the score is higher than with default range (5–20°C)

### Requirement: Cycling comfort penalizes wind more heavily
`CyclingScore.Compute` SHALL accept mean temperature (°C), mean humidity (%), mean wind speed (m/s), mean precipitation probability (%), and a `ResolvedPreferences`. It SHALL return an `int` score 0–100. Wind SHALL be weighted 0.3 (vs 0.2 for running). Penalty terms SHALL be scaled by the corresponding sensitivity multipliers. Null inputs SHALL use neutral sub-score 50.

#### Scenario: Calm warm day
- **WHEN** temp is 18°C, humidity 50%, wind 1.5 m/s, rain prob 0%, all sensitivities 1.0
- **THEN** the score is ≥ 85

#### Scenario: Very windy
- **WHEN** temp is 18°C, humidity 50%, wind 12 m/s, rain prob 0%, all sensitivities 1.0
- **THEN** the score is ≤ 40

### Requirement: BBQ weather from warmth, dryness, light wind
`BbqScore.Compute` SHALL accept mean temperature (°C), mean humidity (%), mean wind speed (m/s), mean precipitation probability (%), and a `ResolvedPreferences`. It SHALL return an `int` score 0–100. Minimum temperature SHALL be `ResolvedPreferences.MinTemp` (default 10°C). Wind ideal range SHALL be `ResolvedPreferences.IdealWindLow` to `ResolvedPreferences.IdealWindHigh` (defaults 1–3 m/s). Rain probability SHALL be weighted 0.35 (critical). Penalty terms SHALL be scaled by the corresponding sensitivity multipliers. Null inputs SHALL use neutral sub-score 50.

#### Scenario: Perfect BBQ
- **WHEN** temp is 26°C, humidity 40%, wind 2 m/s, rain prob 0%, all sensitivities 1.0
- **THEN** the score is ≥ 90

#### Scenario: Rain kills the BBQ
- **WHEN** temp is 26°C, humidity 40%, wind 2 m/s, rain prob 80%, all sensitivities 1.0
- **THEN** the score is ≤ 30

#### Scenario: Custom BBQ min temp
- **WHEN** temp is 12°C, humidity 40%, wind 2 m/s, rain prob 0%, MinTemp 15.0
- **THEN** the score is lower than with MinTemp 10.0

### Requirement: Irrigation need from rain absence, heat, dryness, evapotranspiration
`IrrigationScore.Compute` SHALL accept mean precipitation probability (%), mean temperature (°C), mean humidity (%), mean evapotranspiration (mm), and a `ResolvedPreferences`. It SHALL return an `int` score 0–100. High score = water your garden. Penalty terms SHALL be scaled by the corresponding sensitivity multipliers. Null inputs SHALL use neutral sub-score 50.

#### Scenario: Hot dry day
- **WHEN** rain prob 0%, temp 32°C, humidity 30%, ET 6.0 mm, all sensitivities 1.0
- **THEN** the score is ≥ 85

#### Scenario: Rainy day
- **WHEN** rain prob 90%, temp 15°C, humidity 80%, ET 1.0 mm, all sensitivities 1.0
- **THEN** the score is ≤ 15

### Requirement: Solar yield score from radiation, cloud cover, temperature
`SolarScore.Compute` SHALL accept mean shortwave radiation (W/m²), mean cloud cover (%), and mean temperature (°C), and a `ResolvedPreferences`. It SHALL return an `int` score 0–100. Temperature efficiency SHALL decrease above 25°C, scaled by `ResolvedPreferences.HeatSensitivity`. Null inputs SHALL use neutral sub-score 50.

#### Scenario: Clear cool day
- **WHEN** radiation 800 W/m², cloud cover 10%, temp 18°C, all sensitivities 1.0
- **THEN** the score is ≥ 85

#### Scenario: Overcast hot day
- **WHEN** radiation 150 W/m², cloud cover 90%, temp 38°C, all sensitivities 1.0
- **THEN** the score is ≤ 20

### Requirement: Frost protection hours and confidence
`FrostProtection.Compute` SHALL scan the next 48h of consensus temperature data for frost (≤ 0°C). It does not accept `ResolvedPreferences`.

#### Scenario: Frost in 8 hours
- **WHEN** the consensus shows temp ≤ 0 at T0+8h
- **THEN** HoursUntilFrost is 8

#### Scenario: No frost risk
- **WHEN** all temperatures in the next 48h are > 0
- **THEN** the result is null

### Requirement: VPD plant stress category
`VaporPressureDeficit.Categorize` SHALL compute VPD using the Magnus formula. It does not accept `ResolvedPreferences`.

#### Scenario: Optimal greenhouse
- **WHEN** temp is 25°C and humidity is 60%
- **THEN** VPD is approximately 1.27 kPa and category is "high"

#### Scenario: Null inputs
- **WHEN** temperature or humidity is null
- **THEN** the result is null

### Requirement: IndexResult passes resolved preferences to scorers
`IndexAnalyzer` SHALL resolve the `ResolvedPreferences` for each (location, `IndexKind`) pair from the current `IndexSettings.Preferences`, falling back to `ResolvedPreferences.Default`, and SHALL pass them to the matching `IIndexScore.Score(slice, preferences)` for every day slice. When a live indoor temperature is available from the sensor snapshot, it SHALL override the indoor temperature of the location's preferences for that computation.

#### Scenario: Per-score preferences used across day slices
- **WHEN** Running has `HeatSensitivity: 0.7` and Outdoor has `HeatSensitivity: 1.5`
- **THEN** `RunningScore` receives 0.7 and `OutdoorScore` receives 1.5 for all day slices

### Requirement: IndexResult includes per-day envelope for each activity score
Each `DayScoreSet` SHALL include, for each numeric score field (Laundry, Outdoor, Running, Cycling, Bbq, Irrigation, Solar, NightVentilation): a `ScoreEnvelope` with `Min` (int), `Max` (int), and `Confidence` (double, 0.0–1.0). Envelope computation SHALL use the same time-filtered consensus bounds as the main score computation.

#### Scenario: Envelope uses day-filtered CI bounds
- **WHEN** envelope pessimistic/optimistic scores are computed for d1 Outdoor
- **THEN** only daylight-hour CI bounds from d1 SHALL be used

#### Scenario: Later days have wider envelopes
- **WHEN** d0 and d2 envelopes are compared for the same weather conditions
- **THEN** d2 confidence SHALL generally be lower than d0 confidence (reflecting forecast uncertainty)

### Requirement: Index computation uses day-filtered means instead of rolling 24h
`IndexAnalyzer` SHALL use `DaySlicer.Slice(consensus, now)` to split the consensus into up to three UTC day slices (d0, d1, d2) and SHALL score each slice with every registered `IIndexScore`, using the slice's period means:

- **Activity scores** (Outdoor, Running, Cycling, BBQ, Solar): computed from the daylight means (`DaySlice.DayMeans`).
- **Utility scores** (Laundry, Irrigation): computed from the full-day means (`DaySlice.FullDayMeans`).
- **NightVentilation**: computed from the nighttime means (`DaySlice.NightMeans`).

FrostProtection SHALL be computed once over the consensus and VPD once from the d0 full-day means (not per day).

#### Scenario: Outdoor score uses daylight means only
- **WHEN** d1 has 16 daylight hours with mean temp 24°C and 8 nighttime hours with mean temp 14°C
- **THEN** the d1 Outdoor score SHALL be computed from the 24°C daylight mean, not a 24h average

#### Scenario: Laundry uses full-day means
- **WHEN** d1 has mean temp 20°C across all 24 hours
- **THEN** the d1 Laundry score SHALL use the 20°C full-day mean

#### Scenario: NightVentilation uses nighttime means
- **WHEN** d1 has nighttime mean temp 16°C and daytime mean temp 28°C, IndoorTemp 22°C
- **THEN** the d1 NightVentilation score SHALL be computed from the 16°C nighttime mean

#### Scenario: Day slice with zero daylight hours
- **WHEN** d0 has 0 remaining daylight hours (late evening)
- **THEN** activity scores for d0 use the neutral fallback (50) and `HoursIncluded` equals the remaining nighttime hours

### Requirement: State payload includes hours_included per day
Each day-offset state JSON SHALL include an `hours_included` field (int) taken from `DaySlice.HoursIncluded`, the larger of the slice's daylight and nighttime hour counts; it communicates the "today shrinks" behavior to consumers.

#### Scenario: Full day
- **WHEN** d1 has 16 daylight hours and 8 nighttime hours
- **THEN** the d1 state payload SHALL contain `"hours_included": 16`

#### Scenario: Late today
- **WHEN** d0 has 3 remaining daylight hours and 6 remaining nighttime hours at poll time
- **THEN** the d0 state payload SHALL contain `"hours_included": 6`

### Requirement: MQTT state topics include day offset
Index state messages SHALL be published to `<baseTopic>/<location>/indices/d0`, `<baseTopic>/<location>/indices/d1`, `<baseTopic>/<location>/indices/d2` instead of the former single `<baseTopic>/<location>/indices` topic.

#### Scenario: Three state topics
- **WHEN** indices are computed for location "lucerne" with base topic "njord"
- **THEN** state messages SHALL be published to `njord/lucerne/indices/d0`, `njord/lucerne/indices/d1`, `njord/lucerne/indices/d2`

### Requirement: State payload excludes HDD and CDD fields
The indices state JSON SHALL NOT contain `hdd` or `cdd` keys.

#### Scenario: JSON without degree days
- **WHEN** index result is serialized to state payload
- **THEN** JSON does not contain `"hdd"` or `"cdd"` keys

### Requirement: State payload includes envelope fields alongside existing scores
Each day-offset state JSON SHALL include `_min`, `_max`, `_confidence` variants for each score key (excluding `hdd`/`cdd`).

#### Scenario: JSON structure
- **WHEN** index result is serialized for d1
- **THEN** JSON contains `{"outdoor": 72, "outdoor_min": 65, "outdoor_max": 80, "outdoor_confidence": 0.8, ...}` without `hdd`/`cdd`

### Requirement: IndexResult aggregates all indices and serializes to MQTT
`IndexResult` (src/Njord.Compute/Contracts/IndexResult.cs) SHALL be the output of index computation. It SHALL contain a `Location` (`LocationId`), a `Days` list of `DayScoreSet` (one per computed day, up to 3), `FrostProtection` (`FrostProtectionInfo?`), and `Vpd` (`VpdInfo?`).

Each `DayScoreSet` SHALL contain: `DayOffset` (int, 0/1/2), `Laundry` (int), `Outdoor` (int), `Running` (int), `Cycling` (int), `Bbq` (int), `Irrigation` (int), `Solar` (int), `NightVentilation` (int), `HoursIncluded` (int), and an optional envelope for each score. The location SHALL come from `ConsensusSnapshot.Location`. `StatePayloadBuilder.FromIndices` SHALL serialize the result.

#### Scenario: Index message content with daily slices
- **WHEN** indices are serialized to MQTT
- **THEN** one retained message SHALL be published per day offset (d0, d1, d2) with all scores and envelope fields for that day

#### Scenario: Retained messages
- **WHEN** index messages are published
- **THEN** each day-offset message SHALL be retained

#### Scenario: Frost and VPD on d0 only
- **WHEN** index messages are published
- **THEN** `frost_hours`, `frost_confidence`, `vpd_category`, `vpd_kpa` SHALL appear only in the d0 payload

### Requirement: The index result has no degree-day fields
`IndexResult` and `DayScoreSet` SHALL NOT contain `Hdd` or `Cdd` properties.

#### Scenario: IndexResult without degree days
- **WHEN** `IndexAnalyzer` computes an `IndexResult`
- **THEN** neither the result nor its `DayScoreSet`s contain `Hdd` or `Cdd` properties

### Requirement: NightVentilation replaces Ventilation in the score set
`DayScoreSet` SHALL contain `NightVentilation` (int) instead of `Ventilation`, scored by `NightVentilationScore` (`IndexKind.NightVentilation`). State payload keys and preference resolution SHALL use `night_ventilation` / `NightVentilation` instead of `ventilation` / `Ventilation`.

#### Scenario: Wire format uses night_ventilation
- **WHEN** index result is serialized to state payload
- **THEN** the JSON key SHALL be `"night_ventilation"`, not `"ventilation"`

