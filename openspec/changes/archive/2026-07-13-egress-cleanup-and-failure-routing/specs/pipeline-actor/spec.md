## MODIFIED Requirements

### Requirement: The pipeline actor vends a SinkRef for producers and a SourceRef for consumers
The PipelineActor SHALL materialize a `MergeHub` as the pipeline entry point
and a `BroadcastHub` as the pipeline output. The BroadcastHub SHALL carry
`FetchOutcome` (not just `FetchOutcome.Success`) so that both successes and
failures are available to all consumers. On request, it SHALL vend a
`SinkRef<WeightedTarget>` (connected to the MergeHub) for producers, and a
`SourceRef<FetchOutcome>` (connected to the BroadcastHub) for consumers.
No raw `ISourceQueueWithComplete` SHALL be exposed.

#### Scenario: SchedulerActor requests and receives a SinkRef
- **WHEN** the SchedulerActor sends a `RequestPipelineSink` message after materialization
- **THEN** the PipelineActor responds with a `PipelineSinkResponse` containing a `SinkRef<WeightedTarget>`

#### Scenario: EgressActor requests and receives a SourceRef
- **WHEN** the EgressActor sends a `RequestPipelineSource` message after materialization
- **THEN** the PipelineActor responds with a `PipelineSourceResponse` containing a `SourceRef<FetchOutcome>`

#### Scenario: No raw queue handle is exposed
- **WHEN** any actor requests pipeline access
- **THEN** only typed StreamRef endpoints (SinkRef or SourceRef) are returned, never an `ISourceQueueWithComplete`

### Requirement: The pipeline actor materializes the feedback consumer locally
The PipelineActor SHALL materialize a BroadcastHub consumer that filters for
`FetchOutcome.Success`, computes a `ForecastDataHash`, and sends the `HashResult`
to the SchedulerActor via the built-in `Ask<Ack>` flow.

#### Scenario: Feedback consumer computes hash and asks scheduler
- **WHEN** a `FetchOutcome.Success` is broadcast
- **THEN** the feedback consumer computes a hash, sends `HashResult` to the SchedulerActor via Ask, and waits for `Ack`

#### Scenario: Feedback consumer ignores failures
- **WHEN** a `FetchOutcome.Failure` is broadcast
- **THEN** the feedback consumer skips it (no hash computation, no Ask)

#### Scenario: Feedback consumer lifecycle is bound to PipelineActor
- **WHEN** the PipelineActor stops
- **THEN** the feedback consumer graph also terminates
