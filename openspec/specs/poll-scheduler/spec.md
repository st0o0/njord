# poll-scheduler Specification

## Purpose

Adaptive per-model poll scheduling: a persistent actor that learns each weather model's update cycle from data hash changes, schedules polls via ScheduleOnce timers, and persists learned rhythms across restarts via Akka.Persistence + SQLite.

## Requirements

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

### Requirement: The SchedulerActor manages per-model poll timing
A `SchedulerActor` (ReceivePersistentActor) SHALL maintain a `ModelPollState` per configured (location, model) pair. Each state SHALL track: `lastHash` (int?), `lastChangeUtc` (DateTimeOffset?), `prevChangeUtc` (DateTimeOffset?), `nextPollUtc` (DateTimeOffset), `missCount` (int), and `phase` (Discovery or Steady). The actor SHALL use `ScheduleTellOnce` to fire polls at each model's individually calculated time. On first initialization (no prior persisted state), all models SHALL have `NextPollUtc = now` — there is no stagger delay. The pipeline's Throttle operator is the sole rate-limiting gate.

#### Scenario: Each model gets its own timer
- **WHEN** 1 location and 8 models are configured
- **THEN** the SchedulerActor maintains 8 independent `ModelPollState` entries, each with its own `ScheduleOnce` timer

#### Scenario: Timer fires offer a target into the local queue
- **WHEN** a `ScheduleOnce` timer fires for (lucerne, icon_d2)
- **THEN** the actor offers a `WeightedTarget(lucerne, icon_d2)` into its own local `Source.Queue`, which drains through the SinkRef into the PipelineActor's MergeHub

#### Scenario: Initial polls are offered without stagger delay
- **WHEN** the SchedulerActor initializes with 27 (location, model) pairs and no prior persisted state
- **THEN** all 27 `ScheduleOnce` timers fire with `NextPollUtc = now`, offering all targets to the queue immediately
- **AND** the pipeline Throttle shapes them to 2 req/sec

#### Scenario: Recovered state preserves existing NextPollUtc
- **WHEN** the SchedulerActor recovers with persisted state for a model
- **THEN** the recovered `NextPollUtc` is used as-is (no stagger applied)

### Requirement: Discovery phase polls at a fixed interval until the cycle is learned
When no cycle is known for a (location, model) pair (phase = Discovery), the SchedulerActor SHALL poll every 20 minutes via `ScheduleOnce`. After two consecutive data changes are detected (two different `lastChangeUtc` values), the actor SHALL compute `cycle = lastChangeUtc - prevChangeUtc` and transition to Steady phase.

#### Scenario: Discovery polls every 20 minutes
- **WHEN** a model has no known cycle (phase = Discovery)
- **THEN** the next poll is scheduled 20 minutes from now

#### Scenario: First data change is recorded but stays in Discovery
- **WHEN** the first hash change is detected for a model
- **THEN** `lastChangeUtc` is set, `prevChangeUtc` remains null, and the phase stays Discovery

#### Scenario: Second data change computes the cycle
- **WHEN** a second hash change is detected with `prevChangeUtc = 07:00` and `lastChangeUtc = 10:00`
- **THEN** `cycle = 3h` is computed and the phase transitions to Steady

### Requirement: Steady phase schedules polls based on the learned cycle
When a cycle is known (phase = Steady), the SchedulerActor SHALL schedule the next poll at `lastChangeUtc + cycle + 1 minute`. If the expected data change does not occur (hash unchanged), the actor SHALL retry with exponential backoff (1 min, 2 min, 4 min, 8 min, capped at 15 min). After 5 consecutive misses, the actor SHALL fall back to Discovery phase.

#### Scenario: Steady schedules at learned cycle plus buffer
- **WHEN** `lastChangeUtc = 09:30`, `cycle = 3h`
- **THEN** the next poll is scheduled at 12:31

#### Scenario: Missed change triggers retry backoff
- **WHEN** the poll at 12:31 finds unchanged data (miss 1)
- **THEN** the next retry is at 12:32 (1 min backoff)

#### Scenario: Second miss doubles the backoff
- **WHEN** the retry at 12:32 also finds unchanged data (miss 2)
- **THEN** the next retry is at 12:34 (2 min backoff)

#### Scenario: Fifth consecutive miss falls back to Discovery
- **WHEN** 5 consecutive polls find unchanged data
- **THEN** the phase resets to Discovery and polling resumes at 20-minute intervals

### Requirement: Hash results from the pipeline update the schedule
The SchedulerActor SHALL handle `HashResult(Location, ModelId, Hash)` messages
from the pipeline's Ask flow. On receipt, the actor SHALL compare the hash with
`lastHash`. If changed: persist a `DataChanged` event, update
`lastChangeUtc`/`prevChangeUtc`, reset `missCount`, and schedule the next poll.
If unchanged: increment `missCount` and schedule retry. The actor SHALL respond
with `Ack` after processing.

Additionally, the SchedulerActor SHALL consume `FetchOutcome.Failure` from its
BroadcastHub consumer and route to reason-specific retry logic (see
failure-routing spec).

#### Scenario: Changed hash triggers persist and reschedule
- **WHEN** a `HashResult` arrives with a hash different from `lastHash`
- **THEN** a `DataChanged` event is persisted, the state is updated, and `Ack` is returned

#### Scenario: Unchanged hash increments miss count
- **WHEN** a `HashResult` arrives with a hash equal to `lastHash`
- **THEN** `missCount` is incremented, next retry is scheduled, and `Ack` is returned

#### Scenario: Failure from BroadcastHub triggers reason-based retry
- **WHEN** a `FetchOutcome.Failure(Transport)` is consumed from the BroadcastHub
- **THEN** the scheduler increments missCount and schedules a backoff retry

### Requirement: SchedulerActor iterates resolved models per location
The `SchedulerActor` SHALL resolve effective models per location using
`LocationOptions.ResolveModels(globalModels)` and iterate over the
resolved list. It SHALL NOT iterate the global `Models` list directly.

#### Scenario: Location with extra models gets polled for all
- **WHEN** global Models is `["icon_global"]` and location "berlin" has
  Models `["icon_d2"]`
- **THEN** the scheduler SHALL create poll states for both
  `("berlin", "icon_global")` and `("berlin", "icon_d2")`

#### Scenario: Location without extra models gets global only
- **WHEN** global Models is `["icon_global"]` and location "amsterdam"
  has no Models
- **THEN** the scheduler SHALL create a poll state only for
  `("amsterdam", "icon_global")`

### Requirement: State is persisted and recovered via Akka.Persistence
The SchedulerActor SHALL persist `DataChanged` events to a SQLite journal via Akka.Persistence. On recovery, the actor SHALL rebuild all `ModelPollState` entries from the event stream. If a recovered `nextPollUtc` is in the past, the actor SHALL poll immediately. If a cycle is known from recovery, the actor SHALL enter Steady phase directly without re-discovery.

The SchedulerActor SHALL save a snapshot of its full `_states` dictionary every 50 persisted events. The snapshot SHALL be a dedicated `SchedulerSnapshotDto` containing all `ModelPollState` entries. On `SaveSnapshotSuccess`, the actor SHALL delete all journal entries up to the snapshot's sequence number and delete all previous snapshots. On `SaveSnapshotFailure`, the actor SHALL log a warning. Recovery SHALL prefer the latest snapshot and replay only events after it.

#### Scenario: Recovery skips discovery for known cycles
- **WHEN** SchedulerActor recovers with persisted events containing learned cycles
- **THEN** it enters Steady phase for those models without re-discovery

#### Scenario: Past nextPollUtc triggers immediate poll
- **WHEN** SchedulerActor recovers and a model's nextPollUtc is in the past
- **THEN** it polls that model immediately

#### Scenario: Recovery with no prior events starts in Discovery
- **WHEN** SchedulerActor recovers with no persisted events and no snapshot
- **THEN** all models start in Discovery phase

#### Scenario: Snapshot saved after 50 persisted events
- **WHEN** 50 DataChanged events have been persisted since the last snapshot
- **THEN** the actor saves a snapshot of the full _states dictionary

#### Scenario: Journal and old snapshots cleaned after snapshot success
- **WHEN** a snapshot save succeeds
- **THEN** journal entries up to the snapshot sequence number are deleted
- **THEN** all previous snapshots are deleted

#### Scenario: Snapshot failure is logged without crash
- **WHEN** a snapshot save fails
- **THEN** the actor logs a warning and continues operating

#### Scenario: Recovery from snapshot plus events
- **WHEN** SchedulerActor recovers with both a snapshot and subsequent events
- **THEN** the snapshot is restored first, then remaining events are replayed

### Requirement: OfferAsync result is handled in the Ready state
The SchedulerActor SHALL handle the result of `OfferAsync` in the Ready state by piping it to Self. If the offer fails (queue completed or dropped), the actor SHALL log a warning and re-schedule the poll for that (location, model) pair using the standard `ScheduleNext` logic. The actor SHALL NOT silently discard a failed offer.

#### Scenario: Successful offer in Ready state
- **WHEN** a ScheduledPoll fires in the Ready state and OfferAsync succeeds
- **THEN** the target is enqueued and no additional action is taken

#### Scenario: Failed offer in Ready state triggers re-schedule
- **WHEN** a ScheduledPoll fires in the Ready state and OfferAsync fails
- **THEN** the actor logs a warning with the location, model, and error
- **THEN** the poll is re-scheduled via ScheduleNext

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

### Requirement: Transient failures use an isolated counter and preserve learned cycles
`ModelPollState` SHALL track transient failures with a dedicated `TransientFailureCount` that is independent of `MissCount`. `WithTransientFailure` SHALL only increment `TransientFailureCount` and SHALL never modify `Phase`, `Cycle`, or `MissCount`. A learned cycle SHALL survive any number of consecutive transient failures.

After `MaxTransientBeforeThrottle` (5) consecutive transient failures, `WithTransientFailure` SHALL cap the retry delay at `discoveryInterval` instead of `MaxRetryBackoff`.

`WithDataChange` SHALL reset both `MissCount` and `TransientFailureCount` to 0.

#### Scenario: Transient failures do not poison MissCount
- **WHEN** 20 consecutive transient failures occur in Steady phase
- **AND** the network recovers and the first successful fetch returns unchanged data
- **THEN** `WithMiss` SHALL see `MissCount = 0` and produce `MissCount = 1` with a 1-minute backoff

#### Scenario: Learned cycle survives a network outage
- **WHEN** a model is in Steady phase with `Cycle = 8h`
- **AND** 20 consecutive transient failures occur
- **THEN** `Phase` remains Steady and `Cycle` remains 8h

#### Scenario: Transient failure backoff escalates then caps at discoveryInterval
- **WHEN** consecutive transient failures occur
- **THEN** the retry delays are 1m, 2m, 4m, 8m (exponential backoff)
- **AND** from the 5th failure onward, the delay caps at `discoveryInterval` (20m)

#### Scenario: Data change resets transient failure count
- **WHEN** a data change is detected after a series of transient failures
- **THEN** both `MissCount` and `TransientFailureCount` are reset to 0
