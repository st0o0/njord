## MODIFIED Requirements

### Requirement: The pipeline actor vends a SinkRef for producers and a SourceRef for consumers
The PipelineActor SHALL materialize a `MergeHub` as the pipeline entry point and a `BroadcastHub` as the pipeline output. The BroadcastHub SHALL carry `FetchOutcome` (not just `FetchOutcome.Success`) so that both successes and failures are available to all consumers. On request, it SHALL vend a `SinkRef<WeightedTarget>` (connected to the MergeHub) for producers, and a `SourceRef<FetchOutcome>` (connected to the BroadcastHub) for consumers. No raw `ISourceQueueWithComplete` SHALL be exposed.

When SinkRef or SourceRef materialization fails, the PipelineActor SHALL send a project-owned failure message (`PipelineSinkFailed(Exception Cause)` / `PipelineSourceFailed(Exception Cause)`) to the requesting actor. The actor SHALL NOT send `Status.Failure` and SHALL NOT return `null` or `null!` from a PipeTo failure handler.

#### Scenario: SchedulerActor requests and receives a SinkRef
- **WHEN** SchedulerActor sends RequestPipelineSink
- **THEN** PipelineActor responds with PipelineSinkResponse containing a valid SinkRef

#### Scenario: EgressActor requests and receives a SourceRef
- **WHEN** a consumer sends RequestPipelineSource
- **THEN** PipelineActor responds with PipelineSourceResponse containing a valid SourceRef

#### Scenario: No raw queue handle is exposed
- **WHEN** any actor requests pipeline access
- **THEN** only SinkRef/SourceRef handles are provided

#### Scenario: SinkRef materialization failure sends PipelineSinkFailed
- **WHEN** SinkRef materialization fails with an exception
- **THEN** PipelineActor sends PipelineSinkFailed carrying the exception to the requesting actor
- **THEN** the failure is logged at Error level

#### Scenario: SourceRef materialization failure sends PipelineSourceFailed
- **WHEN** SourceRef materialization fails with an exception
- **THEN** PipelineActor sends PipelineSourceFailed carrying the exception to the requesting actor
- **THEN** the failure is logged at Error level
