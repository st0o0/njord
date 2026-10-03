# poll-scheduler Delta Specification

## MODIFIED Requirements

### Requirement: The SchedulerActor obtains a SinkRef from the PipelineActor
The SchedulerActor SHALL resolve the PipelineActor reference asynchronously via `GetActorAsync<PipelineActor>().PipeTo(Self)` in `PreStart`. The actor SHALL NOT call synchronous `GetActor<PipelineActor>()` during `PreStart` or any state transition, because the PipelineActor may not yet be registered in the `IActorRegistry` at that point. Once the resolved reference arrives as a message, the actor SHALL call `Context.Watch` and add the ref to a `HashSet<IActorRef> _watchedDeps`, send `RequestPipelineSink` and `RequestPipelineSource`, and stash all timer messages until both refs are received. Only after obtaining the SinkRef SHALL the actor materialize a local `Source.Queue<WeightedTarget>` connected to the SinkRef and start scheduling timers.

The SchedulerActor SHALL detect dead refs returned by `GetActorAsync` (ref matches `_lastTerminatedRef`) and schedule a retry with exponential backoff (`min(1s × 2^retryCount, 30s)`) instead of immediately re-resolving. It SHALL gate `TryTransitionToConnecting` with `_lastTerminatedRef is not null` to prevent stale in-flight responses from triggering premature transitions.

The SchedulerActor SHALL wire a `SharedKillSwitch` into both materialized stream graphs (the `Source.Queue → SinkRef.Sink` graph and the `SourceRef.Source → Sink.ActorRef` failure-consumer graph). On `Terminated` for a tracked dependency, it SHALL call `_killSwitch.Shutdown()` before re-resolving.

On `Terminated`, the actor SHALL ignore any ref not in `_watchedDeps`. This prevents StreamSupervisor child termination from triggering dependency re-resolution.

#### Scenario: Scheduler resolves PipelineActor asynchronously on startup
- **WHEN** the SchedulerActor starts and PipelineActor is not yet registered in the IActorRegistry
- **THEN** the actor SHALL use `GetActorAsync<PipelineActor>().PipeTo(Self)` and wait for the resolved reference before sending pipeline requests

#### Scenario: Scheduler starts successfully regardless of registration order
- **WHEN** SchedulerActor is registered before PipelineActor in the same `WithResolvableActors` block
- **THEN** the SchedulerActor SHALL start without error and eventually receive the PipelineActor reference once it is registered

#### Scenario: SinkRef received triggers local queue materialization and scheduling
- **WHEN** the PipelineActor responds with a `PipelineSinkResponse` containing a `SinkRef<WeightedTarget>`
- **THEN** the SchedulerActor materializes a local `Source.Queue<WeightedTarget>` connected to `sinkRef.Sink`, schedules `ScheduleOnce` for every (location, model) pair, and unstashes pending messages

#### Scenario: Dead ref detected triggers backoff retry
- **WHEN** `GetActorAsync<PipelineActor>` returns the same dead ref from the registry
- **THEN** the actor schedules a retry with exponential backoff instead of tight-looping

#### Scenario: KillSwitch shuts down both graphs on dependency loss
- **WHEN** `Terminated` fires for the PipelineActor
- **THEN** both the Source.Queue graph and the failure-consumer graph are shut down via KillSwitch

#### Scenario: Untracked Terminated is ignored
- **WHEN** `Terminated` arrives for a ref not in `_watchedDeps` (e.g. StreamSupervisor child)
- **THEN** the actor ignores it
