# activity-indices Specification

## Purpose

Activity and environmental index scores computed from forecast data: lifestyle scores (laundry drying, outdoor, running, cycling, BBQ, irrigation, ventilation, solar yield), frost protection, VPD plant stress, and a unified IndexResult that serializes all indices to MQTT. All scorer methods accept `ResolvedPreferences` for configurable sensitivity multipliers and ideal points.

## Requirements

### Requirement: Laundry drying score from temperature, humidity, wind, rain, sunshine
`IndexScorer.LaundryDrying` SHALL accept mean temperature (°C), mean relative humidity (%), mean wind speed (m/s), mean precipitation probability (%), sunshine percentage (%), and a `ResolvedPreferences`. It SHALL return an `int` score 0–100. The formula SHALL weight: temperature 0.3, humidity 0.25, wind 0.2, rain probability 0.15, sunshine 0.1. Penalty terms SHALL be scaled by the corresponding sensitivity multipliers from `ResolvedPreferences`. Null inputs SHALL use neutral sub-score 50.

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
`IndexScorer.OutdoorScore` SHALL accept mean temperature (°C), mean relative humidity (%), mean precipitation probability (%), mean wind speed (m/s), mean cloud cover (%), and a `ResolvedPreferences`. It SHALL return an `int` score 0–100.

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
`IndexScorer.RunningComfort` SHALL accept mean temperature (°C), mean humidity (%), mean wind speed (m/s), mean precipitation probability (%), and a `ResolvedPreferences`. It SHALL return an `int` score 0–100. Optimal temperature range SHALL be `ResolvedPreferences.IdealTempLow` to `ResolvedPreferences.IdealTempHigh` (defaults 5–20°C). Penalty terms SHALL be scaled by the corresponding sensitivity multipliers. Null inputs SHALL use neutral sub-score 50.

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
`IndexScorer.CyclingComfort` SHALL accept mean temperature (°C), mean humidity (%), mean wind speed (m/s), mean precipitation probability (%), and a `ResolvedPreferences`. It SHALL return an `int` score 0–100. Wind SHALL be weighted 0.3 (vs 0.2 for running). Penalty terms SHALL be scaled by the corresponding sensitivity multipliers. Null inputs SHALL use neutral sub-score 50.

#### Scenario: Calm warm day
- **WHEN** temp is 18°C, humidity 50%, wind 1.5 m/s, rain prob 0%, all sensitivities 1.0
- **THEN** the score is ≥ 85

#### Scenario: Very windy
- **WHEN** temp is 18°C, humidity 50%, wind 12 m/s, rain prob 0%, all sensitivities 1.0
- **THEN** the score is ≤ 40

### Requirement: BBQ weather from warmth, dryness, light wind
`IndexScorer.BbqWeather` SHALL accept mean temperature (°C), mean humidity (%), mean wind speed (m/s), mean precipitation probability (%), and a `ResolvedPreferences`. It SHALL return an `int` score 0–100. Minimum temperature SHALL be `ResolvedPreferences.MinTemp` (default 10°C). Wind ideal range SHALL be `ResolvedPreferences.IdealWindLow` to `ResolvedPreferences.IdealWindHigh` (defaults 1–3 m/s). Rain probability SHALL be weighted 0.35 (critical). Penalty terms SHALL be scaled by the corresponding sensitivity multipliers. Null inputs SHALL use neutral sub-score 50.

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
`IndexScorer.IrrigationNeed` SHALL accept mean precipitation probability (%), mean temperature (°C), mean humidity (%), mean evapotranspiration (mm), and a `ResolvedPreferences`. It SHALL return an `int` score 0–100. High score = water your garden. Penalty terms SHALL be scaled by the corresponding sensitivity multipliers. Null inputs SHALL use neutral sub-score 50.

#### Scenario: Hot dry day
- **WHEN** rain prob 0%, temp 32°C, humidity 30%, ET 6.0 mm, all sensitivities 1.0
- **THEN** the score is ≥ 85

#### Scenario: Rainy day
- **WHEN** rain prob 90%, temp 15°C, humidity 80%, ET 1.0 mm, all sensitivities 1.0
- **THEN** the score is ≤ 15

### Requirement: Solar yield score from radiation, cloud cover, temperature
`IndexScorer.SolarYield` SHALL accept mean shortwave radiation (W/m²), mean cloud cover (%), and mean temperature (°C), and a `ResolvedPreferences`. It SHALL return an `int` score 0–100. Temperature efficiency SHALL decrease above 25°C, scaled by `ResolvedPreferences.HeatSensitivity`. Null inputs SHALL use neutral sub-score 50.

#### Scenario: Clear cool day
- **WHEN** radiation 800 W/m², cloud cover 10%, temp 18°C, all sensitivities 1.0
- **THEN** the score is ≥ 85

#### Scenario: Overcast hot day
- **WHEN** radiation 150 W/m², cloud cover 90%, temp 38°C, all sensitivities 1.0
- **THEN** the score is ≤ 20

### Requirement: Ventilation score from outdoor-indoor delta, humidity, wind, rain
`IndexScorer.Ventilation` SHALL accept mean outdoor temperature (°C), indoor temperature, mean humidity (%), mean wind speed (m/s), mean precipitation probability (%), and a `ResolvedPreferences`. It SHALL return an `int` score 0–100. High score = open the windows. The indoor temperature SHALL be resolved using a fallback chain: (1) live `IndoorTemperature` from the `SensorSnapshot` when available, (2) configured `ResolvedPreferences.IndoorTemp` if no live reading exists, (3) hardcoded default of 22.0 if neither is set. Penalty terms SHALL be scaled by the corresponding sensitivity multipliers. Null inputs SHALL use neutral sub-score 50.

#### Scenario: Cool evening breeze
- **WHEN** outdoor 17°C, humidity 45%, wind 3 m/s, rain prob 0%, IndoorTemp 22°C, all sensitivities 1.0
- **THEN** the score is ≥ 85

#### Scenario: Hot humid outside
- **WHEN** outdoor 30°C, humidity 80%, wind 1 m/s, rain prob 0%, IndoorTemp 22°C, all sensitivities 1.0
- **THEN** the score is ≤ 15

#### Scenario: Live sensor value used
- **WHEN** the SensorSnapshot contains `IndoorTemperature = 24.5`
- **AND** the config `IndoorTemp` is `22.0`
- **THEN** the Ventilation score SHALL be computed with indoor temperature `24.5`

#### Scenario: No sensor value falls back to config
- **WHEN** the SensorSnapshot is null or does not contain `IndoorTemperature`
- **AND** the config `IndoorTemp` is `20.0`
- **THEN** the Ventilation score SHALL be computed with indoor temperature `20.0`

#### Scenario: No sensor and no config falls back to default
- **WHEN** the SensorSnapshot is null
- **AND** no config `IndoorTemp` is set
- **THEN** the Ventilation score SHALL be computed with indoor temperature `22.0`

### Requirement: Frost protection hours and confidence
`IndexScorer.FrostProtection` SHALL scan the next 48h of consensus temperature data for frost (≤ 0°C). It does not accept `ResolvedPreferences`.

#### Scenario: Frost in 8 hours
- **WHEN** the consensus shows temp ≤ 0 at T0+8h
- **THEN** HoursUntilFrost is 8

#### Scenario: No frost risk
- **WHEN** all temperatures in the next 48h are > 0
- **THEN** the result is null

### Requirement: VPD plant stress category
`IndexScorer.VpdCategory` SHALL compute VPD using the Magnus formula. It does not accept `ResolvedPreferences`.

#### Scenario: Optimal greenhouse
- **WHEN** temp is 25°C and humidity is 60%
- **THEN** VPD is approximately 1.27 kPa and category is "high"

#### Scenario: Null inputs
- **WHEN** temperature or humidity is null
- **THEN** the result is null

### Requirement: IndexResult excludes HDD and CDD
`IndexResult` SHALL NOT contain `Hdd` or `Cdd` properties. The record constructor SHALL not accept degree-day values. `IndexResult.Compute` SHALL NOT call `IndexScorer.HeatingDegreeDays` or `IndexScorer.CoolingDegreeDays`.

#### Scenario: IndexResult without degree days
- **WHEN** `IndexResult.Compute` is called
- **THEN** the result does not contain `Hdd` or `Cdd` properties

### Requirement: IndexResult passes resolved preferences to scorers
`IndexResult.Compute` SHALL accept a resolver function or dictionary to obtain `ResolvedPreferences` for the current location and score. Each scorer call SHALL use the preferences resolved for its specific (location, score) pair.

#### Scenario: Per-score preferences used
- **WHEN** Running has `HeatSensitivity: 0.7` and Outdoor has `HeatSensitivity: 1.5`
- **THEN** `RunningComfort` receives 0.7 and `OutdoorScore` receives 1.5

### Requirement: IndexResult includes per-model envelope for each activity score
`IndexResult` SHALL include, for each numeric score field (Laundry, Outdoor, Running, Cycling, Bbq, Irrigation, Solar, Ventilation): a `ScoreEnvelope` with `Min` (int), `Max` (int), and `Confidence` (double, 0.0–1.0). Envelope computation SHALL use the same `ResolvedPreferences` as the main score computation.

#### Scenario: Envelope uses resolved preferences
- **WHEN** envelope pessimistic/optimistic scores are computed
- **THEN** the same `ResolvedPreferences` are used for both bounds

### Requirement: Index computation evaluates consensus values instead of per-model aggregation

`IndexResult.Compute` SHALL accept a `ConsensusSnapshot` instead of `ModelSnapshot`. Activity scores (laundry, outdoor, running, cycling, BBQ, irrigation, solar, ventilation) SHALL be computed from consensus median values. The per-model envelope (min/max/confidence across individual models) is no longer available since enrichments no longer see raw model data.

#### Scenario: Scores computed from consensus medians
- **WHEN** `IndexResult.Compute` is called with a `ConsensusSnapshot`
- **THEN** each activity score is computed using consensus median temperature, precipitation, wind speed, etc.

#### Scenario: Envelope derived from consensus spread
- **WHEN** consensus spread is available for the input parameters
- **THEN** the score envelope min/max SHALL be derived from consensus confidence interval or spread bounds instead of per-model evaluation

#### Scenario: Single-value consensus
- **WHEN** only 2 models contribute and spread is minimal
- **THEN** envelope min and max are close to the score value with high confidence

### Requirement: State payload excludes HDD and CDD fields
The indices state JSON SHALL NOT contain `hdd` or `cdd` keys.

#### Scenario: JSON without degree days
- **WHEN** index result is serialized to state payload
- **THEN** JSON does not contain `"hdd"` or `"cdd"` keys

### Requirement: State payload includes envelope fields alongside existing scores
The indices state JSON SHALL include `_min`, `_max`, `_confidence` variants for each score key (excluding `hdd`/`cdd`).

#### Scenario: JSON structure
- **WHEN** index result is serialized
- **THEN** JSON contains `{"outdoor": 72, "outdoor_min": 65, "outdoor_max": 80, "outdoor_confidence": 0.8, ...}` without `hdd`/`cdd`

### Requirement: Discovery excludes HDD and CDD components
`IndexEnrichment.BuildDiscoveryPayload` SHALL NOT register sensor components for `hdd` or `cdd`. Sensor count per location drops from 38 to 34 (8 scores + 8x3 envelopes + frost_hours + frost_confidence + vpd_kpa + vpd_category = 34 without hdd/cdd).

#### Scenario: Discovery without degree day sensors
- **WHEN** discovery payload is built for indices
- **THEN** components do not include `hdd` or `cdd`

### Requirement: Discovery registers envelope components
`IndexEnrichment.BuildDiscoveryPayload` SHALL register sensor components for each envelope field of the remaining 8 scores.

#### Scenario: Envelope discovery components
- **WHEN** discovery payload is built for indices
- **THEN** components include `outdoor_min`, `outdoor_max`, `outdoor_confidence` (and likewise for all 8 scores)

### Requirement: IndexResult aggregates all indices and serializes to MQTT

`IndexResult` SHALL derive its location from `ConsensusSnapshot.Location`.

#### Scenario: Index message content
- **WHEN** indices are serialized to MQTT
- **THEN** one retained message is published with all scores and envelope fields

#### Scenario: Retained message
- **WHEN** an index message is published
- **THEN** it is retained

### Requirement: IndexResult serialization with pinned wire names
`IndexResult` and `ScoreEnvelope` records SHALL have `[property: JsonProperty("...")]` on all positional parameters. Value tuple properties `FrostProtection` and `Vpd` SHALL be replaced with named records (`FrostProtectionInfo`, `VpdInfo`) carrying `[JsonProperty]` attributes. The removal of `Hdd`/`Cdd` properties constitutes a version bump.

#### Scenario: IndexResult without hdd/cdd round-trips through JSON
- **WHEN** an `IndexResult` is serialized and deserialized
- **THEN** all properties round-trip correctly and no `hdd`/`cdd` keys appear

#### Scenario: IndexResult with null optional fields round-trips
- **WHEN** an `IndexResult` with null FrostProtection, Vpd, and null envelope fields is serialized and deserialized
- **THEN** all null values are preserved and non-null fields round-trip correctly
