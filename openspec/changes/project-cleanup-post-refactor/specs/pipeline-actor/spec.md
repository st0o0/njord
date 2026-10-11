## MODIFIED Requirements

### Requirement: The pipeline graph is materialized by an actor
A `PipelineActor` SHALL materialize the pipeline graph using `Context.Materializer()`. The pipeline graph SHALL be materialized independently — the actor SHALL NOT wait for any downstream actor before materializing. The stream lifecycle SHALL be bound to the actor — when the actor stops, the graph terminates. No `IHostedService` SHALL be used for pipeline lifecycle. The actor SHALL own a `UniqueKillSwitch` that is used only for graceful shutdown. On `StopStreams` the actor SHALL shut the switch down, complete the queue, complete the sensor ref, and reply `StreamsStopped`. The actor SHALL NOT implement `IWithStash`. The fetch logic (calling `IOpenMeteoClient.FetchAsync`) SHALL be inlined in the pipeline graph as a `SelectAsyncUnordered` operator.

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

## REMOVED Requirements

### Requirement: PipelineEvent.ModelCapability variant
**Reason**: Never emitted by production code; leftover from a pre-refactor design.
**Migration**: Remove the `ModelCapability` variant from `PipelineEvent`. Update any test fixtures that construct it.
