## MODIFIED Requirements

### Requirement: Proto messages map all enrichment domain types

The `ConsensusUpdate` proto message SHALL carry hourly and daily parameter consensus. The `daily_summaries` field (which mapped `DailyConsensusSummary`) SHALL be deprecated. Daily consensus SHALL be represented as `ParameterConsensus` entries in a `daily_parameters` repeated field with `dN` horizon keys.

#### Scenario: AlertUpdate carries 9 alert types
- **WHEN** an alert update is mapped to proto
- **THEN** all 9 alert types are represented

#### Scenario: TrendUpdate carries parameter trends and timing
- **WHEN** a trend update is mapped to proto
- **THEN** parameter trends, precipitation timing, and extrema timing are included

#### Scenario: ConsensusUpdate carries per-parameter per-horizon data
- **WHEN** a `ConsensusSnapshot` is mapped to `ConsensusUpdate`
- **THEN** `parameters` contains hourly `ParameterConsensus` with `hN` horizon keys and `daily_parameters` contains daily `ParameterConsensus` with `dN` horizon keys

#### Scenario: ConsensusUpdate no longer carries daily summaries
- **WHEN** a `ConsensusSnapshot` is mapped to `ConsensusUpdate`
- **THEN** the `daily_summaries` field SHALL be empty (deprecated)
