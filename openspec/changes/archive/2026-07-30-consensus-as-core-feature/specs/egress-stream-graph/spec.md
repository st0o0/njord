## MODIFIED Requirements

### Requirement: MergeHub converges messages from multiple sources into a single publish sink

The MergeHub SHALL accept messages from: (1) `MqttEgressActor` for enrichment state messages, (2) consensus egress for consensus state messages, (3) discovery and availability paths. Consensus state messages originate from the consensus pipeline stage, not from the enrichment pathway.

#### Scenario: MqttEgressActor is the sole data publisher
- **WHEN** enrichment updates arrive at the `MqttEgressActor`
- **THEN** they are formatted and fed into the MergeHub

#### Scenario: Consensus egress feeds the MergeHub
- **WHEN** `EgressEvent.ConsensusUpdate` arrives at the `MqttEgressActor`
- **THEN** consensus state messages are formatted and fed into the MergeHub

#### Scenario: Discovery and availability paths are unchanged
- **WHEN** discovery or availability events occur
- **THEN** they follow existing `Source.Queue` paths into the MergeHub

#### Scenario: Transport error resumes the stream
- **WHEN** a transport error occurs during publish
- **THEN** the stream resumes without data loss
