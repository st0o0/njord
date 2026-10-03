## MODIFIED Requirements

### Requirement: The SchedulerActor obtains a SinkRef from the PipelineActor
The SchedulerActor SHALL resolve the PipelineActor reference asynchronously via `GetActorAsync<PipelineActor>().PipeTo(Self)` in `PreStart`. The actor SHALL NOT call synchronous `GetActor<PipelineActor>()` during `PreStart` or any state transition, because the PipelineActor may not yet be registered in the `IActorRegistry` at that point. Once the resolved reference arrives as a message, the actor SHALL `Watch` it, send `RequestPipelineSink` and `RequestPipelineSource`, and stash all timer messages until both refs are received. Only after obtaining the SinkRef SHALL the actor materialize a local `Source.Queue<WeightedTarget>` connected to the SinkRef and start scheduling timers.

#### Scenario: Scheduler resolves PipelineActor asynchronously on startup
- **WHEN** the SchedulerActor starts and PipelineActor is not yet registered in the IActorRegistry
- **THEN** the actor SHALL use `GetActorAsync<PipelineActor>().PipeTo(Self)` and wait for the resolved reference before sending pipeline requests

#### Scenario: Scheduler starts successfully regardless of registration order
- **WHEN** SchedulerActor is registered before PipelineActor in the same `WithResolvableActors` block
- **THEN** the SchedulerActor SHALL start without error and eventually receive the PipelineActor reference once it is registered

#### Scenario: SinkRef received triggers local queue materialization and scheduling
- **WHEN** the PipelineActor responds with a `PipelineSinkResponse` containing a `SinkRef<WeightedTarget>`
- **THEN** the SchedulerActor materializes a local `Source.Queue<WeightedTarget>` connected to `sinkRef.Sink`, schedules `ScheduleOnce` for every (location, model) pair, and unstashes pending messages

### Requirement: GetPollStates is handled in all behaviors
The SchedulerActor SHALL handle `GetPollStates` messages in ALL behaviors (`WaitingForPipeline`, `WaitingForRefs`, `Connecting`, `WaitingForConnection`, `Ready`) by responding immediately with a `PollStatesSnapshot` of the current `_states` dictionary. The handler SHALL NOT stash, delay, or drop the message in any state.

#### Scenario: Query returns current state during pipeline resolution
- **WHEN** a `GetPollStates` message is received while the actor is waiting for the PipelineActor reference to resolve
- **THEN** the actor SHALL respond with a `PollStatesSnapshot` (which may be empty if no states are initialized yet)

#### Scenario: Query returns current state in Ready
- **WHEN** a `GetPollStates` message is received in Ready state with 6 model poll states
- **THEN** the actor SHALL respond with `PollStatesSnapshot` containing 6 entries

#### Scenario: Query is read-only
- **WHEN** a `GetPollStates` message is received in any behavior
- **THEN** no events SHALL be persisted and no timers SHALL be scheduled

## REMOVED Requirements

### Requirement: GetPollStates is stashed in non-Ready behaviors
**Reason**: Replaced by "GetPollStates is handled in all behaviors" — stashing caused gRPC timeouts when the actor was not yet in Ready state.
**Migration**: No migration needed; the new requirement supersedes this one.
