# mqtt-actor-topology Specification

## Purpose

Actor topology for MQTT concerns: MqttConnectionActor owns the physical broker connection and MergeHub, MqttEgressActor maps EgressEvent to MqttMessages, DiscoveryActor handles HA discovery config publishing. All live in `Njord.Mqtt`.

## Requirements

### Requirement: MqttConnectionActor owns the broker connection and MergeHub
The `MqttConnectionActor` SHALL own the `IMqttConnection` and `IMqttTransport` instances. It SHALL materialize a MergeHub sink for outbound `MqttMessage` flow. It SHALL handle connect, reconnect with exponential backoff, LWT (online/offline on the availability topic), and disconnection recovery. It SHALL vend `SinkRef<MqttMessage>` to requestors via a `RequestMqttSink`/`MqttSinkResponse` protocol.

#### Scenario: Connection established
- **WHEN** `MqttConnectionActor` starts and connects successfully
- **THEN** it publishes "online" on the availability topic

#### Scenario: Connection lost and reconnected
- **WHEN** the broker connection drops
- **THEN** `MqttConnectionActor` reconnects with exponential backoff and re-publishes "online"

#### Scenario: SinkRef vended to requestor
- **WHEN** an actor sends `RequestMqttSink`
- **THEN** `MqttConnectionActor` responds with `MqttSinkResponse` containing a `SinkRef<MqttMessage>` connected to the MergeHub

#### Scenario: Graceful shutdown publishes offline
- **WHEN** `MqttConnectionActor` stops
- **THEN** it publishes "offline" on the availability topic before stopping

### Requirement: MqttEgressActor maps EgressEvent to MQTT messages

The `MqttEgressActor` SHALL subscribe to the EgressActor's BroadcastHub via `RequestEgressSource`, map each `EgressEvent` variant to `MqttMessage` instances using `StatePayloadBuilder` and `TopicScheme`, deduplicate by topic, and send to `MqttConnectionActor`'s MergeHub via `ISinkRef<MqttMessage>`.

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

### Requirement: DiscoveryActor publishes HA discovery configs
The `DiscoveryActor` SHALL request a `SinkRef<MqttMessage>` from `MqttConnectionActor`. It SHALL subscribe to the HA status topic (`{discoveryPrefix}/status`) via `MqttConnectionActor`. On connection and on HA birth ("online" on status topic), it SHALL publish retained discovery config payloads for all configured devices (per-model devices, consensus, alerts, derived, trends, indices, energy, history) using `DiscoveryPayloadBuilder`. It SHALL be a no-op when `DiscoveryEnabled` is false.

#### Scenario: Discovery published on connect
- **WHEN** `MqttConnectionActor` connects and notifies `DiscoveryActor`
- **THEN** `DiscoveryActor` publishes discovery config payloads for all devices

#### Scenario: Discovery re-published on HA birth
- **WHEN** HA publishes "online" on the status topic
- **THEN** `DiscoveryActor` re-publishes all discovery config payloads

#### Scenario: Discovery disabled
- **WHEN** `DiscoveryEnabled` is false
- **THEN** `DiscoveryActor` does not publish configs and does not subscribe to HA status
