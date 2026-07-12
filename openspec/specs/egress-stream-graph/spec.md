# egress-stream-graph Specification

## Purpose

Defines the Akka.Streams graph owned by the egress actor: a MergeHub that converges messages from multiple sources (egress consumer graph processing FetchOutcome.Success from the pipeline's BroadcastHub via SourceRef, discovery queue, availability queue, tombstone queue) into a single Publish Sink with bounded buffering and DropHead overflow.

## Requirements

### Requirement: All MQTT publishes flow through a single MergeHub
The egress actor SHALL materialize a `MergeHub<MqttMessage>` that accepts messages from multiple sources: the egress consumer graph (processing `FetchOutcome.Success` elements from the pipeline's BroadcastHub via SourceRef), and internal actor sources (discovery, availability, tombstone via Source.Queue). All messages SHALL converge into a single Publish Sink that calls the broker transport.

#### Scenario: State and discovery messages share the same publish path
- **WHEN** the egress consumer emits a state MqttMessage and the actor emits a discovery MqttMessage concurrently
- **THEN** both are published to the broker through the same Publish Sink

#### Scenario: Multiple sources can attach to the MergeHub
- **WHEN** the egress graph is materialized
- **THEN** at least four sources are connected: egress consumer (from BroadcastHub SourceRef), discovery queue, availability queue, and tombstone queue

### Requirement: MqttMessage is the unified publish protocol
All messages destined for the broker SHALL be expressed as `MqttMessage(string Topic, string Payload, bool Retain)`. The Publish Sink SHALL be content-agnostic — it does not interpret topic or payload semantics.

#### Scenario: MqttMessage carries topic, payload, and retain flag
- **WHEN** an `MqttMessage("njord/home/icon_d2/state", "{...}", true)` enters the hub
- **THEN** the sink publishes to topic `njord/home/icon_d2/state` with the given payload and retain=true

### Requirement: The egress actor pulls from the pipeline's BroadcastHub via SourceRef
The egress actor SHALL request a `SourceRef<FetchOutcome.Success>` from the PipelineActor via `RequestPipelineSource`. Upon receipt, the actor SHALL materialize a consumer graph: `SourceRef.Source -> Select(BuildPerHorizon) -> Select(DeltaFilter) -> SelectMany(-> MqttMessage) -> MergeHub.Sink`. The egress actor SHALL stash messages until the SourceRef is received.

#### Scenario: Egress obtains a SourceRef
- **WHEN** the egress actor sends `RequestPipelineSource` to the PipelineActor
- **THEN** the PipelineActor responds with a `PipelineSourceResponse` containing a `SourceRef<FetchOutcome.Success>`

#### Scenario: Consumer graph maps fetch outcomes to MqttMessages
- **WHEN** a `FetchOutcome.Success` for (lucerne, icon_d2) arrives via the SourceRef with 3 changed horizons
- **THEN** the consumer graph emits 3 `MqttMessage`(s) into the egress MergeHub

#### Scenario: SourceRef invalidation triggers re-request
- **WHEN** the PipelineActor restarts and the SourceRef becomes invalid
- **THEN** the egress actor detects the failure, re-sends `RequestPipelineSource`, and materializes a new consumer graph

### Requirement: The egress actor exposes a SinkRef for internal use only
The egress actor SHALL materialize a `SinkRef<MqttMessage>` connected to the MergeHub for internal use by its consumer graph. The egress actor SHALL NOT vend SinkRefs to external actors -- external data enters via the BroadcastHub SourceRef consumer, not via direct SinkRef push.

#### Scenario: No external SinkRef is vended
- **WHEN** an external actor requests a SinkRef from the egress actor
- **THEN** no SinkRef is provided; the external actor should use the pipeline's BroadcastHub instead

#### Scenario: Internal consumer graph feeds into MergeHub
- **WHEN** the egress consumer graph processes a `FetchOutcome.Success`
- **THEN** the resulting `MqttMessage`(s) flow into the MergeHub through the internal consumer connection

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
