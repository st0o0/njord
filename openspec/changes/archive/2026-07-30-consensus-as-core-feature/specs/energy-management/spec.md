## MODIFIED Requirements

### Requirement: Energy computation evaluates consensus values instead of per-model aggregation

`EnergyResult.Compute` SHALL accept a `ConsensusSnapshot` instead of `ModelSnapshot`. Heating demand, COP estimate, COP-optimal hours, shading, battery strategy, and night cooling SHALL be computed from consensus median values. Pessimistic envelope fields SHALL be derived from consensus spread/confidence interval instead of per-model evaluation.

#### Scenario: Computed from consensus medians
- **WHEN** `EnergyResult.Compute` is called with a `ConsensusSnapshot`
- **THEN** all energy values use consensus median temperature, radiation, wind, and cloud cover

#### Scenario: Pessimistic envelope from consensus spread
- **WHEN** consensus spread is available
- **THEN** `HeatingDemandMax` and `CopEstimateMin` SHALL be derived from the pessimistic end of the consensus confidence interval

#### Scenario: COP-optimal hours from consensus
- **WHEN** COP-optimal hours are computed
- **THEN** the ranking uses consensus median temperatures per hour

### Requirement: EnergyResult aggregates all energy values and serializes to MQTT

`EnergyResult` SHALL derive its location from `ConsensusSnapshot.Location`.

#### Scenario: Energy message content
- **WHEN** energy values are serialized to MQTT
- **THEN** one retained message is published with all fields

#### Scenario: Retained message
- **WHEN** an energy message is published
- **THEN** it is retained
