# enrichment-actor Specification

## Purpose

The EnrichmentActor consumes the pipeline's BroadcastHub via SourceRef, maintains a running ModelSnapshot via Scan, fans out to consumer streams through a second BroadcastHub, and routes computed MqttMessages to the egress actor's MergeHub via SinkRef. Consumer streams are materialized only when enabled in configuration.

## ADDED Requirements

### Requirement: The EnrichmentActor requests a SourceRef from the PipelineActor
The `EnrichmentActor` SHALL send a `RequestPipelineSource` message to the `PipelineActor` on startup. Upon receiving a `PipelineSourceResponse`, it SHALL transition from `WaitingForSourceRef` to its operational state. Messages received before the SourceRef arrives SHALL be stashed.

#### Scenario: SourceRef received transitions to operational
- **WHEN** the EnrichmentActor starts and receives a `PipelineSourceResponse`
- **THEN** it transitions to its operational state and unstashes pending messages

#### Scenario: Messages are stashed before SourceRef
- **WHEN** the EnrichmentActor receives messages before the `PipelineSourceResponse`
- **THEN** the messages are stashed and replayed after the transition

#### Scenario: PipelineActor restart triggers re-request
- **WHEN** the EnrichmentActor receives a `Terminated` message for the PipelineActor
- **THEN** it sends a new `RequestPipelineSource` to the restarted PipelineActor

### Requirement: The EnrichmentActor maintains a ModelSnapshot via Scan
The `EnrichmentActor` SHALL materialize a consumer on the pipeline's `SourceRef<FetchOutcome>` using a `Scan` operator to accumulate a `ModelSnapshot`. Each `FetchOutcome.Success` SHALL update the snapshot with the contained `ModelForecast`. `FetchOutcome.Failure` elements SHALL not modify the snapshot. Only snapshots where `HasChanged` is `true` SHALL propagate downstream.

#### Scenario: Success updates the snapshot
- **WHEN** a `FetchOutcome.Success` for (lucerne, icon_d2) arrives
- **THEN** the snapshot is updated with that forecast and emitted downstream

#### Scenario: Failure does not change the snapshot
- **WHEN** a `FetchOutcome.Failure` arrives
- **THEN** the snapshot remains unchanged and no element is emitted

#### Scenario: Unchanged data is filtered
- **WHEN** a `FetchOutcome.Success` arrives with data identical to what is already in the snapshot
- **THEN** `HasChanged` is `false` and the snapshot is not emitted to consumers

### Requirement: The EnrichmentActor fans out via a second BroadcastHub
The `EnrichmentActor` SHALL materialize a `BroadcastHub<ModelSnapshot>` from the Scan output. Consumer streams SHALL each independently subscribe to this BroadcastHub. Each consumer SHALL receive every changed snapshot.

#### Scenario: Two consumers receive the same snapshot
- **WHEN** a changed `ModelSnapshot` enters the BroadcastHub and two consumers are subscribed
- **THEN** both consumers receive the snapshot independently

### Requirement: The EnrichmentActor requests an MqttSinkRef from the EgressActor
The `EnrichmentActor` SHALL send a `RequestMqttSink` message to the `MqttEgressActor` and receive a `MqttSinkResponse` containing a `SinkRef<MqttMessage>`. All consumer stream outputs SHALL sink into this SinkRef, routing computed messages through the egress actor's existing MergeHub transport.

#### Scenario: SinkRef obtained and used by consumers
- **WHEN** the EnrichmentActor receives an `MqttSinkResponse`
- **THEN** consumer streams materialize their sinks using the provided `SinkRef<MqttMessage>`

#### Scenario: EgressActor restart triggers re-request
- **WHEN** the EnrichmentActor receives a `Terminated` message for the MqttEgressActor
- **THEN** it re-requests the MqttSinkRef from the restarted actor

### Requirement: Consumer streams are materialized only when enabled
The `EnrichmentActor` SHALL read `EnrichmentOptions` from configuration. For each consumer type (e.g. `Consensus`), if `Enabled` is `false`, the consumer stream SHALL NOT be materialized — no BroadcastHub subscriber is created. If `Enabled` is `true`, the consumer stream SHALL be materialized and connected to the BroadcastHub and MqttSinkRef.

#### Scenario: Disabled consumer is not materialized
- **WHEN** `EnrichmentOptions.Consensus.Enabled` is `false`
- **THEN** no consensus consumer stream is materialized; the BroadcastHub has no subscriber for consensus

#### Scenario: Enabled consumer is materialized
- **WHEN** `EnrichmentOptions.Consensus.Enabled` is `true`
- **THEN** the consensus consumer stream is materialized and connected to the BroadcastHub

### Requirement: Consumer streams use delta publishing
Each consumer stream SHALL maintain a `Dictionary<string, string>` mapping topic → last published payload. A computed `MqttMessage` SHALL only be emitted to the SinkRef when the serialized payload differs from the last published value for that topic.

#### Scenario: First publish always emits
- **WHEN** the consensus consumer computes a result for `njord/lucerne/consensus/h3` for the first time
- **THEN** the MqttMessage is emitted to the SinkRef

#### Scenario: Identical payload is suppressed
- **WHEN** a recomputed consensus yields the same JSON payload as the last publish for that topic
- **THEN** no MqttMessage is emitted

#### Scenario: Changed payload emits
- **WHEN** a recomputed consensus yields a different JSON payload than the last publish
- **THEN** the new MqttMessage is emitted to the SinkRef

### Requirement: Stream supervision resumes on consumer errors
Each consumer stream SHALL use a supervision strategy that resumes on exceptions. A failure in one consumer's computation SHALL NOT terminate other consumer streams or the snapshot BroadcastHub.

#### Scenario: Consumer exception does not kill the pipeline
- **WHEN** the consensus consumer throws during one computation
- **THEN** the stream resumes and processes the next snapshot; other consumers are unaffected
