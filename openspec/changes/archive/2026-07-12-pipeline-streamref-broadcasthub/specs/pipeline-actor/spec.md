## MODIFIED Requirements

### Requirement: The pipeline graph is materialized by an actor
A `PipelineActor` SHALL materialize the full pipeline graph using `Context.Materializer()`. The pipeline graph SHALL be materialized independently — the actor SHALL NOT wait for the EgressActor before materializing. The stream lifecycle SHALL be bound to the actor — when the actor stops, the graph terminates. No `IHostedService` or manual `KillSwitch` SHALL be used for pipeline lifecycle.

#### Scenario: Actor stop terminates the pipeline
- **WHEN** the PipelineActor is stopped
- **THEN** the pipeline graph and all BroadcastHub consumers complete and no further commands are processed

#### Scenario: Actor restart rematerializes the pipeline
- **WHEN** the PipelineActor restarts after a failure
- **THEN** a new pipeline graph is materialized, new SinkRef and SourceRef endpoints are created, and consumers must re-request them

#### Scenario: Pipeline materializes without egress dependency
- **WHEN** the PipelineActor starts
- **THEN** the pipeline graph is materialized immediately without requesting a SinkRef from the EgressActor

### Requirement: The pipeline actor vends a SinkRef for producers and a SourceRef for consumers
The PipelineActor SHALL materialize a `MergeHub` as the pipeline entry point and a `BroadcastHub` as the pipeline output. On request, it SHALL vend a `SinkRef<WeightedTarget>` (connected to the MergeHub) for producers, and a `SourceRef<FetchOutcome.Success>` (connected to the BroadcastHub) for consumers. No raw `ISourceQueueWithComplete` SHALL be exposed.

#### Scenario: SchedulerActor requests and receives a SinkRef
- **WHEN** the SchedulerActor sends a `RequestPipelineSink` message after materialization
- **THEN** the PipelineActor responds with a `PipelineSinkResponse` containing a `SinkRef<WeightedTarget>`

#### Scenario: EgressActor requests and receives a SourceRef
- **WHEN** the EgressActor sends a `RequestPipelineSource` message after materialization
- **THEN** the PipelineActor responds with a `PipelineSourceResponse` containing a `SourceRef<FetchOutcome.Success>`

#### Scenario: No raw queue handle is exposed
- **WHEN** any actor requests pipeline access
- **THEN** only typed StreamRef endpoints (SinkRef or SourceRef) are returned, never an `ISourceQueueWithComplete`

### Requirement: The pipeline actor materializes the feedback consumer locally
The PipelineActor SHALL materialize a BroadcastHub consumer that computes a `ForecastDataHash` for each `FetchOutcome.Success` and sends the `HashResult` to the SchedulerActor via the built-in `Ask<Ack>` flow. This consumer SHALL be materialized as part of the PipelineActor's setup, not delegated to the SchedulerActor.

#### Scenario: Feedback consumer computes hash and asks scheduler
- **WHEN** a `FetchOutcome.Success` is broadcast
- **THEN** the feedback consumer computes a hash, sends `HashResult` to the SchedulerActor via Ask, and waits for `Ack`

#### Scenario: Feedback consumer lifecycle is bound to PipelineActor
- **WHEN** the PipelineActor stops
- **THEN** the feedback consumer graph also terminates

### Requirement: The pipeline actor watches the egress actor for lifecycle coordination
The PipelineActor SHALL watch the EgressActor. If the EgressActor terminates (`Terminated` message), no pipeline rematerialization is needed — the BroadcastHub continues operating. The EgressActor is responsible for re-requesting a SourceRef on restart.

#### Scenario: Egress restart does not affect pipeline
- **WHEN** the EgressActor restarts and the PipelineActor receives `Terminated`
- **THEN** the pipeline graph continues running; the EgressActor re-requests a SourceRef when ready

## REMOVED Requirements

### Requirement: The pipeline actor obtains the egress SinkRef before materializing
**Reason:** The pipeline no longer pushes to egress via SinkRef. Instead, egress pulls from the pipeline via SourceRef from the BroadcastHub. The pipeline materializes independently.
**Migration:** Remove `RequestEgressSink`/`EgressSinkResponse` handshake. PipelineActor no longer stashes while waiting for egress. EgressActor sends `RequestPipelineSource` instead.

### Requirement: The pipeline maps FetchOutcome to MqttMessage at its terminal stage
**Reason:** The FetchOutcome-to-MqttMessage mapping moves to the EgressActor's consumer graph. The pipeline's terminal stage is now the BroadcastHub, which broadcasts raw `FetchOutcome.Success` elements.
**Migration:** `StatePayloadBuilder` usage and delta-publish logic move to the EgressActor's SourceRef consumer.
