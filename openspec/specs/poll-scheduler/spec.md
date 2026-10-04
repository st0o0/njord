# poll-scheduler Specification

## Purpose

Adaptive per-model poll scheduling: a persistent actor that learns each weather model's update cycle from data hash changes, schedules polls via ScheduleOnce timers, and persists learned rhythms across restarts via Akka.Persistence + SQLite.

## Requirements

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
The `SchedulerActor` SHALL resolve effective models per location as the case-insensitive union of the global `Models` list and the location's own `Models` and iterate over the
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

The SchedulerActor SHALL save a snapshot of its full `SchedulerState.States` dictionary every 50 persisted events. The snapshot SHALL be a dedicated `SchedulerSnapshotDto` containing all `ModelPollState` entries. On `SaveSnapshotSuccess`, the actor SHALL delete all journal entries up to the snapshot's sequence number and delete all previous snapshots. On `SaveSnapshotFailure`, the actor SHALL log a warning. Recovery SHALL prefer the latest snapshot and replay only events after it.

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
- **THEN** the actor saves a snapshot of the full SchedulerState.States dictionary

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

### Requirement: SchedulerActor accepts TriggerImmediatePoll message
The `SchedulerActor` SHALL handle a `TriggerImmediatePoll(string Location, string Model)` message in the `Ready` behavior by scheduling an immediate `ScheduledPoll` for each matching target and replying with `TriggerPollResult(Count, Targets)`, where each target is `"{location}/{model}"`. When `Location` or `Model` is empty, it SHALL expand to all matching configured pairs. In every other behavior the message SHALL be stashed until the actor is `Ready`.

#### Scenario: Immediate poll bypasses normal schedule
- **WHEN** `SchedulerActor` receives `TriggerImmediatePoll("home", "icon_d2")`
- **THEN** the actor SHALL schedule a `ScheduledPoll("home", "icon_d2")` immediately and reply with `TriggerPollResult(1, ["home/icon_d2"])`
- **AND** the normal schedule for that model SHALL NOT be disrupted
