# pipeline-actor Specification

## Purpose

Defines the PipelineActor that owns the pipeline stream graph lifecycle: actor-bound materialization with independent startup (no egress dependency), SinkRef vending for producers (SchedulerActor) via MergeHub, SourceRef vending for consumers (EgressActor) via BroadcastHub, local feedback consumer for hash computation, and egress watch for lifecycle coordination.

## Requirements

### Requirement: The pipeline graph is materialized by an actor
A `PipelineActor` SHALL materialize the full pipeline graph using `Context.Materializer()`. The pipeline graph SHALL be materialized independently -- the actor SHALL NOT wait for the EgressActor before materializing. The stream lifecycle SHALL be bound to the actor -- when the actor stops, the graph terminates. No `IHostedService` or manual `KillSwitch` SHALL be used for pipeline lifecycle. The fetch logic (calling `IOpenMeteoClient.FetchAsync`) SHALL be inlined in the pipeline graph as a `SelectAsyncUnordered` operator — no separate `FetchStage` class.

#### Scenario: Actor stop terminates the pipeline
- **WHEN** the PipelineActor is stopped
- **THEN** the pipeline graph and all BroadcastHub consumers complete and no further commands are processed

#### Scenario: Actor restart rematerializes the pipeline
- **WHEN** the PipelineActor restarts after a failure
- **THEN** a new pipeline graph is materialized, new SinkRef and SourceRef endpoints are created, and consumers must re-request them

#### Scenario: Pipeline materializes without egress dependency
- **WHEN** the PipelineActor starts
- **THEN** the pipeline graph is materialized immediately without requesting a SinkRef from the EgressActor

#### Scenario: Fetch logic is inlined in the pipeline graph
- **WHEN** the PipelineActor materializes the graph
- **THEN** the fetch call to `IOpenMeteoClient.FetchAsync` is a direct `SelectAsyncUnordered` in the graph, not a separate `FetchStage.Create()` flow

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
The PipelineActor SHALL watch the EgressActor. If the EgressActor terminates (`Terminated` message), no pipeline rematerialization is needed -- the BroadcastHub continues operating. The EgressActor is responsible for re-requesting a SourceRef on restart.

#### Scenario: Egress restart does not affect pipeline
- **WHEN** the EgressActor restarts and the PipelineActor receives `Terminated`
- **THEN** the pipeline graph continues running; the EgressActor re-requests a SourceRef when ready
