## MODIFIED Requirements

### Requirement: MqttPublisherActor transforms domain results to MQTT messages

The `MqttPublisherActor` is replaced by two actors:

1. **`ModelStateActor`** (in `Njord.Egress`) — subscribes to the Pipeline BroadcastHub, transforms `FetchOutcome.Success` into `EgressEvent.PerModelUpdate`, and sends to the EgressActor's MergeHub. See `egress-event` spec.

2. **`MqttEgressActor`** (in `Njord.Mqtt`) — subscribes to the EgressActor's BroadcastHub via `RequestEgressSource`, maps each `EgressEvent` variant to `MqttMessage` instances using `StatePayloadBuilder` and `TopicScheme`, deduplicates by topic, and sends to `MqttConnectionActor`'s MergeHub via `ISinkRef<MqttMessage>`.

The `MqttEgressActor` SHALL handle all `EgressEvent` variants:
- `PerModelUpdate` → per-horizon `MqttMessage` via `TopicScheme.HorizonTopic`
- `ConsensusUpdate` → `StatePayloadBuilder.FromConsensus`
- `AlertUpdate` → `StatePayloadBuilder.FromAlerts`
- `DerivedUpdate` → `StatePayloadBuilder.FromDerived`
- `TrendUpdate` → `StatePayloadBuilder.FromTrends`
- `IndexUpdate` → `StatePayloadBuilder.FromIndices`
- `EnergyUpdate` → `StatePayloadBuilder.FromEnergy`
- `HistoryUpdate` → `StatePayloadBuilder.FromHistory`

#### Scenario: MqttEgressActor maps PerModelUpdate to MQTT messages
- **WHEN** `MqttEgressActor` receives an `EgressEvent.PerModelUpdate`
- **THEN** it SHALL create one retained `MqttMessage` per horizon entry using `TopicScheme.HorizonTopic` and send them to `MqttConnectionActor`

#### Scenario: MqttEgressActor maps enrichment events to MQTT messages
- **WHEN** `MqttEgressActor` receives a `ConsensusUpdate`, `AlertUpdate`, `DerivedUpdate`, `TrendUpdate`, `IndexUpdate`, `EnergyUpdate`, or `HistoryUpdate`
- **THEN** it SHALL use the corresponding `StatePayloadBuilder.From*` method and send the resulting `MqttMessage` instances to `MqttConnectionActor`

#### Scenario: MqttEgressActor deduplicates by topic
- **WHEN** `MqttEgressActor` maps an `EgressEvent` to an `MqttMessage` whose topic+payload are identical to the last published message on that topic
- **THEN** it SHALL skip publishing that message

#### Scenario: Wire format is unchanged
- **WHEN** `MqttEgressActor` publishes messages for any `EgressEvent` variant
- **THEN** the MQTT topics, JSON payloads, and retain flags SHALL be identical to those produced by the previous `MqttPublisherActor` and direct-to-MQTT enrichment streams

## REMOVED Requirements

### Requirement: MqttPublisherActor transforms domain results to MQTT messages
**Reason**: Split into `ModelStateActor` (domain → EgressEvent) and `MqttEgressActor` (EgressEvent → MQTT). See MODIFIED section above for the replacement.
**Migration**: Replace `MqttPublisherActor` registration in `NjordActorSystemSetup` with `ModelStateActor` and `MqttEgressActor`.
