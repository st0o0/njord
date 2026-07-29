# mqtt-actor-topology Specification

## Purpose

Actor topology for MQTT concerns: MqttConnectionActor owns the physical broker connection and MergeHub, MqttEgressActor maps EgressEvent to MqttMessages, DiscoveryActor handles HA discovery config publishing. All live in `Njord.Mqtt`.

## Requirements

### Requirement: MqttConnectionActor owns the broker connection and MergeHub
The `MqttConnectionActor` SHALL be registered in the actor system only when `Mqtt.Enabled` is `true`. When registered, it SHALL own the `IMqttConnection` and `IMqttTransport` instances. It SHALL materialize a MergeHub sink for outbound `MqttMessage` flow. It SHALL handle connect, reconnect with exponential backoff, LWT (online/offline on the availability topic), and disconnection recovery. It SHALL vend `SinkRef<MqttMessage>` to requestors via a `RequestMqttSink`/`MqttSinkResponse` protocol.

When SinkRef materialization fails, the actor SHALL send `Status.Failure(ex)` to the requesting actor. The actor SHALL NOT return `null` from the failure path.

The actor SHALL use the injected `TimeProvider` for all timestamp operations (health state transitions). It SHALL NOT use `DateTimeOffset.UtcNow` directly.

#### Scenario: Connection established
- **WHEN** the actor connects to the broker
- **THEN** it publishes "online" on the availability topic

#### Scenario: Actor not registered when MQTT disabled
- **WHEN** Mqtt.Enabled is false
- **THEN** the actor is not registered in the actor system

#### Scenario: Connection lost and reconnected
- **WHEN** the broker connection is lost
- **THEN** the actor reconnects with exponential backoff

#### Scenario: SinkRef vended to requestor
- **WHEN** a requestor sends RequestMqttSink and materialization succeeds
- **THEN** it receives MqttSinkResponse with a valid SinkRef

#### Scenario: SinkRef materialization failure sends Status.Failure
- **WHEN** a requestor sends RequestMqttSink and materialization fails
- **THEN** the requestor receives Status.Failure(ex), not null

#### Scenario: Health timestamps use TimeProvider
- **WHEN** the actor records a connect or disconnect timestamp
- **THEN** it uses `TimeProvider.GetUtcNow()` instead of `DateTimeOffset.UtcNow`

#### Scenario: Graceful shutdown publishes offline
- **WHEN** the actor stops
- **THEN** it publishes "offline" on the availability topic

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
The `DiscoveryActor` SHALL be registered in the actor system only when `Mqtt.Enabled` is `true`. When registered, it SHALL request a `SinkRef<MqttMessage>` from `MqttConnectionActor` and a `SourceRef<EgressEvent>` from `EgressActor`. It SHALL materialize the egress source stream, filtering for `EgressEvent.CapabilityLearned` events and piping them to itself. It SHALL subscribe to the HA status topic. It SHALL NOT publish discovery on initial connection. Instead, it SHALL collect capability events from the egress hub. Once all expected (location, model) pairs have reported -- or a configurable timeout expires (default: 2x poll interval) -- it SHALL publish retained discovery config payloads. On HA birth ("online" on status topic), it SHALL re-publish all discovery config payloads. It SHALL be a no-op when `DiscoveryEnabled` is false.

The DiscoveryActor SHALL `Context.Watch()` both the MqttConnectionActor and the EgressActor. On `Terminated`, it SHALL null its stale refs, re-request fresh refs from the restarted actors, and transition back to its WaitingForRefs state.

#### Scenario: DiscoveryActor subscribes to EgressActor hub
- **WHEN** the actor starts
- **THEN** it requests a SourceRef from EgressActor

#### Scenario: Discovery deferred until capabilities learned
- **WHEN** the actor starts
- **THEN** it waits for capability events before publishing discovery

#### Scenario: All capabilities received triggers discovery
- **WHEN** all expected location/model pairs report capabilities
- **THEN** retained discovery config payloads are published

#### Scenario: Actor not registered when MQTT disabled
- **WHEN** Mqtt.Enabled is false
- **THEN** the actor is not registered

#### Scenario: Timeout triggers partial discovery
- **WHEN** the capability timeout expires with incomplete reports
- **THEN** discovery is published for the capabilities received so far

#### Scenario: Discovery re-published on HA birth
- **WHEN** "online" is received on the HA status topic
- **THEN** all discovery config payloads are re-published

#### Scenario: DiscoveryActor watches upstream actors
- **WHEN** MqttConnectionActor or EgressActor is resolved in PreStart
- **THEN** DiscoveryActor calls Context.Watch on both

#### Scenario: Upstream actor restart triggers ref re-request
- **WHEN** DiscoveryActor receives Terminated for MqttConnectionActor or EgressActor
- **THEN** it nulls its stale refs and re-requests from the restarted actors
- **THEN** it transitions to WaitingForRefs

#### Scenario: Late capability after timeout triggers incremental discovery
- **WHEN** a capability event arrives after the initial timeout
- **THEN** discovery is published incrementally for the new capability

#### Scenario: Discovery disabled
- **WHEN** DiscoveryEnabled is false
- **THEN** no discovery payloads are published

#### Scenario: Capability expansion triggers re-discovery for affected device
- **WHEN** a model reports additional parameters after initial discovery
- **THEN** discovery is re-published for that device
