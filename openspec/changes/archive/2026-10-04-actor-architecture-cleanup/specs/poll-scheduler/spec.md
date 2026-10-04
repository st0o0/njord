## MODIFIED Requirements

### Requirement: The PollSchedulerActor sends ScheduledPoll via Tell to PipelineActor
The PollSchedulerActor SHALL resolve the PipelineActor reference asynchronously via `GetActorAsync<IPipelineActor>().PipeTo(Self)` in `PreStart`. Once resolved, the actor SHALL call `Context.Watch` on the PipelineActor ref. The actor SHALL request a `SourceRef<FetchOutcome>` from PipelineActor for failure feedback. The actor SHALL NOT request a SinkRef from PipelineActor. The actor SHALL NOT materialize a local `Source.Queue<WeightedTarget>` connected to a SinkRef.

When a `ScheduledPoll` fires, the actor SHALL send it directly to PipelineActor via Tell. PipelineActor handles the `ScheduledPoll` message and enqueues the target into its internal queue.

The actor SHALL have at most two Become phases: `WaitingForPipeline` (resolving PipelineActor + requesting SourceRef for failure feedback) and `Ready`. The `WaitingForRefs`, `Connecting`, and `WaitingForConnection` phases are eliminated.

On `Terminated` for the PipelineActor, the actor SHALL re-resolve with dead-ref detection and exponential backoff. It SHALL transition to `WaitingForPipeline`. Poll timers already scheduled SHALL continue to fire; their `ScheduledPoll` messages SHALL be stashed until the actor is `Ready` again.

#### Scenario: Scheduler sends ScheduledPoll to PipelineActor
- **WHEN** a ScheduleOnce timer fires for (lucerne, icon_d2)
- **THEN** the actor sends `ScheduledPoll("lucerne", "icon_d2")` to PipelineActor via Tell

#### Scenario: No SinkRef requested
- **WHEN** the PollSchedulerActor resolves PipelineActor
- **THEN** it does NOT send `RequestPipelineSink` — only `RequestPipelineSource` for failure feedback

#### Scenario: No local Source.Queue materialized
- **WHEN** the PollSchedulerActor starts
- **THEN** no `Source.Queue<WeightedTarget>` is materialized

#### Scenario: Two Become phases only
- **WHEN** the PollSchedulerActor starts
- **THEN** it transitions through at most `WaitingForPipeline` → `Ready`
- **THEN** there are no `WaitingForRefs`, `Connecting`, or `WaitingForConnection` phases

#### Scenario: PipelineActor terminated triggers re-resolve
- **WHEN** PipelineActor terminates
- **THEN** the actor transitions to `WaitingForPipeline` and re-resolves with backoff

#### Scenario: ScheduledPoll stashed during WaitingForPipeline
- **WHEN** a `ScheduledPoll` arrives while the actor is in `WaitingForPipeline`
- **THEN** it is stashed and replayed on transition to `Ready`

### Requirement: QueryPollStates is handled in all behaviors
The PollSchedulerActor SHALL handle `QueryPollStates` messages in ALL behaviors (`WaitingForPipeline` and `Ready`) by responding immediately with a `QueryPollStatesResult` of the current `SchedulerState.States` dictionary. The handler SHALL NOT stash, delay, or drop the message in any state.

#### Scenario: Query returns current state during pipeline resolution
- **WHEN** a `QueryPollStates` message is received while the actor is in `WaitingForPipeline`
- **THEN** the actor SHALL respond with a `QueryPollStatesResult` (which may be empty if no states are initialized yet)

#### Scenario: Query returns current state in Ready
- **WHEN** a `QueryPollStates` message is received in Ready state with 6 model poll states
- **THEN** the actor SHALL respond with `QueryPollStatesResult` containing 6 entries

#### Scenario: Query is read-only
- **WHEN** a `QueryPollStates` message is received in any behavior
- **THEN** no events SHALL be persisted and no timers SHALL be scheduled

### Requirement: OfferAsync result is handled in the Ready state
**This requirement is removed — see REMOVED Requirements below.**

## REMOVED Requirements

### Requirement: The SchedulerActor obtains a SinkRef from the PipelineActor
**Reason**: The SinkRef connection pattern is eliminated. The PollSchedulerActor now sends `ScheduledPoll` to PipelineActor via Tell. PipelineActor owns its internal queue.
**Migration**: Replace `RequestPipelineSink`/`PipelineSinkResponse` handling with direct Tell of `ScheduledPoll`. Remove `Source.Queue<WeightedTarget>` materialization. Remove `WaitingForRefs`, `Connecting`, `WaitingForConnection` Become phases.

### Requirement: OfferAsync result is handled in the Ready state
**Reason**: There is no local `Source.Queue` and no `OfferAsync` call. The PollSchedulerActor sends `ScheduledPoll` via Tell, which is fire-and-forget.
**Migration**: Remove `OfferAsync` result handling. If PipelineActor's internal queue is full, PipelineActor handles backpressure internally.
