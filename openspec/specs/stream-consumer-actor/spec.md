# stream-consumer-actor Specification

## Purpose

Base class for actors that resolve upstream dependencies via `GetActorAsync`, watch them, request StreamRefs, materialize a stream graph, re-resolve on `Terminated`, and rewire a graph that completes or fails on its own. Encapsulates dead-ref detection, exponential backoff retry, request-id correlation for stale-response filtering, dependency-only Terminated filtering, and `SharedKillSwitch`-based stream graph lifecycle with generation-tagged graph completion.
## Requirements
### Requirement: StreamConsumerActor tracks watched dependencies explicitly
The base SHALL maintain a `HashSet<IActorRef>` of explicitly tracked dependencies. The subclass SHALL call `TrackDependency(IActorRef)` in its `*Resolved` handlers. `TrackDependency` SHALL add the ref to the set and call `Context.Watch(ref)`. In `HandleTerminated`, the base SHALL ignore any `Terminated` message whose `ActorRef` is NOT in the tracked set. This prevents StreamSupervisor child termination or other internal actor deaths from triggering dependency re-resolution.

#### Scenario: Terminated from tracked dependency triggers re-resolve
- **WHEN** a `Terminated` message arrives for a ref in the tracked set
- **THEN** the base runs the HandleTerminated flow

#### Scenario: Terminated from untracked actor is ignored
- **WHEN** a `Terminated` message arrives for a ref NOT in the tracked set (e.g. StreamSupervisor child)
- **THEN** the base ignores it silently

### Requirement: StreamConsumerActor detects dead refs and retries with BackoffPolicy
When a `*Resolved` handler receives a ref that matches `_lastTerminatedRef` (the registry returned the same dead ref), the subclass SHALL call `ScheduleRetryResolve()` instead of watching and telling. `ScheduleRetryResolve` SHALL schedule a `RetryResolve` message to Self using `BackoffPolicy.DelayWithJitter(retryCount)` for the delay calculation. The `retryCount` SHALL be reset to 0 on successful `TryTransition`. The `RetryResolve` handler SHALL clear `_lastTerminatedRef` and call the async re-resolve path.

#### Scenario: Dead ref detected in Resolved handler
- **WHEN** a `*Resolved` handler receives a ref equal to `_lastTerminatedRef`
- **THEN** the subclass calls `ScheduleRetryResolve()` and does NOT watch or tell the ref

#### Scenario: Exponential backoff with jitter on repeated dead refs
- **WHEN** the first retry resolves the same dead ref again
- **THEN** the next retry delay uses `BackoffPolicy.DelayWithJitter(retryCount)` (doubles with randomized jitter, capped at max delay)

#### Scenario: Retry count resets on success
- **WHEN** `TryTransition` succeeds (all refs ready, graph materialized)
- **THEN** `retryCount` is reset to 0

### Requirement: StreamConsumerActor gates TryTransition while retry is pending
`TryTransition()` SHALL proceed when `AllRefsReady()` returns true. On success it SHALL clear `_lastTerminatedRef`, reset `_retryCount` to 0, call `MaterializeGraph(killSwitch)`, start watching the completion `Task` that `MaterializeGraph` returns (tagged with a new graph generation), transition to `Ready`, and unstash all messages. The `_lastTerminatedRef`-based guard (`_lastTerminatedRef is not null`) is removed from `TryTransition` — stale-response protection is now provided by request-id correlation in subclass response handlers.

#### Scenario: TryTransition succeeds when all refs ready
- **WHEN** `AllRefsReady()` returns true
- **THEN** the base clears `_lastTerminatedRef`, calls `MaterializeGraph`, watches the returned completion and transitions to `Ready`

#### Scenario: TryTransition blocked when refs incomplete
- **WHEN** `AllRefsReady()` returns false
- **THEN** `TryTransition` returns without side effects

#### Scenario: Stale response does not trigger Ready (via RequestId)
- **WHEN** a stale `*Response` arrives after `HandleTerminated` but before fresh responses
- **THEN** the subclass handler drops it (wrong RequestId) and `TryTransition` is never called with stale refs

### Requirement: StreamConsumerActor uses SharedKillSwitch for stream graph lifecycle
The base SHALL create a `SharedKillSwitch` (via `KillSwitches.Shared`) and pass it to `MaterializeGraph(SharedKillSwitch)`. The subclass SHALL wire `killSwitch.Flow<T>()` into its stream graph. In `HandleTerminated`, the base SHALL call `_killSwitch.Shutdown()` BEFORE re-resolving, and it SHALL do the same before rewiring a graph that completed or failed. This terminates the old stream graph cleanly, preventing the `Context.Materializer()`'s StreamSupervisor child actors from failing asynchronously and killing the parent actor via `FinishTerminate`. A graph ended by the actor's own kill switch SHALL NOT trigger a rewire.

#### Scenario: Old graph terminated on dependency loss
- **WHEN** `HandleTerminated` fires for a tracked dependency
- **THEN** the base calls `_killSwitch.Shutdown()` before any re-resolution

#### Scenario: New KillSwitch for new graph
- **WHEN** `HandleTerminated` creates a new resolution cycle
- **THEN** a fresh `SharedKillSwitch` is created for the next `MaterializeGraph` call

#### Scenario: KillSwitch prevents parent actor death
- **WHEN** the old stream graph's SinkRef/SourceRef becomes unreachable after KillSwitch shutdown
- **THEN** the StreamSupervisor child actors terminate gracefully and the parent actor remains alive

### Requirement: StreamConsumerActor HandleTerminated flow
On `Terminated` for a tracked dependency, the base SHALL execute in order: (1) remove the ref from the tracked set, (2) invalidate the current graph generation and any pending rewire, (3) shut down the current `SharedKillSwitch`, (4) create a new `SharedKillSwitch`, (5) remember the ref as the last terminated ref, (6) reset the retry count to 0, (7) increment the request-id counter (invalidating in-flight ids), and (8) send itself an internal recovery message. Handling that message SHALL (9) call the virtual `OnDependencyLost()` for subclass cleanup, (10) enter `WaitingForRefs` if the actor was `Ready`, and (11) call `ResolveDependencies()`. The completion of the graph shut down in step 3 SHALL NOT trigger a rewire.

#### Scenario: Full HandleTerminated sequence
- **WHEN** `HandleTerminated` fires for a tracked dependency
- **THEN** it executes steps 1–8, and after the recovery message steps 9–11, leaving the actor in `WaitingForRefs`

#### Scenario: Request-id counter incremented in HandleTerminated
- **WHEN** `HandleTerminated` fires
- **THEN** the request-id counter is incremented before `ResolveDependencies` runs

#### Scenario: OnDependencyLost hook called
- **WHEN** `HandleTerminated` fires
- **THEN** the subclass's `OnDependencyLost()` runs before re-resolution

### Requirement: StreamConsumerActor request/response messages carry a correlation RequestId
Every handshake request a `StreamConsumerActor` subclass sends upstream SHALL carry a `long RequestId`, and the replies SHALL echo it. Today the only such pair is `RequestPipelineOutput(long RequestId)` answered by `RequestPipelineOutputCompleted(RequestId, SourceRef)` or `RequestPipelineOutputFailed(RequestId, Cause)` (src/Njord.Core/Actors/StreamWiringMessages.cs), used by `MqttStatePublisher` and `ForecastReadModel` towards `ForecastPipeline`. The base class SHALL NOT send or handle SinkRef requests.

#### Scenario: Request carries RequestId
- **WHEN** a subclass sends `RequestPipelineOutput` to `ForecastPipeline`
- **THEN** the message contains a `long RequestId` generated by `NextRequestId()`

#### Scenario: Response echoes RequestId
- **WHEN** `ForecastPipeline` receives `RequestPipelineOutput(42)`
- **THEN** it responds with `RequestPipelineOutputCompleted(42, sourceRef)`

### Requirement: StreamConsumerActor base class provides NextRequestId
The base class SHALL expose a `protected long NextRequestId()` method that returns a monotonically incrementing value. `HandleTerminated` SHALL increment the counter, which invalidates all request ids that are currently in-flight.

#### Scenario: NextRequestId returns incrementing values
- **WHEN** a subclass calls `NextRequestId()` twice
- **THEN** the second value is greater than the first

#### Scenario: HandleTerminated invalidates in-flight request ids
- **WHEN** `HandleTerminated` fires and increments the request-id counter
- **THEN** any subsequent response carrying the old id will not match the subclass's stored id

### Requirement: StreamConsumerActor subclasses filter stale responses by RequestId
Each subclass SHALL store the `long` request id it sent per request type in a private field. In the handshake reply handlers, the subclass SHALL compare the reply's `RequestId` with the stored id and silently return if they differ.

#### Scenario: Stale SourceRef response with wrong RequestId is dropped
- **WHEN** a `RequestPipelineOutputCompleted(1, ...)` arrives but the subclass sent `RequestPipelineOutput(2)`
- **THEN** the handler silently returns without storing the SourceRef or calling `TryTransition`

#### Scenario: Fresh response with matching RequestId is accepted
- **WHEN** a `RequestPipelineOutputCompleted(2, ...)` arrives and the subclass sent `RequestPipelineOutput(2)`
- **THEN** the handler stores the SourceRef and calls `TryTransition`

### Requirement: StreamConsumerActor subclasses resolve initial dependencies synchronously
Subclasses SHALL override `ResolveInitialDependencies()` to resolve their upstream actors with `Context.GetActor<T>()` and call `TrackDependency` for each. The base SHALL call it exactly once, in `PreStart`, before `RequestSourceRefs()`. The async `ResolveDependencies()` path (`GetActorAsync` + `PipeTo`) SHALL only be used during runtime recovery after `Terminated`.

#### Scenario: Subclass provides sync refs at construction
- **WHEN** the actor starts
- **THEN** `ResolveInitialDependencies()` runs once and resolves all required actor refs synchronously

#### Scenario: Missing registry key at startup crashes the actor
- **WHEN** `Context.GetActor<T>()` throws `MissingActorRegistryEntryException` during start
- **THEN** the actor fails to start and its supervisor handles the restart

### Requirement: StreamConsumerActor runs a WaitingForRefs/Ready state machine from the start
The `StreamConsumerActor` base class (src/Njord.Core/Actors/StreamConsumerActor.cs) SHALL implement a two-phase state machine, `WaitingForRefs` and `Ready`. In `PreStart` it SHALL create the actor-scoped materializer, call the subclass's `ResolveInitialDependencies()` (synchronous `Context.GetActor<T>()`), enter `WaitingForRefs` and call `RequestSourceRefs()`. The actor SHALL stay in `WaitingForRefs`, stashing unrecognized messages, until `TryTransition()` finds all SourceRefs ready, materializes the graph and enters `Ready`. `WaitingForRefs` SHALL also be re-entered on runtime recovery after a tracked dependency terminates; then dependencies are resolved asynchronously through `ResolveDependencies()` (`GetActorAsync` + `PipeTo`). `WaitingForRefs` SHALL also be re-entered when the materialized graph completes or fails while the actor is `Ready`; then the SourceRefs are requested again from the still-tracked dependencies through `RequestSourceRefs()`. In `WaitingForRefs` the base SHALL handle `RetryResolve`, `Terminated`, its internal recovery and rewire messages and graph completions, and the subclass SHALL register its `*Resolved` and handshake handlers through `ConfigureWaitingForRefs()`; in `Ready` the base SHALL handle `Terminated` and graph completions and the subclass MAY add handlers through `ConfigureReady()`. The base SHALL only track SourceRef dependencies; subclasses SHALL NOT request SinkRefs.

#### Scenario: Startup waits for the first SourceRef
- **WHEN** the actor starts
- **THEN** it resolves its dependencies synchronously, requests its SourceRefs and stays in `WaitingForRefs` until they arrive

#### Scenario: Messages stashed until the graph is materialized
- **WHEN** a query arrives before the first SourceRef is received
- **THEN** it is stashed and replayed on the transition to `Ready`

#### Scenario: Terminated triggers async re-resolve via WaitingForRefs
- **WHEN** a tracked dependency sends `Terminated` during `Ready`
- **THEN** the actor enters `WaitingForRefs` and resolves dependencies asynchronously (`GetActorAsync` + `PipeTo`)

### Requirement: StreamConsumerActor rewires a graph that completes or fails
`MaterializeGraph` SHALL return the completion `Task` of the materialized graph. When the current graph completes or fails while the actor is `Ready`, the base SHALL log a Warning (naming the consumer and, for a failure, the cause), increment `njord_stream_consumer_rewires_total{consumer}`, shut down and replace the kill switch, invalidate in-flight request ids, call the virtual `OnStreamLost()` so the subclass drops its SourceRefs (but keeps its dependency refs), enter `WaitingForRefs`, and request new SourceRefs through `RequestSourceRefs()` after a jittered exponential backoff delay from the actor's retry policy. The delay SHALL grow with each consecutive graph loss up to the policy's cap, and the loss count SHALL reset once a materialized graph has stayed up for a stability window (default 60 s). The completion of a graph that was already replaced, ended by the actor's own kill switch or ended by the actor's stop SHALL be ignored. Subclass state that is not dropped in `OnStreamLost()` SHALL survive the rewire.

#### Scenario: A failed graph is rewired with a new SourceRef
- **WHEN** the graph of a `Ready` consumer fails (a stage stops on a non-transient exception, or the SourceRef's remote side fails)
- **THEN** the consumer logs a Warning, requests a new SourceRef after the backoff delay, materializes a new graph and events flow again

#### Scenario: An unexpectedly completed graph is rewired
- **WHEN** the graph of a `Ready` consumer completes (the producer's hub completed)
- **THEN** the consumer requests a new SourceRef after the backoff delay and materializes a new graph

#### Scenario: Completion of a replaced graph is ignored
- **WHEN** a tracked dependency terminated, the consumer materialized a new graph, and only then the old graph's completion arrives
- **THEN** no rewire happens and the new graph keeps running

#### Scenario: Stopping the actor does not rewire
- **WHEN** the consumer actor is stopped and its graph ends with it
- **THEN** no SourceRef is requested again and no rewire is counted

#### Scenario: Backoff grows on repeated graph losses
- **WHEN** every new graph fails right after it was materialized
- **THEN** the delay before each new SourceRef request grows exponentially (with jitter) up to the policy's cap instead of staying at the initial delay

#### Scenario: A rewire is counted
- **WHEN** a consumer rewires after a graph loss
- **THEN** `njord_stream_consumer_rewires_total` is incremented by 1 with the tag `consumer` set to the consumer's name

