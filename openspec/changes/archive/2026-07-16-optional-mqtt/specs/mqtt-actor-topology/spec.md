## MODIFIED Requirements

### Requirement: MqttConnectionActor owns the broker connection and MergeHub
The `MqttConnectionActor` SHALL be registered in the actor system only when `Mqtt.Enabled` is `true`. When registered, it SHALL own the `IMqttConnection` and `IMqttTransport` instances. It SHALL materialize a MergeHub sink for outbound `MqttMessage` flow. It SHALL handle connect, reconnect with exponential backoff, LWT (online/offline on the availability topic), and disconnection recovery. It SHALL vend `SinkRef<MqttMessage>` to requestors via a `RequestMqttSink`/`MqttSinkResponse` protocol.

#### Scenario: Connection established
- **WHEN** MQTT is enabled and `MqttConnectionActor` starts and connects successfully
- **THEN** it publishes "online" on the availability topic

#### Scenario: Actor not registered when MQTT disabled
- **WHEN** `Mqtt.Enabled` is `false`
- **THEN** `MqttConnectionActor` is not registered in the actor system

#### Scenario: SinkRef vended to requestor
- **WHEN** an actor sends `RequestMqttSink`
- **THEN** `MqttConnectionActor` responds with `MqttSinkResponse` containing a `SinkRef<MqttMessage>` connected to the MergeHub

#### Scenario: Graceful shutdown publishes offline
- **WHEN** `MqttConnectionActor` stops
- **THEN** it publishes "offline" on the availability topic before stopping

### Requirement: MqttEgressActor maps EgressEvent to MQTT messages

The `MqttEgressActor` SHALL be registered in the actor system only when `Mqtt.Enabled` is `true`. When registered, it SHALL subscribe to the EgressActor's BroadcastHub via `RequestEgressSource`, map each `EgressEvent` variant to `MqttMessage` instances using `StatePayloadBuilder` and `TopicScheme`, deduplicate by topic, and send to `MqttConnectionActor`'s MergeHub via `ISinkRef<MqttMessage>`.

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
- **WHEN** MQTT is enabled and `MqttEgressActor` receives an `EgressEvent.PerModelUpdate`
- **THEN** it SHALL create one retained `MqttMessage` per horizon entry using `TopicScheme.HorizonTopic` and send them to `MqttConnectionActor`

#### Scenario: Actor not registered when MQTT disabled
- **WHEN** `Mqtt.Enabled` is `false`
- **THEN** `MqttEgressActor` is not registered in the actor system

#### Scenario: MqttEgressActor deduplicates by topic
- **WHEN** `MqttEgressActor` maps an `EgressEvent` to an `MqttMessage` whose topic+payload are identical to the last published message on that topic
- **THEN** it SHALL skip publishing that message

### Requirement: DiscoveryActor publishes HA discovery configs
The `DiscoveryActor` SHALL be registered in the actor system only when `Mqtt.Enabled` is `true`. When registered, it SHALL request a `SinkRef<MqttMessage>` from `MqttConnectionActor`. It SHALL subscribe to the HA status topic (`{discoveryPrefix}/status`) via `MqttConnectionActor`. It SHALL NOT publish discovery on initial connection. Instead, it SHALL collect `ModelCapabilityLearned` messages from `ModelStateActor` instances. Once all expected (location, model) pairs have reported — or a configurable timeout expires (default: 2x poll interval) — it SHALL publish retained discovery config payloads for all reported devices using `DiscoveryPayloadBuilder`, filtered by each model's supported parameters and applicable horizons. Enrichment device discovery (consensus, alerts, derived, trends, indices, energy, history) SHALL be published alongside model devices once the timeout/collection completes, using their existing `IEnrichmentFeature.BuildDiscoveryPayload` methods unmodified. On HA birth ("online" on status topic), it SHALL re-publish all discovery config payloads using the current learned capability state. It SHALL be a no-op when `DiscoveryEnabled` is false.

#### Scenario: Discovery deferred until capabilities learned
- **WHEN** MQTT is enabled and `MqttConnectionActor` connects and notifies `DiscoveryActor`
- **THEN** `DiscoveryActor` SHALL NOT publish discovery immediately; it SHALL wait for `ModelCapabilityLearned` messages

#### Scenario: Actor not registered when MQTT disabled
- **WHEN** `Mqtt.Enabled` is `false`
- **THEN** `DiscoveryActor` is not registered in the actor system

#### Scenario: Timeout triggers partial discovery
- **WHEN** the timeout expires and 6 of 8 configured models have reported capabilities
- **THEN** `DiscoveryActor` SHALL publish discovery for the 6 reported models and enrichment devices; the 2 unreported models SHALL be skipped

#### Scenario: Discovery re-published on HA birth
- **WHEN** HA publishes "online" on the status topic after capabilities have been learned
- **THEN** `DiscoveryActor` re-publishes all discovery config payloads using current learned state
