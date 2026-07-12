## MODIFIED Requirements

### Requirement: All MQTT publishes flow through a single MergeHub
The egress actor SHALL materialize a `MergeHub<MqttMessage>` that accepts messages from multiple sources: the egress consumer graph (processing `FetchOutcome.Success` elements from the pipeline's BroadcastHub via SourceRef), and internal actor sources (discovery, availability, tombstone via Source.Queue). All messages SHALL converge into a single Publish Sink that calls the broker transport.

#### Scenario: State and discovery messages share the same publish path
- **WHEN** the egress consumer emits a state MqttMessage and the actor emits a discovery MqttMessage concurrently
- **THEN** both are published to the broker through the same Publish Sink

#### Scenario: Multiple sources can attach to the MergeHub
- **WHEN** the egress graph is materialized
- **THEN** at least four sources are connected: egress consumer (from BroadcastHub SourceRef), discovery queue, availability queue, and tombstone queue

### Requirement: The egress actor pulls from the pipeline's BroadcastHub via SourceRef
The egress actor SHALL request a `SourceRef<FetchOutcome.Success>` from the PipelineActor via `RequestPipelineSource`. Upon receipt, the actor SHALL materialize a consumer graph: `SourceRef.Source → Select(BuildPerHorizon) → Select(DeltaFilter) → SelectMany(→ MqttMessage) → MergeHub.Sink`. The egress actor SHALL stash messages until the SourceRef is received.

#### Scenario: Egress obtains a SourceRef
- **WHEN** the egress actor sends `RequestPipelineSource` to the PipelineActor
- **THEN** the PipelineActor responds with a `PipelineSourceResponse` containing a `SourceRef<FetchOutcome.Success>`

#### Scenario: Consumer graph maps fetch outcomes to MqttMessages
- **WHEN** a `FetchOutcome.Success` for (lucerne, icon_d2) arrives via the SourceRef with 3 changed horizons
- **THEN** the consumer graph emits 3 `MqttMessage`(s) into the egress MergeHub

#### Scenario: SourceRef invalidation triggers re-request
- **WHEN** the PipelineActor restarts and the SourceRef becomes invalid
- **THEN** the egress actor detects the failure, re-sends `RequestPipelineSource`, and materializes a new consumer graph

### Requirement: The Publish Sink buffers during disconnect with DropHead overflow
The Publish Sink SHALL use a bounded buffer (configurable, default 64 messages). When the broker is unreachable and the buffer is full, the oldest messages SHALL be dropped (DropHead strategy). On reconnect, buffered messages SHALL drain in order.

#### Scenario: Short outage buffers and drains
- **WHEN** the broker disconnects for 30 seconds and 10 messages arrive
- **THEN** all 10 are buffered and published in order on reconnect

#### Scenario: Extended outage drops oldest messages
- **WHEN** the broker is down and 100 messages arrive into a 64-message buffer
- **THEN** the newest 64 messages are retained; the oldest 36 are dropped

## MODIFIED Requirements

### Requirement: The egress actor exposes a SinkRef for external producers
The egress actor SHALL materialize a `SinkRef<MqttMessage>` connected to the MergeHub for internal use by its consumer graph. The egress actor SHALL NOT vend SinkRefs to external actors — external data enters via the BroadcastHub SourceRef consumer, not via direct SinkRef push.

#### Scenario: No external SinkRef is vended
- **WHEN** an external actor requests a SinkRef from the egress actor
- **THEN** no SinkRef is provided; the external actor should use the pipeline's BroadcastHub instead

#### Scenario: Internal consumer graph feeds into MergeHub
- **WHEN** the egress consumer graph processes a `FetchOutcome.Success`
- **THEN** the resulting `MqttMessage`(s) flow into the MergeHub through the internal consumer connection

## REMOVED Requirements

### Requirement: The egress actor exposes a SinkRef for external producers
**Reason:** The push model (PipelineActor pushes into EgressActor's SinkRef) is replaced by a pull model (EgressActor pulls from PipelineActor's BroadcastHub via SourceRef). The `RequestEgressSink`/`EgressSinkResponse` handshake is removed.
**Migration:** Remove `RequestEgressSink`/`EgressSinkResponse`. EgressActor sends `RequestPipelineSource` to PipelineActor instead.
