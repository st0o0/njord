# stream-consumer-actor Specification

## Purpose

Base class for the consumers of the pipeline output (`ForecastReadModel`, `MqttStatePublisher`): it attaches to the pipeline through one SourceRef at a time (request id, reply timeout, capped backoff), watches the pipeline incarnation that answered, and re-attaches only when that incarnation terminates or the consumer's own graph completes or fails. The subclass only provides its graph; there is no waiting phase and no stash.

## Requirements
### Requirement: StreamConsumerActor tracks watched dependencies explicitly
The base SHALL watch exactly one actor: the pipeline incarnation that answered the current subscription's SourceRef request. It SHALL unwatch it when the subscription ends. A `Terminated` message for any other actor (an earlier incarnation, a StreamSupervisor child, any internal actor) SHALL be ignored.

#### Scenario: Terminated from tracked dependency triggers re-resolve
- **WHEN** a `Terminated` message arrives for the watched pipeline incarnation
- **THEN** the base ends the current subscription and attaches again after the backoff delay

#### Scenario: Terminated from untracked actor is ignored
- **WHEN** a `Terminated` message arrives for an actor other than the watched pipeline incarnation (e.g. a StreamSupervisor child)
- **THEN** the base ignores it silently and the subscription keeps running

### Requirement: StreamConsumerActor uses SharedKillSwitch for stream graph lifecycle
The base SHALL create a fresh `SharedKillSwitch` for every subscription and route the SourceRef's events through it before handing them to the subclass's graph. It SHALL shut the switch down whenever the subscription is ended by the base (pipeline termination, re-attach after the graph ended) and when the actor stops, before any new subscription is materialized. A graph ended by the actor's own kill switch or by the actor's stop SHALL NOT trigger a re-attach.

#### Scenario: Old graph terminated on dependency loss
- **WHEN** the watched pipeline incarnation terminates
- **THEN** the base shuts down the subscription's kill switch before attaching again

#### Scenario: New KillSwitch for new graph
- **WHEN** a new subscription is materialized after a re-attach
- **THEN** it runs through a fresh `SharedKillSwitch`

#### Scenario: KillSwitch prevents parent actor death
- **WHEN** the old subscription's SourceRef becomes unreachable after its kill switch was shut down
- **THEN** the stream stages terminate gracefully and the consumer actor remains alive

### Requirement: StreamConsumerActor request/response messages carry a correlation RequestId
Every handshake request the base sends upstream SHALL carry a `long RequestId`, and the replies SHALL echo it. The only such pair is `RequestPipelineOutput(long RequestId)` answered by `RequestPipelineOutputCompleted(RequestId, SourceRef)` or `RequestPipelineOutputFailed(RequestId, Cause)` (src/Njord.Core/Actors/StreamWiringMessages.cs), used for `MqttStatePublisher` and `ForecastReadModel` towards `ForecastPipeline`. The base class SHALL NOT send or handle SinkRef requests.

#### Scenario: Request carries RequestId
- **WHEN** the base sends `RequestPipelineOutput` to `ForecastPipeline`
- **THEN** the message contains the `long RequestId` of the current attach attempt

#### Scenario: Response echoes RequestId
- **WHEN** `ForecastPipeline` receives `RequestPipelineOutput(42)`
- **THEN** it responds with `RequestPipelineOutputCompleted(42, sourceRef)`

### Requirement: StreamConsumerActor rewires a graph that completes or fails
The subclass's graph SHALL return its completion `Task`. When the current subscription's graph completes or fails, the base SHALL log a Warning (naming the consumer and, for a failure, the cause), increment `njord_stream_consumer_rewires_total{consumer}`, shut down the subscription's kill switch, unwatch the pipeline incarnation and attach again after a jittered exponential backoff delay from the actor's retry policy. The delay SHALL grow with each consecutive lost subscription or failed attempt up to the policy's cap, and the count SHALL reset once a subscription has stayed up for a stability window (default 60 s). The completion of a subscription that was already replaced, ended by the actor's own kill switch or ended by the actor's stop SHALL be ignored. Subclass state SHALL survive the re-attach.

#### Scenario: A failed graph is rewired with a new SourceRef
- **WHEN** the graph of an attached consumer fails (a stage stops on a non-transient exception, or the SourceRef's remote side fails)
- **THEN** the consumer logs a Warning, requests a new SourceRef after the backoff delay, materializes a new graph and events flow again

#### Scenario: An unexpectedly completed graph is rewired
- **WHEN** the graph of an attached consumer completes (the producer's hub completed)
- **THEN** the consumer requests a new SourceRef after the backoff delay and materializes a new graph

#### Scenario: Completion of a replaced graph is ignored
- **WHEN** the pipeline incarnation terminated, the consumer materialized a new graph, and only then the old graph's completion arrives
- **THEN** no re-attach happens and the new graph keeps running

#### Scenario: Stopping the actor does not rewire
- **WHEN** the consumer actor is stopped and its graph ends with it
- **THEN** no SourceRef is requested again and no re-attach is counted

#### Scenario: Backoff grows on repeated graph losses
- **WHEN** every new graph fails right after it was materialized
- **THEN** the delay before each new SourceRef request grows exponentially (with jitter) up to the policy's cap instead of staying at the initial delay

#### Scenario: A rewire is counted
- **WHEN** a consumer re-attaches after a lost subscription
- **THEN** `njord_stream_consumer_rewires_total` is incremented by 1 with the tag `consumer` set to the consumer's name

### Requirement: StreamConsumerActor attaches to the pipeline output without a waiting phase
The `StreamConsumerActor` base class (src/Njord.Core/Actors/StreamConsumerActor.cs) SHALL attach its subclass to the forecast pipeline's output: when the actor starts it SHALL resolve the `IForecastPipeline` registry entry asynchronously, send `RequestPipelineOutput(RequestId)` to it and, on the matching `RequestPipelineOutputCompleted`, hand the SourceRef's events to the subclass's graph. The subclass SHALL only provide that graph (and its completion) and MAY name itself for logs and metrics; it SHALL NOT send handshake requests or handle handshake replies. The base SHALL have no waiting phase and SHALL NOT stash: messages of the subclass are handled from the start, whether or not a subscription is running. While the registry entry is missing the attach SHALL NOT crash the actor; it is retried like an unanswered request.

#### Scenario: Consumer attaches on start
- **WHEN** a consumer starts while the pipeline is registered
- **THEN** it sends one `RequestPipelineOutput` to the pipeline and runs its graph on the returned SourceRef

#### Scenario: Messages are handled before the first SourceRef
- **WHEN** a query reaches a consumer before the pipeline has answered its SourceRef request
- **THEN** the consumer answers it at once instead of holding it back

#### Scenario: Pipeline registered late
- **WHEN** a consumer starts before the pipeline is registered and the pipeline is registered afterwards
- **THEN** the consumer stays alive and attaches once the pipeline can be resolved

### Requirement: StreamConsumerActor retries attaching with capped backoff
Every attach attempt SHALL end: with the matching `RequestPipelineOutputCompleted`, with the matching `RequestPipelineOutputFailed`, with a failed resolution, or after a reply timeout (default 10 s) that covers both the resolution and the reply. A failed or unanswered attempt SHALL be logged as a Warning and retried after a jittered exponential backoff delay from the actor's retry policy (default 1 s doubling, capped at 30 s).

#### Scenario: Unanswered request is retried
- **WHEN** the pipeline does not answer a `RequestPipelineOutput` within the reply timeout
- **THEN** the consumer logs a Warning and sends a new `RequestPipelineOutput` with a new request id after the backoff delay

#### Scenario: Failed request is retried
- **WHEN** the pipeline answers `RequestPipelineOutputFailed` for the current request id
- **THEN** the consumer logs a Warning and requests a new SourceRef after the backoff delay

### Requirement: StreamConsumerActor re-attaches on pipeline termination
After a successful attach the base SHALL watch the actor that sent the `RequestPipelineOutputCompleted` (the pipeline incarnation, not the registry entry). When that actor terminates, the base SHALL end the current subscription and attach again after the backoff delay. A consumer SHALL NOT re-attach for any other actor's termination, and SHALL NOT re-attach while its subscription and the pipeline incarnation are alive (for example when the gateway restarts).

#### Scenario: Pipeline terminates
- **WHEN** the pipeline incarnation a consumer is attached to terminates and a new incarnation is registered
- **THEN** the consumer ends its old subscription, requests a SourceRef from the new incarnation and consumes its events

#### Scenario: Gateway restart causes no re-attach
- **WHEN** the gateway restarts while a consumer is attached to the pipeline
- **THEN** the consumer sends no further `RequestPipelineOutput` and keeps receiving events on its subscription

### Requirement: StreamConsumerActor filters stale replies by RequestId
The base SHALL give every attach attempt a new, monotonically increasing request id and SHALL ignore every reply, resolution result, reply timeout and graph completion that carries an id other than the current one.

#### Scenario: Stale SourceRef reply is ignored
- **WHEN** a `RequestPipelineOutputCompleted` for an earlier request id arrives after a retry was sent
- **THEN** the consumer does not run a graph on it and keeps waiting for the reply to the current request

#### Scenario: Matching reply is accepted
- **WHEN** a `RequestPipelineOutputCompleted` carries the current request id
- **THEN** the consumer runs its graph on that SourceRef

