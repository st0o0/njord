# stream-consumer-actor Specification

## Purpose

Base class for actors that resolve upstream dependencies via `GetActorAsync`, watch them, request StreamRefs, materialize a stream graph, and re-resolve on `Terminated`. Encapsulates dead-ref detection, exponential backoff retry, stale-response gating, dependency-only Terminated filtering, and `SharedKillSwitch`-based stream graph lifecycle.

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
`TryTransition()` SHALL NOT proceed if `_lastTerminatedRef is not null`. This prevents stale in-flight `*Response` messages (from PipeTo chains initiated before `HandleTerminated`) from triggering a premature transition to `Ready` with broken StreamRefs. When `RetryResolve` clears `_lastTerminatedRef` and fresh responses arrive, they overwrite the stale values and `TryTransition` proceeds with valid refs.

#### Scenario: Stale response does not trigger Ready
- **WHEN** a stale `*Response` arrives after `HandleTerminated` but before `RetryResolve`
- **THEN** the ref is stored but `TryTransition` does not proceed (blocked by `_lastTerminatedRef`)

#### Scenario: Fresh responses after retry trigger Ready
- **WHEN** `RetryResolve` fires, fresh `*Resolved` and `*Response` messages arrive
- **THEN** `TryTransition` proceeds and materializes the graph

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
On `Terminated` for a tracked dependency, the base SHALL execute in order: (1) remove the ref from `_watchedDeps`, (2) shutdown the current `_killSwitch`, (3) create a new `SharedKillSwitch`, (4) set `_lastTerminatedRef`, (5) reset `_retryCount` to 0, (6) call virtual `OnDependencyLost()` for subclass cleanup, (7) call `ResolveDependencies()`, (8) transition to `WaitingForRefs`.

#### Scenario: Full HandleTerminated sequence
- **WHEN** `HandleTerminated` fires for a tracked dependency
- **THEN** it executes steps 1-8 in order and the actor is in `WaitingForRefs`

#### Scenario: OnDependencyLost hook called
- **WHEN** `HandleTerminated` fires
- **THEN** the subclass's `OnDependencyLost()` runs before re-resolution (e.g. queue.Complete())
