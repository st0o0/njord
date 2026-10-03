## MODIFIED Requirements

### Requirement: DetailedEnrichmentConfig message

`DetailedEnrichmentConfig` SHALL contain fields: `ConsensusConfig consensus`, `AlertConfig alerts`, `DerivedConfig derived`, `TrendConfig trends`, `IndexConfig indices`, `HistoryConfig history`. It SHALL NOT contain any `reserved` statements or energy-related fields. Field numbers SHALL be sequential.

#### Scenario: No energy config field

- **WHEN** `DetailedEnrichmentConfig` is inspected
- **THEN** it SHALL have exactly 6 config fields with no gaps or reserved entries

### Requirement: IndexConfig message

`IndexConfig` SHALL contain fields: `optional bool enabled`, `optional double indoor_temp`, `optional double ideal_outdoor_temp`, `optional double heat_sensitivity`, `optional double humidity_sensitivity`, `optional double wind_sensitivity`, `optional double rain_sensitivity`, `optional double running_ideal_temp_low`, `optional double running_ideal_temp_high`, `optional double bbq_min_temp`, `optional double bbq_ideal_wind_low`, `optional double bbq_ideal_wind_high`. It SHALL NOT contain `heating_base_temp`, `cooling_base_temp`, or any `reserved` statements. Field numbers SHALL be sequential starting from 1.

#### Scenario: IndexConfig without degree-day fields

- **WHEN** `IndexConfig` is inspected
- **THEN** it SHALL have 12 fields with sequential numbering and no reserved entries

### Requirement: SetEnrichmentRequest message

`SetEnrichmentRequest` SHALL contain optional fields for each enrichment config: `ConsensusConfig consensus`, `AlertConfig alerts`, `DerivedConfig derived`, `TrendConfig trends`, `IndexConfig indices`, `HistoryConfig history`. It SHALL NOT contain any `reserved` statements or energy-related fields. Field numbers SHALL be sequential.

#### Scenario: No energy field in mutation request

- **WHEN** `SetEnrichmentRequest` is inspected
- **THEN** it SHALL have exactly 6 optional config fields with no gaps
