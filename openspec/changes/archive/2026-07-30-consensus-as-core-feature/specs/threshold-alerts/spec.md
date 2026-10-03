## MODIFIED Requirements

### Requirement: Frost warning evaluates minimum temperature across models

Frost warning SHALL evaluate temperature from `ConsensusSnapshot.Hourly` consensus medians instead of iterating raw model data. Confidence SHALL be derived from the consensus agreement score.

#### Scenario: All models predict frost
- **WHEN** consensus median temperature is ≤ 0°C with agreement ≥ 0.8
- **THEN** a frost alert is produced with high confidence

#### Scenario: No model predicts frost
- **WHEN** consensus median temperature is > 2°C across all horizons
- **THEN** no frost alert is produced

#### Scenario: Partial agreement
- **WHEN** consensus median temperature is ≤ 0°C but agreement is < 0.5
- **THEN** a frost alert is produced with low confidence

### Requirement: Heat warning evaluates apparent temperature max with tiered severity

Heat warning SHALL use consensus median apparent temperature from `ConsensusSnapshot.Hourly`.

#### Scenario: Extreme heat
- **WHEN** consensus median apparent temperature exceeds the extreme threshold
- **THEN** a heat alert with severity "extreme" is produced

#### Scenario: Moderate heat
- **WHEN** consensus median apparent temperature exceeds the moderate threshold but not extreme
- **THEN** a heat alert with severity "moderate" is produced

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

### Requirement: AlertResult aggregates all alerts for a location

`AlertEvaluator.EvaluateAll` SHALL accept a `ConsensusSnapshot` instead of `ModelSnapshot`. The location is taken from `ConsensusSnapshot.Location`.

#### Scenario: Serialization to MQTT messages
- **WHEN** alerts are serialized
- **THEN** each alert produces one MQTT message on its sub-topic

#### Scenario: None severity still publishes
- **WHEN** no threshold is exceeded for an alert type
- **THEN** a "none" severity alert is published
