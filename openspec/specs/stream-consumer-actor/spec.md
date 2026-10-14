# stream-consumer-actor Specification

## Purpose

Base class for actors that resolve upstream dependencies via `GetActorAsync`, watch them, request StreamRefs, materialize a stream graph, and re-resolve on `Terminated`. Encapsulates dead-ref detection, exponential backoff retry, request-id correlation for stale-response filtering, dependency-only Terminated filtering, and `SharedKillSwitch`-based stream graph lifecycle.
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
`TryTransition()` SHALL proceed when `AllRefsReady()` returns true. On success it SHALL clear `_lastTerminatedRef`, reset `_retryCount` to 0, call `MaterializeGraph(killSwitch)`, transition to `Ready`, and unstash all messages. The `_lastTerminatedRef`-based guard (`_lastTerminatedRef is not null`) is removed from `TryTransition` — stale-response protection is now provided by request-id correlation in subclass response handlers.

#### Scenario: TryTransition succeeds when all refs ready
- **WHEN** `AllRefsReady()` returns true
- **THEN** the base clears `_lastTerminatedRef`, calls `MaterializeGraph`, and transitions to `Ready`

#### Scenario: TryTransition blocked when refs incomplete
- **WHEN** `AllRefsReady()` returns false
- **THEN** `TryTransition` returns without side effects

#### Scenario: Stale response does not trigger Ready (via RequestId)
- **WHEN** a stale `*Response` arrives after `HandleTerminated` but before fresh responses
- **THEN** the subclass handler drops it (wrong RequestId) and `TryTransition` is never called with stale refs

### Requirement: StreamConsumerActor uses SharedKillSwitch for stream graph lifecycle
The base SHALL create a `SharedKillSwitch` (via `KillSwitches.Shared`) and pass it to `MaterializeGraph(SharedKillSwitch)`. The subclass SHALL wire `killSwitch.Flow<T>()` into its stream graph. In `HandleTerminated`, the base SHALL call `_killSwitch.Shutdown()` BEFORE re-resolving. This terminates the old stream graph cleanly, preventing the `Context.Materializer()`'s StreamSupervisor child actors from failing asynchronously and killing the parent actor via `FinishTerminate`.

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
On `Terminated` for a tracked dependency, the base SHALL execute in order: (1) remove the ref from the tracked set, (2) shut down the current `SharedKillSwitch`, (3) create a new `SharedKillSwitch`, (4) remember the ref as the last terminated ref, (5) reset the retry count to 0, (6) increment the request-id counter (invalidating in-flight ids), and (7) send itself an internal recovery message. Handling that message SHALL (8) call the virtual `OnDependencyLost()` for subclass cleanup, (9) enter `WaitingForRefs` if the actor was `Ready`, and (10) call `ResolveDependencies()`.

#### Scenario: Full HandleTerminated sequence
- **WHEN** `HandleTerminated` fires for a tracked dependency
- **THEN** it executes steps 1–7, and after the recovery message steps 8–10, leaving the actor in `WaitingForRefs`

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
The `StreamConsumerActor` base class (src/Njord.Core/Actors/StreamConsumerActor.cs) SHALL implement a two-phase state machine, `WaitingForRefs` and `Ready`. In `PreStart` it SHALL create the actor-scoped materializer, call the subclass's `ResolveInitialDependencies()` (synchronous `Context.GetActor<T>()`), enter `WaitingForRefs` and call `RequestSourceRefs()`. The actor SHALL stay in `WaitingForRefs`, stashing unrecognized messages, until `TryTransition()` finds all SourceRefs ready, materializes the graph and enters `Ready`. `WaitingForRefs` SHALL also be re-entered on runtime recovery after a tracked dependency terminates; then dependencies are resolved asynchronously through `ResolveDependencies()` (`GetActorAsync` + `PipeTo`). In `WaitingForRefs` the base SHALL handle `RetryResolve`, `Terminated` and its internal recovery message and the subclass SHALL register its `*Resolved` and handshake handlers through `ConfigureWaitingForRefs()`; in `Ready` the base SHALL handle `Terminated` and the subclass MAY add handlers through `ConfigureReady()`. The base SHALL only track SourceRef dependencies; subclasses SHALL NOT request SinkRefs.

#### Scenario: Startup waits for the first SourceRef
- **WHEN** the actor starts
- **THEN** it resolves its dependencies synchronously, requests its SourceRefs and stays in `WaitingForRefs` until they arrive

#### Scenario: Messages stashed until the graph is materialized
- **WHEN** a query arrives before the first SourceRef is received
- **THEN** it is stashed and replayed on the transition to `Ready`

#### Scenario: Terminated triggers async re-resolve via WaitingForRefs
- **WHEN** a tracked dependency sends `Terminated` during `Ready`
- **THEN** the actor enters `WaitingForRefs` and resolves dependencies asynchronously (`GetActorAsync` + `PipeTo`)

