## MODIFIED Requirements

### Requirement: IndexResult aggregates all indices and serializes to MQTT

`DaySliceIndexResult` SHALL replace `IndexResult` as the output of index computation. It SHALL contain a `Location` (string), a `Days` list of `DayScoreSet` (one per computed day, up to 3), `FrostProtection` (`FrostProtectionInfo?`), and `Vpd` (`VpdInfo?`).

Each `DayScoreSet` SHALL contain: `DayOffset` (int, 0/1/2), `Laundry` (int), `Outdoor` (int), `Running` (int), `Cycling` (int), `Bbq` (int), `Irrigation` (int), `Solar` (int), `NightVentilation` (int), `HoursIncluded` (int), envelope fields for each score, and derive its location from `ConsensusSnapshot.Location`.

#### Scenario: Index message content with daily slices

- **WHEN** indices are serialized to MQTT
- **THEN** one retained message SHALL be published per day offset (d0, d1, d2) with all scores and envelope fields for that day

#### Scenario: Retained messages

- **WHEN** index messages are published
- **THEN** each day-offset message SHALL be retained

#### Scenario: Frost and VPD on d0 only

- **WHEN** index messages are published
- **THEN** `frost_hours`, `frost_confidence`, `vpd_category`, `vpd_kpa` SHALL appear only in the d0 payload

### Requirement: Index computation uses day-filtered means instead of rolling 24h

`DaySliceIndexResult.Compute` SHALL accept a `ConsensusSnapshot`, `ResolvedParameterSet`, `TimeProvider`, and resolved preferences. It SHALL use `TimeSliceAggregator` to split consensus into day slices, then compute scores per slice:

- **Activity scores** (Outdoor, Running, Cycling, BBQ, Solar): computed from `DaySlice.DayMeans` (daylight hours only).
- **Utility scores** (Laundry, Irrigation): computed from `DaySlice.FullDayMeans` (all hours).
- **NightVentilation**: computed from `DaySlice.NightMeans` (nighttime hours only).

FrostProtection and VPD SHALL be computed once (not per day), unchanged from current logic.

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
- **THEN** activity scores for d0 SHALL use neutral fallback (50) and `HoursIncluded` SHALL be 0

### Requirement: Ventilation replaced by NightVentilation in score set

`DayScoreSet` SHALL contain `NightVentilation` (int) instead of `Ventilation`. The `IndexScorer.Ventilation` method SHALL be renamed to `NightVentilation`. All discovery components, state payload keys, and preference resolution SHALL use `night_ventilation` / `NightVentilation` instead of `ventilation` / `Ventilation`.

#### Scenario: Wire format uses night_ventilation

- **WHEN** index result is serialized to state payload
- **THEN** the JSON key SHALL be `"night_ventilation"`, not `"ventilation"`

#### Scenario: Discovery uses night_ventilation component name

- **WHEN** discovery payload is built for indices
- **THEN** components SHALL include `night_ventilation_d0`, `night_ventilation_d1`, `night_ventilation_d2`
- **AND** components SHALL NOT include `ventilation`

### Requirement: State payload includes hours_included per day

Each day-offset state JSON SHALL include an `hours_included` field (int) indicating how many hours contributed to the scores in that slice. For activity scores, this is the daylight hour count; the field communicates the "today shrinks" behavior to HA templates.

#### Scenario: Full day

- **WHEN** d1 has 16 daylight hours
- **THEN** the d1 state payload SHALL contain `"hours_included": 16`

#### Scenario: Late today

- **WHEN** d0 has 3 remaining daylight hours at poll time
- **THEN** the d0 state payload SHALL contain `"hours_included": 3`

### Requirement: MQTT state topics include day offset

Index state messages SHALL be published to `<baseTopic>/<location>/indices/d0`, `<baseTopic>/<location>/indices/d1`, `<baseTopic>/<location>/indices/d2` instead of the former single `<baseTopic>/<location>/indices` topic.

#### Scenario: Three state topics

- **WHEN** indices are computed for location "lucerne" with base topic "njord"
- **THEN** state messages SHALL be published to `njord/lucerne/indices/d0`, `njord/lucerne/indices/d1`, `njord/lucerne/indices/d2`

### Requirement: Discovery components include day offset dimension

Discovery SHALL register sensor components with day-offset suffixes: `outdoor_d0`, `outdoor_d1`, `outdoor_d2`, etc. Each component SHALL reference its day-offset state topic. Envelope components follow the same pattern: `outdoor_min_d0`, `outdoor_max_d1`, etc.

#### Scenario: Discovery component count per location

- **WHEN** discovery payload is built for indices with 3 day slices
- **THEN** the device SHALL have (8 scores × 3 days × 4 fields) + frost (2) + VPD (2) = 100 components

#### Scenario: Component references day-offset topic

- **WHEN** the discovery payload for location "lucerne" includes `outdoor_d1`
- **THEN** the component SHALL have `"state_topic": "njord/lucerne/indices/d1"` and `"value_template": "{{ value_json.outdoor }}"`

### Requirement: DaySliceIndexResult serialization with pinned wire names

`DaySliceIndexResult` and `DayScoreSet` records SHALL have `[property: JsonProperty("...")]` on all positional parameters. The `ventilation` wire name SHALL be replaced by `night_ventilation`. Persistence DTO version SHALL be incremented.

#### Scenario: DaySliceIndexResult round-trips through JSON

- **WHEN** a `DaySliceIndexResult` with 3 day score sets is serialized and deserialized
- **THEN** all properties round-trip correctly including day offsets, scores, envelopes, frost, and VPD

#### Scenario: No ventilation key in JSON

- **WHEN** a `DayScoreSet` is serialized
- **THEN** JSON SHALL NOT contain a `"ventilation"` key; it SHALL contain `"night_ventilation"`

### Requirement: IndexResult excludes HDD and CDD

`DaySliceIndexResult` SHALL NOT contain `Hdd` or `Cdd` properties. (Unchanged from current spec — carried forward.)

#### Scenario: DaySliceIndexResult without degree days

- **WHEN** `DaySliceIndexResult.Compute` is called
- **THEN** the result does not contain `Hdd` or `Cdd` properties

### Requirement: IndexResult passes resolved preferences to scorers

`DaySliceIndexResult.Compute` SHALL accept a resolver function or dictionary to obtain `ResolvedPreferences` for the current location and score. Each scorer call SHALL use the preferences resolved for its specific (location, score) pair. (Unchanged from current spec — carried forward.)

#### Scenario: Per-score preferences used across day slices

- **WHEN** Running has `HeatSensitivity: 0.7` and Outdoor has `HeatSensitivity: 1.5`
- **THEN** `RunningComfort` receives 0.7 and `OutdoorScore` receives 1.5 for all day slices

### Requirement: IndexResult includes per-day envelope for each activity score

Each `DayScoreSet` SHALL include, for each numeric score field (Laundry, Outdoor, Running, Cycling, Bbq, Irrigation, Solar, NightVentilation): a `ScoreEnvelope` with `Min` (int), `Max` (int), and `Confidence` (double, 0.0–1.0). Envelope computation SHALL use the same time-filtered consensus bounds as the main score computation.

#### Scenario: Envelope uses day-filtered CI bounds

- **WHEN** envelope pessimistic/optimistic scores are computed for d1 Outdoor
- **THEN** only daylight-hour CI bounds from d1 SHALL be used

#### Scenario: Later days have wider envelopes

- **WHEN** d0 and d2 envelopes are compared for the same weather conditions
- **THEN** d2 confidence SHALL generally be lower than d0 confidence (reflecting forecast uncertainty)

## REMOVED Requirements

### Requirement: Ventilation score from outdoor-indoor delta, humidity, wind, rain

**Reason**: Replaced by NightVentilation (nighttime-only variant). The 24h Ventilation score mixed day and night hours, making it misleading in summer when daytime heat dominated the average.

**Migration**: Use `NightVentilation` score. Config key changes from `Ventilation` to `NightVentilation` in `ScoreOverrides`. HA entity ID changes from `*_ventilation` to `*_night_ventilation_d0/d1/d2`.
