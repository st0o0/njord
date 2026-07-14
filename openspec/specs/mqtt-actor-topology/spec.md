# mqtt-actor-topology Specification

## Purpose

Actor topology for MQTT concerns: MqttConnectionActor owns the physical broker connection and MergeHub, MqttPublisherActor transforms domain results into MqttMessages, DiscoveryActor handles HA discovery config publishing. All live in `Njord.Mqtt`.

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

### Requirement: MqttPublisherActor transforms domain results to MQTT messages
The `MqttPublisherActor` SHALL register itself with `EgressActor` via `RegisterPublisher` on startup. It SHALL request a `SinkRef<MqttMessage>` from `MqttConnectionActor`. On receiving `PublishStateResult` messages from `EgressActor`, it SHALL transform domain result records into `MqttMessage` instances using `StatePayloadBuilder` and push them into the MergeHub sink. It SHALL maintain a delta-publishing cache per (location, model, horizon) to skip unchanged payloads.

#### Scenario: Domain result transformed to MQTT messages
- **WHEN** `MqttPublisherActor` receives a `PublishStateResult` containing a `ConsensusResult`
- **THEN** it produces MQTT messages on the consensus topic scheme and pushes them to the MergeHub

#### Scenario: Unchanged payload is skipped (delta publishing)
- **WHEN** the same payload was published in the previous cycle
- **THEN** the message is not re-published

#### Scenario: Registers with EgressActor on startup
- **WHEN** `MqttPublisherActor` starts
- **THEN** it sends `RegisterPublisher` to `EgressActor`

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
