## MODIFIED Requirements

### Requirement: StreamConsumerActor provides a dependency-resolution state machine
The `StreamConsumerActor` base class SHALL implement a two-phase state machine: `WaitingForRefs` and `Ready`. On initial startup, `ResolveDependencies()` SHALL resolve all dependencies **synchronously** via `Context.GetActor<T>()` in the constructor, call `MaterializeGraph(killSwitch)`, and enter `Ready` directly — no `WaitingForRefs` phase on first start. The `WaitingForRefs` state SHALL only be entered on **runtime recovery** after a `Terminated` event (re-resolve after a dependency crashes). In `WaitingForRefs`, the base SHALL handle `RetryResolve`, `Terminated`, and stash unrecognized messages via `ReceiveAny`. The subclass SHALL register its typed `*Resolved` and `*Response` handlers by overriding `ConfigureWaitingForRefs()`. In `Ready`, the base SHALL handle `Terminated` and the subclass MAY register additional handlers by overriding `ConfigureReady()`. The base SHALL only track SourceRef dependencies — subclasses SHALL NOT request or track SinkRefs. The `AllRefsReady()` check SHALL only verify SourceRef availability.

#### Scenario: Actor starts in Ready after sync resolution
- **WHEN** the actor starts
- **THEN** it calls `ResolveDependencies()` synchronously, calls `MaterializeGraph`, and enters `Ready`

#### Scenario: No WaitingForRefs phase on initial startup
- **WHEN** the actor starts for the first time
- **THEN** it does NOT enter `WaitingForRefs` and does NOT stash messages

#### Scenario: Terminated triggers async re-resolve via WaitingForRefs
- **WHEN** a tracked dependency sends `Terminated` during `Ready`
- **THEN** the actor enters `WaitingForRefs` and resolves dependencies asynchronously (GetActorAsync + PipeTo)

#### Scenario: Messages stashed during WaitingForRefs
- **WHEN** an unrecognized message arrives during `WaitingForRefs` (runtime recovery)
- **THEN** it is stashed and replayed on transition to `Ready`

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

## ADDED Requirements

### Requirement: StreamConsumerActor subclasses resolve initial dependencies synchronously
Subclasses SHALL override a sync resolution method that returns actor refs resolved via `Context.GetActor<T>()`. This method SHALL be called once during construction. The async `ResolveDependencies()` path (GetActorAsync + PipeTo) SHALL only be used during runtime recovery after `Terminated`.

#### Scenario: Subclass provides sync refs at construction
- **WHEN** the base class constructs
- **THEN** the subclass's sync resolution method is called and returns all required actor refs

#### Scenario: Missing registry key at startup crashes the actor
- **WHEN** `Context.GetActor<T>()` throws `MissingActorRegistryEntryException` during construction
- **THEN** the actor fails to start (constructor exception) and the supervisor handles the restart
