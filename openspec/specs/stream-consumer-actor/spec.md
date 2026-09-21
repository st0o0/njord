# stream-consumer-actor Specification

## Purpose

Base class for actors that resolve upstream dependencies via `GetActorAsync`, watch them, request StreamRefs, materialize a stream graph, and re-resolve on `Terminated`. Encapsulates dead-ref detection, exponential backoff retry, request-id correlation for stale-response filtering, dependency-only Terminated filtering, and `SharedKillSwitch`-based stream graph lifecycle.

## Requirements

### Requirement: StreamConsumerActor provides a dependency-resolution state machine
The `StreamConsumerActor` base class SHALL implement a two-phase state machine: `WaitingForRefs` and `Ready`. In `WaitingForRefs`, the base SHALL handle `RetryResolve`, `Terminated`, and stash unrecognized messages via `ReceiveAny`. The subclass SHALL register its typed `*Resolved` and `*Response` handlers by overriding `ConfigureWaitingForRefs()`. In `Ready`, the base SHALL handle `Terminated` and the subclass MAY register additional handlers by overriding `ConfigureReady()`.

#### Scenario: Actor starts in WaitingForRefs
- **WHEN** the actor starts
- **THEN** it calls `ResolveDependencies()` and enters `WaitingForRefs`

#### Scenario: Transition to Ready when all refs collected
- **WHEN** all dependencies have responded with valid StreamRefs and no retry is pending
- **THEN** the base calls `MaterializeGraph(killSwitch)` and transitions to `Ready`

#### Scenario: Messages stashed during WaitingForRefs
- **WHEN** an unrecognized message arrives during `WaitingForRefs`
- **THEN** it is stashed and replayed on transition to `Ready`

### Requirement: StreamConsumerActor tracks watched dependencies explicitly
The base SHALL maintain a `HashSet<IActorRef>` of explicitly tracked dependencies. The subclass SHALL call `TrackDependency(IActorRef)` in its `*Resolved` handlers. `TrackDependency` SHALL add the ref to the set and call `Context.Watch(ref)`. In `HandleTerminated`, the base SHALL ignore any `Terminated` message whose `ActorRef` is NOT in the tracked set. This prevents StreamSupervisor child termination or other internal actor deaths from triggering dependency re-resolution.

#### Scenario: Terminated from tracked dependency triggers re-resolve
- **WHEN** a `Terminated` message arrives for a ref in the tracked set
- **THEN** the base runs the HandleTerminated flow

#### Scenario: Terminated from untracked actor is ignored
- **WHEN** a `Terminated` message arrives for a ref NOT in the tracked set (e.g. StreamSupervisor child)
- **THEN** the base ignores it silently

### Requirement: StreamConsumerActor detects dead refs and retries with exponential backoff
When a `*Resolved` handler receives a ref that matches `_lastTerminatedRef` (the registry returned the same dead ref), the subclass SHALL call `ScheduleRetryResolve()` instead of watching and telling. `ScheduleRetryResolve` SHALL schedule a `RetryResolve` message to Self with exponential backoff: `delay = min(1s × 2^retryCount, 30s)`. The `retryCount` SHALL be reset to 0 on successful `TryTransition`. The `RetryResolve` handler SHALL clear `_lastTerminatedRef` and call `ResolveDependencies()`.

#### Scenario: Dead ref detected in Resolved handler
- **WHEN** a `*Resolved` handler receives a ref equal to `_lastTerminatedRef`
- **THEN** the subclass calls `ScheduleRetryResolve()` and does NOT watch or tell the ref

#### Scenario: Exponential backoff on repeated dead refs
- **WHEN** the first retry resolves the same dead ref again
- **THEN** the next retry delay doubles (1s → 2s → 4s → ... capped at 30s)

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
On `Terminated` for a tracked dependency, the base SHALL execute in order: (1) remove the ref from `_watchedDeps`, (2) shutdown the current `_killSwitch`, (3) create a new `SharedKillSwitch`, (4) set `_lastTerminatedRef`, (5) reset `_retryCount` to 0, (6) increment the request-id counter (invalidating in-flight ids), (7) call virtual `OnDependencyLost()` for subclass cleanup, (8) call `ResolveDependencies()`, (9) transition to `WaitingForRefs`.

#### Scenario: Full HandleTerminated sequence
- **WHEN** `HandleTerminated` fires for a tracked dependency
- **THEN** it executes steps 1–9 in order and the actor is in `WaitingForRefs`

#### Scenario: Request-id counter incremented in HandleTerminated
- **WHEN** `HandleTerminated` fires
- **THEN** the request-id counter is incremented before `ResolveDependencies` (step 6 before step 8)

#### Scenario: OnDependencyLost hook called
- **WHEN** `HandleTerminated` fires
- **THEN** the subclass's `OnDependencyLost()` runs before re-resolution

### Requirement: StreamConsumerActor request/response messages carry a correlation RequestId
All `Request*` messages sent by `StreamConsumerActor` subclasses to upstream actors (MqttConnectionActor, EgressActor, PipelineActor) SHALL carry a `long RequestId` field. The corresponding `*Response` messages SHALL echo the same `RequestId` back. The affected message pairs are: `RequestMqttSink`/`MqttSinkResponse`, `RequestEgressSource`/`EgressSourceResponse`, `RequestEgressSink`/`EgressSinkResponse`, `RequestPipelineSource`/`PipelineSourceResponse`.

#### Scenario: Request carries RequestId
- **WHEN** a subclass sends `RequestMqttSink` to `MqttConnectionActor`
- **THEN** the message contains a `long RequestId` generated by `NextRequestId()`

#### Scenario: Response echoes RequestId
- **WHEN** `MqttConnectionActor` receives `RequestMqttSink(RequestId: 42)`
- **THEN** it responds with `MqttSinkResponse(RequestId: 42, SinkRef: ...)`

### Requirement: StreamConsumerActor base class provides NextRequestId
The base class SHALL expose a `protected long NextRequestId()` method that returns a monotonically incrementing value. `HandleTerminated` SHALL increment the counter, which invalidates all request ids that are currently in-flight.

#### Scenario: NextRequestId returns incrementing values
- **WHEN** a subclass calls `NextRequestId()` twice
- **THEN** the second value is greater than the first

#### Scenario: HandleTerminated invalidates in-flight request ids
- **WHEN** `HandleTerminated` fires and increments the request-id counter
- **THEN** any subsequent response carrying the old id will not match the subclass's stored id

### Requirement: StreamConsumerActor subclasses filter stale responses by RequestId
Each subclass SHALL store the `long` request id it sent per request type in a private field. In the `*Response` handler, the subclass SHALL compare `response.RequestId` against the stored id and silently return if they differ.

#### Scenario: Stale response with wrong RequestId is dropped
- **WHEN** a `MqttSinkResponse(RequestId: 1)` arrives but the subclass sent `RequestMqttSink(RequestId: 2)`
- **THEN** the handler silently returns without setting `_mqttSinkRef` or calling `TryTransition`

#### Scenario: Fresh response with matching RequestId is accepted
- **WHEN** a `MqttSinkResponse(RequestId: 2)` arrives and the subclass sent `RequestMqttSink(RequestId: 2)`
- **THEN** the handler sets `_mqttSinkRef` and calls `TryTransition`
