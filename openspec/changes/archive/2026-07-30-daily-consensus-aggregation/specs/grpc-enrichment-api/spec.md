## MODIFIED Requirements

### Requirement: Proto messages map all enrichment domain types
The proto definition SHALL include messages for all 7 enrichment types with full fidelity to the domain Result records. `EnrichmentProtoMapper.MapConsensus` SHALL map both the hourly `Parameters` and the `DailySummaries` from `ConsensusResult` to the proto `ConsensusUpdate` (fields `parameters` and `daily` respectively).

#### Scenario: AlertUpdate carries 9 alert types
- **WHEN** an `AlertUpdate` is serialized
- **THEN** it SHALL contain up to 9 alerts each with type (enum), severity (enum), and confidence (double)

#### Scenario: TrendUpdate carries parameter trends and timing
- **WHEN** a `TrendUpdate` is serialized
- **THEN** it SHALL contain parameter trends (direction + delta), precipitation timing (starts/ends in hours), extrema timing, stability, and decay rate

#### Scenario: ConsensusUpdate carries per-parameter per-horizon data
- **WHEN** a `ConsensusUpdate` is serialized
- **THEN** it SHALL contain per-parameter entries, each with per-horizon consensus values (median, spread, agreement, available model count)

#### Scenario: ConsensusUpdate carries daily summaries
- **WHEN** a `ConsensusUpdate` is serialized and the domain `ConsensusResult` contains `DailySummaries`
- **THEN** the proto `ConsensusUpdate.daily` SHALL contain one `DailyConsensus` entry per calendar day with temperature_max, temperature_min, precipitation_sum, wind_speed_max, weather_code, spread, agreement, and available_models
