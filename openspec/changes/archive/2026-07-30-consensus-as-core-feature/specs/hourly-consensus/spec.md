## MODIFIED Requirements

### Requirement: HourlyConsensusEnrichment computes consensus for every hour with sufficient model coverage

Hourly consensus SHALL be computed as part of `ConsensusSnapshot.Compute`, producing the `HourlyConsensus` facet. It SHALL NOT be an `IEnrichmentFeature`. The computation logic (iterating hourly parameters, collecting model values per horizon via exact point lookup, calling `ConsensusComputer`) is unchanged.

#### Scenario: Hourly consensus across models with different horizons
- **WHEN** `ConsensusSnapshot.Compute` is called with 3 models having horizons 48h, 72h, and 120h
- **THEN** `Hourly.Parameters` SHALL contain consensus for hours 0 through 72 (cutoff = second-to-last)

#### Scenario: Single model remaining stops consensus
- **WHEN** only 1 model has hourly data for a location
- **THEN** `Hourly.CutoffHour` SHALL be -1 and `Hourly.Parameters` SHALL be empty

#### Scenario: 3-hourly models contribute at their native hours only
- **WHEN** a model provides data at 3-hour intervals (h0, h3, h6...)
- **THEN** that model contributes only at those hours; other hours have one fewer contributing model

### Requirement: HourlyConsensusEnrichment is independently toggleable

The consensus pipeline stage SHALL always compute `ConsensusSnapshot` regardless of the `ConsensusOptions.Enabled` flag, because enrichments depend on it. The `Enabled` flag SHALL control only whether consensus egress (MQTT/gRPC output) is produced.

#### Scenario: Consensus always computed for enrichments
- **WHEN** `ConsensusOptions.Enabled` is false
- **THEN** `ConsensusSnapshot` is still computed and fed to enrichments, but no consensus egress events are emitted

#### Scenario: Consensus egress controlled by Enabled flag
- **WHEN** `ConsensusOptions.Enabled` is true
- **THEN** consensus egress events (`EgressEvent.ConsensusUpdate`) are emitted to the `EgressActor`

### Requirement: MQTT output publishes one topic per hour

The consensus egress SHALL publish one retained MQTT message per hourly horizon to `{baseTopic}/{location}/consensus/h{N}`.

#### Scenario: MQTT topics for consensus
- **WHEN** consensus egress processes a `ConsensusSnapshot` with `Hourly.Parameters`
- **THEN** one retained MQTT message per hour horizon is published to `{baseTopic}/{location}/consensus/h{N}`

#### Scenario: MQTT Discovery registers consensus device
- **WHEN** discovery runs for a location
- **THEN** a consensus device is registered with one sensor component per (hourly parameter, hour) pair
