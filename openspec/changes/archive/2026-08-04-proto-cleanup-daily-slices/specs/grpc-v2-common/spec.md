## ADDED Requirements

### Requirement: DayScoreSet message for per-day index scores

`common.proto` SHALL define a `DayScoreSet` message with fields: `int32 day_offset`, `int32 laundry`, `int32 outdoor`, `int32 running`, `int32 cycling`, `int32 bbq`, `int32 irrigation`, `int32 solar`, `int32 night_ventilation`, `int32 hours_included`, and optional `ScoreEnvelope` fields for each score (`laundry_envelope`, `outdoor_envelope`, `running_envelope`, `cycling_envelope`, `bbq_envelope`, `irrigation_envelope`, `solar_envelope`, `night_ventilation_envelope`).

#### Scenario: DayScoreSet carries all scores for one day

- **WHEN** a `DayScoreSet` with `day_offset = 1` is serialized
- **THEN** it SHALL contain all 8 score fields, `hours_included`, and up to 8 optional envelope fields

### Requirement: ScoreEnvelope sub-message

`common.proto` SHALL define a `ScoreEnvelope` message with fields: `int32 min`, `int32 max`, `double confidence`.

#### Scenario: ScoreEnvelope round-trips

- **WHEN** a `ScoreEnvelope` with min=65, max=80, confidence=0.85 is serialized and deserialized
- **THEN** all three fields SHALL round-trip correctly

### Requirement: FrostInfo sub-message

`common.proto` SHALL define a `FrostInfo` message with fields: `int32 hours_until_frost`, `double confidence`.

#### Scenario: FrostInfo carries countdown

- **WHEN** frost is detected 14 hours out with 0.75 confidence
- **THEN** `FrostInfo` SHALL have `hours_until_frost = 14` and `confidence = 0.75`

### Requirement: VpdInfo sub-message

`common.proto` SHALL define a `VpdInfo` message with fields: `double kpa`, `string category`.

#### Scenario: VpdInfo carries category and value

- **WHEN** VPD is 1.27 kPa (category "high")
- **THEN** `VpdInfo` SHALL have `kpa = 1.27` and `category = "high"`

## MODIFIED Requirements

### Requirement: Enrichment payload messages

`common.proto` SHALL define all enrichment payload messages: `AlertUpdate`, `Alert`, `AlertType`, `AlertSeverity`, `IndexUpdate`, `DayScoreSet`, `ScoreEnvelope`, `FrostInfo`, `VpdInfo`, `ParameterTrend`, `TrendUpdate`, `HorizonDerived`, `ScalarDerived`, `DerivedUpdate`, `ModelMetrics`, `HistoryUpdate`, `HorizonConsensus`, `ParameterConsensus`, `ConsensusUpdate`.

`IndexUpdate` SHALL contain: `repeated DayScoreSet days`, `optional FrostInfo frost`, `optional VpdInfo vpd`. It SHALL NOT contain flat score fields (`laundry`, `outdoor`, etc.), `ventilation`, `hdd`, `cdd`, `frost_hours`, `frost_confidence`, `vpd_kpa`, `vpd_category`, or any `reserved` statements.

#### Scenario: IndexUpdate contains daily slices

- **WHEN** an `IndexUpdate` is populated with 3 day slices
- **THEN** `days` SHALL contain 3 `DayScoreSet` entries with `day_offset` 0, 1, 2

#### Scenario: IndexUpdate without frost

- **WHEN** no frost is detected
- **THEN** `frost` field SHALL be absent (not set)

#### Scenario: No reserved fields in any message

- **WHEN** `common.proto` is inspected
- **THEN** it SHALL contain zero `reserved` statements and zero energy-removal comments

## REMOVED Requirements

### Requirement: IndexUpdate flat score fields

**Reason**: Replaced by `repeated DayScoreSet days` with per-day scores. The flat layout cannot represent daily slices.

**Migration**: Use `IndexUpdate.days[0]` to access d0 scores. Field `ventilation` replaced by `night_ventilation` in `DayScoreSet`.
