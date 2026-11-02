## ADDED Requirements

### Requirement: GrpcConsumer reconnects after pipeline stream completion

The GrpcConsumer SHALL automatically re-attach to the ForecastPipeline when its SourceRef stream completes. The reconnect SHALL use synchronous actor resolution (`GetActor<IForecastPipeline>`) and request a new SourceRef via `RequestPipelineOutput`. During the reconnect window, the consumer's BroadcastHub and cached state SHALL remain intact -- only the upstream feed is re-established.

#### Scenario: Pipeline restarts and stream completes
- **WHEN** the GrpcConsumer receives a `StreamCompleted` message (the SourceRef stream terminated)
- **THEN** the consumer resolves the pipeline actor synchronously and sends `RequestPipelineOutput`
- **THEN** upon receiving `RequestPipelineOutputCompleted`, the consumer materializes a new stream from the SourceRef to its internal hub
- **THEN** new pipeline events flow to gRPC clients again

#### Scenario: Pipeline is temporarily unavailable during reconnect
- **WHEN** `GetActor<IForecastPipeline>` throws because the actor is not yet registered
- **THEN** the exception propagates to the actor's supervisor (BackoffOnFailure) which restarts the GrpcConsumer, triggering a fresh PreStart with full reconnect

### Requirement: GrpcConsumer uses synchronous pipeline resolution

The GrpcConsumer SHALL resolve the pipeline actor via synchronous `GetActor<IForecastPipeline>` in PreStart, consistent with all other actors in the codebase (PollManager, Gateway). The async `GetActorAsync` + `PipeTo` pattern SHALL be removed.

#### Scenario: Normal startup
- **WHEN** the GrpcConsumer starts (PreStart)
- **THEN** it resolves the pipeline synchronously and immediately sends `RequestPipelineOutput`
- **THEN** the `PipelineResolved` / `PipelineResolveFailed` messages are no longer needed
