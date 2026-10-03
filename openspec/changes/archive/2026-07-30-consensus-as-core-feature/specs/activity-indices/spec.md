## MODIFIED Requirements

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

### Requirement: IndexResult aggregates all indices and serializes to MQTT

`IndexResult` SHALL derive its location from `ConsensusSnapshot.Location`.

#### Scenario: Index message content
- **WHEN** indices are serialized to MQTT
- **THEN** one retained message is published with all scores and envelope fields

#### Scenario: Retained message
- **WHEN** an index message is published
- **THEN** it is retained
