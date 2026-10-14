# stream-composition Specification

## Purpose

Structural requirements for the Akka.Streams graphs: each stream-owning actor (`OpenMeteoGateway`, `ForecastPipeline`, consumers) materializes its graph once and binds its lifecycle to the actor, the gateway throttles to the global API budget and publishes fetch outcomes directly, streams use a logging supervision decider, metrics are recorded per fetch outcome, and ref requesters handle typed handshake failures.
## Requirements
### Requirement: Weighted throttle enforces the global API budget
The pipeline SHALL throttle fetch requests using a cost-weighted throttle set to 80% of the Open-Meteo per-minute rate limit. Each element's cost SHALL be its pre-calculated API weight. The throttle SHALL use shaping mode (delay, not drop).

#### Scenario: Weight-1 requests within budget pass immediately
- **WHEN** 8 weight-1 targets enter the throttle with budget ceiling 480/min
- **THEN** all 8 pass through without delay

#### Scenario: Burst beyond budget is shaped
- **WHEN** 500 weight-1 targets enter the throttle with budget ceiling 480/min
- **THEN** the first 480 pass within the first minute; the remaining 20 are delayed into the next minute window

### Requirement: Fetch outcomes publish directly without aggregation
Each successful fetch SHALL deliver its `Forecast` to the gateway's BroadcastHub individually, without waiting for other models of the same cycle. Downstream consumers (not the gateway) SHALL turn forecasts into analysis results and MQTT messages. Failed outcomes SHALL NOT reach the BroadcastHub; they are reported to the requester as `FetchForecastFailed`.

#### Scenario: Successful fetch reaches BroadcastHub
- **WHEN** a fetch for (lucerne, icon_d2) succeeds
- **THEN** its `Forecast` is offered to the gateway's BroadcastHub

#### Scenario: Failed fetch does not reach BroadcastHub
- **WHEN** a fetch fails
- **THEN** no element is emitted into the BroadcastHub for that target

### Requirement: Stream supervision uses a logging decider
The gateway fetch graph, the `ForecastPipeline` analysis stage and the `MqttStatePublisher` publish graph SHALL use the shared `StreamSupervision.LoggingDecider` instead of a blanket `_ => Directive.Resume`. Each call site SHALL pass its own `ILoggingAdapter` (or `ILogger`) so that log entries identify the originating actor or component. The decider SHALL resume on `AskTimeoutException`, `TaskCanceledException`, `OperationCanceledException`, `TimeoutException` and `HttpRequestException`, and stop on every other exception. A stopped stage ends its graph; the owning actor SHALL notice the ended graph and rebuild it (the gateway re-materializes its fetch stream, `ForecastPipeline` requests a new forecast stream, `StreamConsumerActor` subclasses rewire). The owning actor itself SHALL NOT be restarted by a stopped stage.

#### Scenario: Stream exception is logged with actor context
- **WHEN** a stream exception occurs in one of these graphs
- **THEN** the exception is logged at Warning level through the owning actor's logger

#### Scenario: Transient errors do not terminate the stream
- **WHEN** an AskTimeoutException occurs in a stream graph
- **THEN** the stream continues processing subsequent elements

#### Scenario: Unexpected errors stop the stream stage
- **WHEN** a NullReferenceException occurs in a stream graph
- **THEN** the stream stage stops, the graph ends, and the owning actor rebuilds the graph without being restarted

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
Stream graphs SHALL terminate through the lifecycle of the actor that owns them (`Context.Materializer()`); `StreamConsumerActor` subclasses SHALL additionally route their graph through a `SharedKillSwitch` that the base shuts down when a tracked dependency terminates or before it rewires a graph that ended. A graph that ends while its owning actor keeps running SHALL be rebuilt by that actor; a graph that ends because the actor stops or replaced it SHALL NOT be rebuilt. There SHALL be no `StopStreams`/`StreamsStopped` messages and no CoordinatedShutdown stream task.

#### Scenario: Actor stop ends the stream
- **WHEN** an actor that owns a stream graph stops
- **THEN** the graph terminates with the actor-scoped materializer

#### Scenario: Dependency loss ends the consumer stream
- **WHEN** `ForecastPipeline` terminates while `MqttStatePublisher` consumes its output
- **THEN** `MqttStatePublisher` shuts down its kill switch before re-resolving the pipeline

#### Scenario: An ended graph of a running actor is rebuilt
- **WHEN** the pipeline output hub that `MqttStatePublisher` consumes completes while both actors keep running
- **THEN** `MqttStatePublisher` requests a new SourceRef and publishes the events of the new subscription

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

