## MODIFIED Requirements

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
