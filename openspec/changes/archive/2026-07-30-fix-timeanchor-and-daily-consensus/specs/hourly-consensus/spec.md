## MODIFIED Requirements

### Requirement: HourlyConsensusEnrichment computes consensus for every hour with sufficient model coverage
`ConsensusEnrichment` SHALL implement `IStatelessEnrichment` with `TypeName = "consensus"`. On each `ModelSnapshot`, it SHALL compute `ConsensusResult` for hourly horizons from h0 up to the last hour where at least 2 models have forecast data. Hours where fewer than 2 models contribute data for a given parameter SHALL be excluded from the output. The enrichment SHALL be the sole consensus enrichment — there is no separate horizon-based consensus.

#### Scenario: Hourly consensus across models with different horizons
- **WHEN** a `ModelSnapshot` contains icon_d2 (48h coverage) and ecmwf_ifs025 (240h coverage) and gfs_seamless (384h coverage)
- **THEN** the enrichment SHALL produce consensus for h0 through h48 (the last hour where >=2 models have data)
- **AND** hours like h1, h2, h4, h5 (where only hourly-resolution models contribute) SHALL still be included if >=2 hourly models are available

#### Scenario: Single model remaining stops consensus
- **WHEN** only one model has data beyond h48
- **THEN** the enrichment SHALL NOT produce consensus entries for h49 and beyond

#### Scenario: 3-hourly models contribute at their native hours only
- **WHEN** ecmwf_ifs025 provides data at h0, h3, h6, h9... (3-hourly)
- **THEN** it SHALL be included in consensus at h3, h6, h9... via exact ValidAt match
- **AND** it SHALL NOT contribute to h1, h2, h4, h5... (no data point at those exact times)
