## MODIFIED Requirements

### Requirement: ForecastPipeline receives forecasts via SourceRef from the gateway
The `ForecastPipeline` (renamed from `PipelineActor`) SHALL request a `SourceRef<Forecast>` from the `OpenMeteoGateway` via `Context.GetActor<IForecastGateway>()` and `Ask<ForecastStreamReady>(new RequestForecastStream(...))`. The pipeline stream graph SHALL be: `SourceRef.Source → ZipWith(sensorSource) → StatefulSelectMany(analysis) → BroadcastHub<PipelineEvent>`. The actor SHALL NOT own a `Source.Queue`, `BudgetThrottleStage`, `SelectAsyncUnordered(fetch)`, or any fetch-related logic. The actor SHALL NOT receive `ScheduledPoll` or `WeightedTarget` messages.

#### Scenario: Pipeline requests SourceRef from gateway
- **WHEN** the ForecastPipeline starts
- **THEN** it SHALL resolve the gateway via `Context.GetActor<IForecastGateway>()` and Ask for a `SourceRef<Forecast>`

#### Scenario: Pipeline stream starts from SourceRef
- **WHEN** the `ForecastStreamReady` response arrives
- **THEN** the pipeline SHALL materialize `sourceRef.Source → ZipWith(sensors) → StatefulSelectMany(analysis) → BroadcastHub<PipelineEvent>`

#### Scenario: No fetch logic in pipeline
- **WHEN** the ForecastPipeline is inspected
- **THEN** it SHALL have no dependency on `IOpenMeteoClient`, `IBudgetGate`, or `BudgetThrottleStage`

### Requirement: ForecastPipeline distributes PipelineEvents via BroadcastHub
The `ForecastPipeline` SHALL materialize a `BroadcastHub.Sink<PipelineEvent>` with `bufferSize: 2` to distribute analysis results to consumers. On `RequestPipelineOutput(RequestId)`, the actor SHALL materialize a `SourceRef<PipelineEvent>` from the BroadcastHub output and reply with `PipelineOutputResponse(RequestId, SourceRef)`.

#### Scenario: Consumer requests and receives a SourceRef
- **WHEN** a consumer sends `RequestPipelineOutput(requestId)`
- **THEN** ForecastPipeline responds with `PipelineOutputResponse(requestId, sourceRef)`

#### Scenario: SourceRef materialization failure
- **WHEN** SourceRef materialization fails
- **THEN** ForecastPipeline sends `PipelineOutputFailed(requestId, exception)` to the requester

### Requirement: ForecastPipeline materializes analysis via actor-scoped materializer
The `ForecastPipeline` SHALL materialize the stream graph using `Context.Materializer()`. The stream lifecycle SHALL be bound to the actor — when the actor stops, the graph terminates via the materializer lifecycle. No `KillSwitch`, `StopStreams`, or `StreamsStopped` messages SHALL exist.

#### Scenario: Actor stop terminates the pipeline
- **WHEN** the ForecastPipeline is stopped
- **THEN** the stream graph terminates via the actor-scoped materializer lifecycle

#### Scenario: No StopStreams message handling
- **WHEN** the ForecastPipeline is inspected
- **THEN** it SHALL not handle `StopStreams` or reply `StreamsStopped`

## REMOVED Requirements

### Requirement: The pipeline graph is materialized by an actor
**Reason**: Replaced by the simplified ForecastPipeline that receives SourceRef from gateway. The `KillSwitch`, `StopStreams`/`StreamsStopped`, and graceful shutdown pattern are removed — the actor-scoped materializer handles lifecycle.
**Migration**: Remove `StopStreams`/`StreamsStopped` messages, `KillSwitch` usage, `AddStreamShutdownTask` in `PipelineSetupContainer`.

### Requirement: Targets arrive via MergeHub from producers connected by SinkRef
**Reason**: Replaced by SourceRef-based input from the gateway. The pipeline no longer receives targets or owns a fetch stream.
**Migration**: Pipeline receives `SourceRef<Forecast>` from gateway instead of `WeightedTarget` from scheduler.

### Requirement: Outbound requests respect the per-minute budget
**Reason**: Budget throttling is now owned by the OpenMeteoGateway. The pipeline no longer contains fetch or throttle stages.
**Migration**: Budget throttling lives in `Njord.OpenMeteo` (gateway-internal).

### Requirement: PipelineActor receives ScheduledPoll via Tell and broadcasts FetchOutcome
**Reason**: PipelineActor no longer receives ScheduledPoll or owns a fetch stream. The gateway handles fetching; the pipeline handles analysis only.
**Migration**: Remove `ScheduledPoll` handling, `WeightedTarget` creation, `Source.Queue`, and `BudgetThrottle` from pipeline.

### Requirement: The pipeline actor materializes the feedback consumer locally
**Reason**: Scheduler feedback is now handled by the gateway's `WireTap` stage. The pipeline no longer computes hashes or sends feedback to the scheduler.
**Migration**: Hash computation and scheduler feedback move to the gateway.
