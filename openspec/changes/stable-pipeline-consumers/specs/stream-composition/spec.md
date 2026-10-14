## MODIFIED Requirements

### Requirement: Stream lifecycle is bound to the owning actor
Stream graphs SHALL terminate through the lifecycle of the actor that owns them (`Context.Materializer()`); `StreamConsumerActor` subclasses SHALL additionally run each pipeline subscription through a `SharedKillSwitch` that the base shuts down when the pipeline incarnation it is attached to terminates, before it re-attaches after the subscription ended, and when the actor stops. A consumer graph that ends while its owning actor keeps running SHALL be rebuilt by that actor on a new SourceRef; the gateway's forecast branch that ends SHALL be re-attached by the gateway; the gateway's fetch graph and the pipeline's analysis graph that end SHALL restart their actor. A graph that ends because the actor stops or replaced it SHALL NOT be rebuilt. There SHALL be no `StopStreams`/`StreamsStopped` messages and no CoordinatedShutdown stream task.

#### Scenario: Actor stop ends the stream
- **WHEN** an actor that owns a stream graph stops
- **THEN** the graph terminates with the actor-scoped materializer

#### Scenario: Dependency loss ends the consumer stream
- **WHEN** `ForecastPipeline` terminates while `MqttStatePublisher` consumes its output
- **THEN** `MqttStatePublisher` shuts down its kill switch before attaching to the pipeline again

#### Scenario: An ended graph of a running actor is rebuilt
- **WHEN** the pipeline output hub that `MqttStatePublisher` consumes completes while both actors keep running
- **THEN** `MqttStatePublisher` requests a new SourceRef and publishes the events of the new subscription

### Requirement: Stream-ref requesters handle typed handshake failures
Every actor or service that requests a SinkRef or SourceRef SHALL handle the matching project-owned failure record from `Njord.Core/Actors/StreamWiringMessages.cs`, each carrying the originating `RequestId` and the `Exception` cause, and SHALL ignore a reply whose `RequestId` is not its current one: `OpenMeteoGateway` handles `RequestForecastSinkFailed` (and a missing reply) by logging a warning and requesting a new SinkRef after jittered backoff; the `StreamConsumerActor` base handles `RequestPipelineOutputFailed` (and a missing reply) for its subclasses by logging a warning and requesting a new SourceRef after jittered backoff; the gRPC services treat `RequestReadModelStreamFailed` and `RequestConfigStreamFailed` as `Unavailable`. The system SHALL NOT use Akka's `Status.Failure`/`Status.Success` as replies or stream-termination messages.

#### Scenario: Stream consumer retries after a failed ref request
- **WHEN** a `StreamConsumerActor` subclass receives `RequestPipelineOutputFailed` for its current request id
- **THEN** it logs a warning and re-requests the ref after the next backoff delay

#### Scenario: Gateway retries after a failed sink request
- **WHEN** `OpenMeteoGateway` receives `RequestForecastSinkFailed` for its current request id
- **THEN** it logs a warning and requests a new SinkRef after the next backoff delay

#### Scenario: gRPC stream call fails with Unavailable
- **WHEN** `StreamForecasts` or `StreamEnrichments` asks `ForecastReadModel` for a stream and does not get `RequestReadModelStreamCompleted`
- **THEN** the call ends with `RpcException` status `Unavailable`
