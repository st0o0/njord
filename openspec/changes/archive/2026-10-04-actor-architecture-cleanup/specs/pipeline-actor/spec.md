## MODIFIED Requirements

### Requirement: The pipeline graph is materialized by an actor
A `PipelineActor` SHALL materialize the pipeline graph using `Context.Materializer()`. The pipeline graph SHALL be materialized independently — the actor SHALL NOT wait for any downstream actor before materializing. The stream lifecycle SHALL be bound to the actor — when the actor stops, the graph terminates. No `IHostedService` SHALL be used for pipeline lifecycle. The actor SHALL own a `UniqueKillSwitch` that is used only for graceful shutdown. On `StopStreams` the actor SHALL shut the switch down, wait for the graph completion, and reply `StreamsStopped` (or `StreamsStopFailed`). The fetch logic (calling `IOpenMeteoClient.FetchAsync`) SHALL be inlined in the pipeline graph as a `SelectAsyncUnordered` operator.

#### Scenario: Actor stop terminates the pipeline
- **WHEN** the PipelineActor is stopped
- **THEN** the pipeline graph and all BroadcastHub consumers complete and no further commands are processed

#### Scenario: Graceful stop completes the graph without abrupt termination
- **WHEN** the PipelineActor receives `StopStreams` while Ready
- **THEN** the graph completes normally, `StreamsStopped` is sent to the requester, and no `AbruptTerminationException` is logged

#### Scenario: Actor restart rematerializes the pipeline
- **WHEN** the PipelineActor restarts after a failure
- **THEN** a new pipeline graph is materialized, new SourceRef endpoints are created, and consumers must re-request them

#### Scenario: Pipeline materializes without downstream dependency
- **WHEN** the PipelineActor starts
- **THEN** the pipeline graph is materialized immediately without waiting for any downstream actor

#### Scenario: Fetch logic is inlined in the pipeline graph
- **WHEN** the PipelineActor materializes the graph
- **THEN** the fetch call to `IOpenMeteoClient.FetchAsync` is a direct `SelectAsyncUnordered` in the graph, not a separate `FetchStage.Create()` flow

### Requirement: PipelineActor receives ScheduledPoll via Tell and broadcasts FetchOutcome
The PipelineActor SHALL accept `ScheduledPoll` messages via Tell from the PollSchedulerActor. On receipt it SHALL create a `WeightedTarget` and enqueue it into an internal `Source.Queue`. The queue SHALL feed into BudgetThrottle → FetchAsync → BroadcastHub. The actor SHALL NOT materialize a MergeHub — there is exactly one producer (the PollSchedulerActor via Tell). The actor SHALL accept `RequestPipelineSource` and return `SourceRef<FetchOutcome>` from its BroadcastHub for consumers. No `SinkRef` SHALL be vended. No raw `ISourceQueueWithComplete` SHALL be exposed outside the actor.

#### Scenario: ScheduledPoll enqueued into internal queue
- **WHEN** PipelineActor receives `ScheduledPoll("lucerne", "icon_d2")`
- **THEN** it creates a `WeightedTarget` and offers it into the internal queue

#### Scenario: Consumer requests and receives a SourceRef
- **WHEN** a consumer sends `RequestPipelineSource`
- **THEN** PipelineActor responds with `PipelineSourceResponse` containing a valid `SourceRef<FetchOutcome>`

#### Scenario: No MergeHub materialized
- **WHEN** the PipelineActor materializes its graph
- **THEN** no MergeHub is present — the source is a single internal queue

#### Scenario: No SinkRef vended
- **WHEN** any actor sends a message requesting a SinkRef
- **THEN** PipelineActor does not handle it — no SinkRef API exists

#### Scenario: SourceRef materialization failure sends PipelineSourceFailed
- **WHEN** SourceRef materialization fails with an exception
- **THEN** PipelineActor sends `PipelineSourceFailed` carrying the exception to the requesting actor

### Requirement: PipelineActor materializes a BroadcastHub for FetchOutcome distribution
The `PipelineActor` SHALL materialize a `BroadcastHub.Sink<FetchOutcome>` with a buffer size of 16 to distribute fetch results to all consumers (ModelStateActor, EnrichmentActor, PollSchedulerActor feedback, GrpcSnapshotConsumer).

#### Scenario: BroadcastHub buffer size is 16
- **WHEN** the PipelineActor materializes its stream graph
- **THEN** the BroadcastHub SHALL use a buffer size of 16

### Requirement: The pipeline actor materializes the feedback consumer locally
The PipelineActor SHALL materialize a BroadcastHub consumer that filters for `FetchOutcome.Success`, computes a `ForecastDataHash`, and sends the `HashResult` to the SchedulerActor via the built-in `Ask<Ack>` flow.

The feedback consumer's stream supervision SHALL use the shared `StreamSupervision.LoggingDecider` with the PipelineActor's ILogger. The fetch stage's stream supervision SHALL also use the shared decider.

#### Scenario: Feedback consumer computes hash and asks scheduler
- **WHEN** a FetchOutcome.Success arrives
- **THEN** a ForecastDataHash is computed and sent to SchedulerActor via Ask

#### Scenario: Feedback consumer ignores failures
- **WHEN** a FetchOutcome.Failure arrives
- **THEN** it is filtered out before hash computation

#### Scenario: Feedback consumer lifecycle is bound to PipelineActor
- **WHEN** PipelineActor stops
- **THEN** the feedback consumer stream completes

## REMOVED Requirements

### Requirement: The pipeline actor vends a SinkRef for producers and a SourceRef for consumers
**Reason**: The MergeHub and SinkRef API are removed. PipelineActor now receives `ScheduledPoll` via Tell from the PollSchedulerActor (single producer) and only vends SourceRefs from its BroadcastHub.
**Migration**: PollSchedulerActor sends `ScheduledPoll` messages via Tell to PipelineActor instead of requesting a SinkRef and materializing a Source.Queue connected to it.

### Requirement: The pipeline actor watches the egress actor for lifecycle coordination
**Reason**: The EgressActor hub is eliminated. There is no EgressActor to watch.
**Migration**: Consumers (ModelStateActor, EnrichmentActor) subscribe directly to PipelineActor's BroadcastHub via SourceRef. PipelineActor does not need to watch any downstream actor.
