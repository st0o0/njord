## MODIFIED Requirements

### Requirement: Weighted throttle enforces the global API budget
The gateway SHALL throttle fetch requests using a cost-weighted throttle set to 80% of the Open-Meteo per-minute rate limit. Each element's cost SHALL be its pre-calculated API weight. The throttle SHALL shape (delay) a request as long as it can still be admitted before its admission deadline and SHALL reject it with reason `Budget` otherwise; it SHALL never drop a request without answering it.

#### Scenario: Weight-1 requests within budget pass immediately
- **WHEN** 8 weight-1 targets enter the throttle with budget ceiling 480/min
- **THEN** all 8 pass through without delay

#### Scenario: Burst beyond budget is shaped
- **WHEN** more weight-1 targets enter the throttle than its burst allows
- **THEN** the targets that can be admitted within their admission deadline are delayed and then fetched, and the rest are answered `FetchForecastRejected` with reason `Budget`

### Requirement: Stream supervision uses a logging decider
The gateway fetch graph, the `ForecastPipeline` analysis stage and the `MqttStatePublisher` publish graph SHALL use the shared `StreamSupervision.LoggingDecider` instead of a blanket `_ => Directive.Resume`. Each call site SHALL pass its own `ILoggingAdapter` (or `ILogger`) so that log entries identify the originating actor or component. The decider SHALL resume on `AskTimeoutException`, `TaskCanceledException`, `OperationCanceledException`, `TimeoutException` and `HttpRequestException`, and stop on every other exception. A stopped stage ends its graph. `StreamConsumerActor` subclasses SHALL notice the ended graph and rewire without being restarted; `OpenMeteoGateway` and `ForecastPipeline` SHALL fail on an ended graph of their own and be restarted by their backoff-on-failure supervisors.

#### Scenario: Stream exception is logged with actor context
- **WHEN** a stream exception occurs in one of these graphs
- **THEN** the exception is logged at Warning level through the owning actor's logger

#### Scenario: Transient errors do not terminate the stream
- **WHEN** an AskTimeoutException occurs in a stream graph
- **THEN** the stream continues processing subsequent elements

#### Scenario: Unexpected errors stop the stream stage
- **WHEN** a NullReferenceException occurs in a stream graph
- **THEN** the stream stage stops and the graph ends; a stream consumer rewires without being restarted, while the gateway or the pipeline is restarted by its supervisor

### Requirement: Each stream-owning actor materializes its graph once
`OpenMeteoGateway` SHALL materialize its private fetch graph once, in `PreStart`, without waiting for any other actor, and a small forecast branch each time it attaches to the pipeline input. `ForecastPipeline` SHALL materialize its input hub, analysis graph and output hub once per actor incarnation, in `PreStart`. Both SHALL use `Context.Materializer()`, so a graph lives exactly as long as the actor incarnation that owns it. No stage SHALL materialize a sub-stream per element; the only additional materializations SHALL be the SinkRefs and SourceRefs vended to producers and consumers and the gateway's forecast branch. No `IHostedService` SHALL own a stream.

#### Scenario: No per-cycle materialization
- **WHEN** 10 fetch requests are processed
- **THEN** no stream materialization happens beyond the actors' initial graphs, the gateway's forecast branch and the stream refs vended on request

#### Scenario: Actor stop terminates graph
- **WHEN** the gateway or `ForecastPipeline` stops
- **THEN** its graphs terminate with the actor-scoped materializer

#### Scenario: Gateway materializes without waiting for consumers
- **WHEN** `OpenMeteoGateway` starts
- **THEN** its fetch graph is materialized immediately, before the pipeline answers its SinkRef request

### Requirement: Stream lifecycle is bound to the owning actor
Stream graphs SHALL terminate through the lifecycle of the actor that owns them (`Context.Materializer()`); `StreamConsumerActor` subclasses SHALL additionally route their graph through a `SharedKillSwitch` that the base shuts down when a tracked dependency terminates or before it rewires a graph that ended. A consumer graph that ends while its owning actor keeps running SHALL be rebuilt by that actor; the gateway's forecast branch that ends SHALL be re-attached by the gateway; the gateway's fetch graph and the pipeline's analysis graph that end SHALL restart their actor. A graph that ends because the actor stops or replaced it SHALL NOT be rebuilt. There SHALL be no `StopStreams`/`StreamsStopped` messages and no CoordinatedShutdown stream task.

#### Scenario: Actor stop ends the stream
- **WHEN** an actor that owns a stream graph stops
- **THEN** the graph terminates with the actor-scoped materializer

#### Scenario: Dependency loss ends the consumer stream
- **WHEN** `ForecastPipeline` terminates while `MqttStatePublisher` consumes its output
- **THEN** `MqttStatePublisher` shuts down its kill switch before re-resolving the pipeline

#### Scenario: An ended graph of a running actor is rebuilt
- **WHEN** the pipeline output hub that `MqttStatePublisher` consumes completes while both actors keep running
- **THEN** `MqttStatePublisher` requests a new SourceRef and publishes the events of the new subscription

## REMOVED Requirements

### Requirement: Fetch outcomes publish directly without aggregation
**Reason**: The gateway no longer has a BroadcastHub; forecasts flow into the pipeline's input hub.
**Migration**: See "Fetch outcomes flow to the pipeline input without aggregation".

### Requirement: Ref requesters handle typed handshake failures
**Reason**: `ForecastPipeline` no longer requests a forecast stream; the gateway now requests a SinkRef from the pipeline.
**Migration**: See "Stream-ref requesters handle typed handshake failures".

## ADDED Requirements

### Requirement: Fetch outcomes flow to the pipeline input without aggregation
Each successful fetch SHALL deliver its `Forecast` to the pipeline's input individually, without waiting for other models of the same cycle. The pipeline (not the gateway) SHALL turn forecasts into analysis results; consumers SHALL turn those into MQTT messages and API answers. Failed and rejected outcomes SHALL NOT reach the pipeline input; they are reported to the requester as `FetchForecastFailed` or `FetchForecastRejected`.

#### Scenario: Successful fetch reaches the pipeline input
- **WHEN** a fetch for (lucerne, icon_d2) succeeds
- **THEN** its `Forecast` is sent into the pipeline's SinkRef on its own

#### Scenario: Failed fetch does not reach the pipeline input
- **WHEN** a fetch fails
- **THEN** no element is sent into the pipeline's SinkRef for that target

### Requirement: Stream-ref requesters handle typed handshake failures
Every actor or service that requests a SinkRef or SourceRef SHALL handle the matching project-owned failure record from `Njord.Core/Actors/StreamWiringMessages.cs`, each carrying the originating `RequestId` and the `Exception` cause, and SHALL ignore a reply whose `RequestId` is not its current one: `OpenMeteoGateway` handles `RequestForecastSinkFailed` (and a missing reply) by logging a warning and requesting a new SinkRef after jittered backoff; `StreamConsumerActor` subclasses handle `RequestPipelineOutputFailed` by logging a warning and scheduling a re-resolve through the base backoff; the gRPC services treat `RequestReadModelStreamFailed` and `RequestConfigStreamFailed` as `Unavailable`. The system SHALL NOT use Akka's `Status.Failure`/`Status.Success` as replies or stream-termination messages.

#### Scenario: Stream consumer retries after a failed ref request
- **WHEN** a `StreamConsumerActor` subclass receives `RequestPipelineOutputFailed` for its current request id
- **THEN** it logs a warning and re-requests the ref after the next backoff delay

#### Scenario: Gateway retries after a failed sink request
- **WHEN** `OpenMeteoGateway` receives `RequestForecastSinkFailed` for its current request id
- **THEN** it logs a warning and requests a new SinkRef after the next backoff delay

#### Scenario: gRPC stream call fails with Unavailable
- **WHEN** `StreamForecasts` or `StreamEnrichments` asks `ForecastReadModel` for a stream and does not get `RequestReadModelStreamCompleted`
- **THEN** the call ends with `RpcException` status `Unavailable`
