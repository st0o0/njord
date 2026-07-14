# egress-stream-graph Specification

## Purpose

Defines the Akka.Streams graph owned by the MqttConnectionActor: a MergeHub that converges messages from multiple producers (MqttEgressActor, DiscoveryActor, availability queue, tombstone queue) into a single Publish Sink with bounded buffering and DropHead overflow.

## Requirements

### Requirement: MergeHub converges messages from multiple sources into a single publish sink
The `MqttConnectionActor` SHALL materialize a MergeHub of `MqttMessage`. The publish sink SHALL process messages with `SelectAsync(1)` through `IMqttTransport.SendAsync` with `Supervision.Directive.Resume` on errors. The MergeHub SHALL receive `MqttMessage` instances from the following sources:
- `MqttEgressActor` (handles both per-model and enrichment data via EgressEvent mapping)
- `DiscoveryActor` (unchanged)
- Availability Source.Queue (unchanged)
- Tombstone Source.Queue (unchanged)

The `MqttConnectionActor` SHALL no longer receive messages from `MqttPublisherActor` (deleted) or directly from `EnrichmentActor` (which now routes through EgressActor → MqttEgressActor). The availability source queue (online/offline) SHALL remain internal to `MqttConnectionActor`.

#### Scenario: MqttEgressActor is the sole data publisher
- **WHEN** the MQTT egress stream graph is materialized
- **THEN** `MqttEgressActor` SHALL be the only actor sending per-model state and enrichment data as `MqttMessage` to the MqttConnectionActor's MergeHub

#### Scenario: Discovery and availability paths are unchanged
- **WHEN** the MQTT egress stream graph is materialized
- **THEN** `DiscoveryActor`, the availability Source.Queue, and the tombstone Source.Queue SHALL continue to feed into the MergeHub as before

#### Scenario: Transport error resumes the stream
- **WHEN** `IMqttTransport.SendAsync` throws for one message
- **THEN** the stream resumes and processes subsequent messages

### Requirement: MqttMessage is the unified publish protocol
All messages destined for the broker SHALL be expressed as `MqttMessage(string Topic, string Payload, bool Retain)`. The Publish Sink SHALL be content-agnostic — it does not interpret topic or payload semantics.

#### Scenario: MqttMessage carries topic, payload, and retain flag
- **WHEN** an `MqttMessage("njord/home/icon_d2/state", "{...}", true)` enters the hub
- **THEN** the sink publishes to topic `njord/home/icon_d2/state` with the given payload and retain=true

### Requirement: Pipeline-to-state mapping is handled by ModelStateActor
The pipeline-to-state mapping is handled by `ModelStateActor` in `Njord.Egress`, which produces `EgressEvent.PerModelUpdate` and feeds the EgressActor's MergeHub. The MQTT-specific mapping is in `MqttEgressActor`. No stream stage in the MqttConnectionActor's graph SHALL directly consume `FetchOutcome` from the Pipeline BroadcastHub.

#### Scenario: No direct pipeline-to-MQTT path
- **WHEN** the MqttConnectionActor's egress stream graph is materialized
- **THEN** no stream stage SHALL directly consume `FetchOutcome` from the Pipeline BroadcastHub — that responsibility belongs to `ModelStateActor`

### Requirement: The Publish Sink buffers during disconnect with DropHead overflow
The Publish Sink SHALL use a bounded buffer (configurable, default 64 messages). When the broker is unreachable and the buffer is full, the oldest messages SHALL be dropped (DropHead strategy). On reconnect, buffered messages SHALL drain in order.

#### Scenario: Short outage buffers and drains
- **WHEN** the broker disconnects for 30 seconds and 10 messages arrive
- **THEN** all 10 are buffered and published in order on reconnect

#### Scenario: Extended outage drops oldest messages
- **WHEN** the broker is down and 100 messages arrive into a 64-message buffer
- **THEN** the newest 64 messages are retained; the oldest 36 are dropped

### Requirement: Discovery messages are fed via Source.Queue on lifecycle events
The egress actor SHALL offer discovery `MqttMessage` payloads into a dedicated Source.Queue on: (a) successful broker connection, and (b) HA birth (`homeassistant/status` = `online`). Discovery messages SHALL flow through the same MergeHub as state messages.

#### Scenario: Discovery on connect
- **WHEN** the broker connection succeeds
- **THEN** one discovery MqttMessage per configured (location, model) pair is offered into the discovery queue

#### Scenario: Discovery on HA birth
- **WHEN** `homeassistant/status` receives `online`
- **THEN** all discovery MqttMessages are re-offered into the discovery queue

### Requirement: Availability messages are fed via Source.Queue
The egress actor SHALL offer an `online` availability MqttMessage after every successful connect, and an `offline` message during graceful shutdown. These flow through the MergeHub alongside other messages.

#### Scenario: Online on connect
- **WHEN** the broker connection succeeds
- **THEN** an `MqttMessage(availabilityTopic, "online", retain: true)` is offered into the availability queue

#### Scenario: Offline on shutdown
- **WHEN** the actor is stopping
- **THEN** an `MqttMessage(availabilityTopic, "offline", retain: true)` is offered and the sink drains before the actor stops

### Requirement: Tombstone messages are fed via Source.Queue on stale config detection
When the egress actor detects a retained device config topic that is not in the current configuration set, it SHALL offer a tombstone `MqttMessage(topic, "", retain: true)` into the tombstone queue.

#### Scenario: Stale config is tombstoned
- **WHEN** a retained message on `homeassistant/device/njord_home_removed_model/config` is received and the device is not in config
- **THEN** an `MqttMessage("homeassistant/device/njord_home_removed_model/config", "", true)` is offered into the tombstone queue
