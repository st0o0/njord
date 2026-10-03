## MODIFIED Requirements

### Requirement: MergeHub converges messages from multiple sources into a single publish sink

The `MqttConnectionActor`'s MergeHub SHALL receive `MqttMessage` instances from the following sources:
- `MqttEgressActor` (replaces `MqttPublisherActor` — handles both per-model and enrichment data)
- `DiscoveryActor` (unchanged)
- Availability Source.Queue (unchanged)
- Tombstone Source.Queue (unchanged)

The `MqttConnectionActor` SHALL no longer receive messages from `MqttPublisherActor` (deleted) or directly from `EnrichmentActor` (which now routes through EgressActor → MqttEgressActor).

#### Scenario: MqttEgressActor is the sole data publisher
- **WHEN** the MQTT egress stream graph is materialized
- **THEN** `MqttEgressActor` SHALL be the only actor sending per-model state and enrichment data as `MqttMessage` to the MqttConnectionActor's MergeHub

#### Scenario: Discovery and availability paths are unchanged
- **WHEN** the MQTT egress stream graph is materialized
- **THEN** `DiscoveryActor`, the availability Source.Queue, and the tombstone Source.Queue SHALL continue to feed into the MergeHub as before

### Requirement: Pipeline SourceRef consumer maps FetchOutcome to state payloads

This requirement is removed from `egress-stream-graph` — the pipeline-to-state mapping is now handled by `ModelStateActor` in `Njord.Egress`, which produces `EgressEvent.PerModelUpdate` and feeds the EgressActor's MergeHub. The MQTT-specific mapping is in `MqttEgressActor`.

#### Scenario: No direct pipeline-to-MQTT path
- **WHEN** the MqttConnectionActor's egress stream graph is materialized
- **THEN** no stream stage SHALL directly consume `FetchOutcome` from the Pipeline BroadcastHub — that responsibility belongs to `ModelStateActor`
