## ADDED Requirements

### Requirement: Each stream-owning actor materializes its graph once
`OpenMeteoGateway` SHALL materialize its fetch graph and its forecast hub once, in `PreStart`, without waiting for any other actor. `ForecastPipeline` SHALL materialize its analysis graph once per received forecast stream. Both SHALL use `Context.Materializer()`, so a graph lives exactly as long as the actor incarnation that owns it. No stage SHALL materialize a sub-stream per element; the only additional materializations SHALL be the SourceRefs vended to consumers. No `IHostedService` SHALL own a stream.

#### Scenario: No per-cycle materialization
- **WHEN** 10 fetch requests are processed
- **THEN** no stream materialization happens beyond the gateway's initial graphs and the SourceRefs vended on request

#### Scenario: Actor stop terminates graph
- **WHEN** the gateway or `ForecastPipeline` stops
- **THEN** its graphs terminate with the actor-scoped materializer

#### Scenario: Gateway materializes without waiting for consumers
- **WHEN** `OpenMeteoGateway` starts
- **THEN** its fetch graph and forecast hub are materialized immediately, before any consumer requests a SourceRef

### Requirement: Stream lifecycle is bound to the owning actor
Stream graphs SHALL terminate through the lifecycle of the actor that owns them (`Context.Materializer()`); `StreamConsumerActor` subclasses SHALL additionally route their graph through a `SharedKillSwitch` that the base shuts down when a tracked dependency terminates. There SHALL be no `StopStreams`/`StreamsStopped` messages and no CoordinatedShutdown stream task.

#### Scenario: Actor stop ends the stream
- **WHEN** an actor that owns a stream graph stops
- **THEN** the graph terminates with the actor-scoped materializer

#### Scenario: Dependency loss ends the consumer stream
- **WHEN** `ForecastPipeline` terminates while `MqttStatePublisher` consumes its output
- **THEN** `MqttStatePublisher` shuts down its kill switch before re-resolving the pipeline

### Requirement: Observability via metrics per fetch outcome
`OpenMeteoClient` SHALL record every fetch in the `njord_openmeteo_fetch_total` counter tagged with `location`, `model` and `outcome` (`success` or the lower-case failure reason), and SHALL record the duration of successful fetches in the `njord_openmeteo_fetch_duration_seconds` histogram tagged with `location` and `model`.

#### Scenario: Each fetch counts its outcome
- **WHEN** a fetch for (lucerne, icon_d2) succeeds in 1.2 s
- **THEN** `njord_openmeteo_fetch_total{location="lucerne",model="icon_d2",outcome="success"}` increases by 1 and 1.2 s is recorded in the duration histogram

#### Scenario: Failures are counted by reason
- **WHEN** a fetch is answered with HTTP 429
- **THEN** the counter increases with `outcome="ratelimited"` and no duration is recorded

### Requirement: Ref requesters handle typed handshake failures
Every actor or service that requests a SourceRef SHALL handle the matching project-owned failure record from `Njord.Core/Actors/StreamWiringMessages.cs`, each carrying the originating `RequestId` and the `Exception` cause: `ForecastPipeline` handles `RequestForecastStreamFailed` by logging a warning and re-resolving the gateway after jittered backoff; `StreamConsumerActor` subclasses handle `RequestPipelineOutputFailed` (ignoring a reply whose `RequestId` is not their current one) by logging a warning and scheduling a re-resolve through the base backoff; the gRPC services treat `RequestReadModelStreamFailed` and `RequestConfigStreamFailed` as `Unavailable`. The system SHALL NOT use Akka's `Status.Failure`/`Status.Success` as replies or stream-termination messages.

#### Scenario: Stream consumer retries after a failed ref request
- **WHEN** a `StreamConsumerActor` subclass receives `RequestPipelineOutputFailed` for its current request id
- **THEN** it logs a warning and re-requests the ref after the next backoff delay

#### Scenario: Pipeline retries after a failed forecast stream request
- **WHEN** `ForecastPipeline` receives `RequestForecastStreamFailed`
- **THEN** it logs a warning and re-resolves the gateway after the next backoff delay

#### Scenario: gRPC stream call fails with Unavailable
- **WHEN** `StreamForecasts` or `StreamEnrichments` asks `ForecastReadModel` for a stream and does not get `RequestReadModelStreamCompleted`
- **THEN** the call ends with `RpcException` status `Unavailable`

## MODIFIED Requirements

### Requirement: Fetch outcomes publish directly without aggregation
Each successful fetch SHALL deliver its `Forecast` to the gateway's BroadcastHub individually, without waiting for other models of the same cycle. Downstream consumers (not the gateway) SHALL turn forecasts into analysis results and MQTT messages. Failed outcomes SHALL NOT reach the BroadcastHub; they are reported to the requester as `FetchForecastFailed`.

#### Scenario: Successful fetch reaches BroadcastHub
- **WHEN** a fetch for (lucerne, icon_d2) succeeds
- **THEN** its `Forecast` is offered to the gateway's BroadcastHub

#### Scenario: Failed fetch does not reach BroadcastHub
- **WHEN** a fetch fails
- **THEN** no element is emitted into the BroadcastHub for that target

### Requirement: Stream supervision uses a logging decider
The gateway fetch graph, the `ForecastPipeline` analysis stage and the `MqttStatePublisher` publish graph SHALL use the shared `StreamSupervision.LoggingDecider` instead of a blanket `_ => Directive.Resume`. Each call site SHALL pass its own `ILoggingAdapter` (or `ILogger`) so that log entries identify the originating actor or component. The decider SHALL resume on `AskTimeoutException`, `TaskCanceledException`, `OperationCanceledException`, `TimeoutException` and `HttpRequestException`, and stop on every other exception.

#### Scenario: Stream exception is logged with actor context
- **WHEN** a stream exception occurs in one of these graphs
- **THEN** the exception is logged at Warning level through the owning actor's logger

#### Scenario: Transient errors do not terminate the stream
- **WHEN** an AskTimeoutException occurs in a stream graph
- **THEN** the stream continues processing subsequent elements

#### Scenario: Unexpected errors stop the stream stage
- **WHEN** a NullReferenceException occurs in a stream graph
- **THEN** the stream stage stops, triggering actor supervision

## REMOVED Requirements

### Requirement: The pipeline is a single materialized graph
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Each stream-owning actor materializes its graph once" in this capability.

### Requirement: MergeHub accepts targets from producers via SinkRef
**Reason**: There is no MergeHub and no SinkRef; `PollScheduler` sends `FetchForecast` to the gateway, which offers targets into its own internal `Source.Queue`.
**Migration**: See `poll-pipeline`.

### Requirement: BroadcastHub fans out fetch outcomes to multiple consumers
**Reason**: There is no `BroadcastHub<FetchOutcome.Success>` and no egress or feedback consumer of it. The gateway broadcasts `Forecast` to `ForecastPipeline`, and fetch feedback goes straight back to the requester.
**Migration**: See `poll-pipeline` ("Successful forecasts fan out via BroadcastHub and every fetch reports back") and `producer-broadcast-pattern`.

### Requirement: Hash and schedule feedback is a BroadcastHub consumer
**Reason**: There is no feedback consumer and no `HashResult`/`Ack` Ask flow. The gateway computes `ForecastDataHash` itself and replies `FetchForecastCompleted` to the requesting `PollScheduler`.
**Migration**: See `poll-pipeline` and `poll-scheduler` ("Fetch feedback from the gateway updates the schedule").

### Requirement: Pipeline stages are independently testable flows
**Reason**: There is no `FetchStage` factory; the fetch call is inline in the gateway graph and the only separate stage, `GatewayThrottleStage`, is internal. The requirement describes a structure the code does not have.
**Migration**: None.

### Requirement: Clean shutdown via actor lifecycle
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Stream lifecycle is bound to the owning actor" in this capability.

### Requirement: Observability via structured logging per outcome
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Observability via metrics per fetch outcome" in this capability.

### Requirement: The EnrichmentActor is a BroadcastHub consumer via SourceRef
**Reason**: `EnrichmentActor` and `FetchOutcome` no longer exist; analysis runs inside `ForecastPipeline`.
**Migration**: See `pipeline-composition`.

### Requirement: Ref requesters handle typed request failures with backoff
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Ref requesters handle typed handshake failures" in this capability.
