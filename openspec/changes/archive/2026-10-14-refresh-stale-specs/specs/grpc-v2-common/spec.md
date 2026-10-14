## MODIFIED Requirements

### Requirement: Enrichment payload messages
`common.proto` SHALL define all enrichment payload messages: `AlertUpdate`, `Alert`, `AlertType`, `AlertSeverity`, `IndexUpdate`, `DayScoreSet`, `ScoreEnvelope`, `FrostInfo`, `VpdInfo`, `ParameterTrend`, `TrendUpdate`, `HorizonDerived`, `ScalarDerived`, `DerivedUpdate`, `HorizonConsensus`, `ParameterConsensus`, `ConsensusUpdate`.

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
