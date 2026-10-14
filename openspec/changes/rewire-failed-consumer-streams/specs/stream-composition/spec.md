## MODIFIED Requirements

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
