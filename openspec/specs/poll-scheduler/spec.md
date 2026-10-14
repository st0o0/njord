# poll-scheduler Specification

## Purpose

Adaptive per-model poll scheduling: a persistent actor that learns each weather model's update cycle from data hash changes, schedules polls via ScheduleOnce timers, and persists learned rhythms across restarts via Akka.Persistence + SQLite.
## Requirements
### Requirement: Discovery phase polls at a fixed interval until the cycle is learned
When no cycle is known for a (location, model) pair (phase = Discovery), the PollScheduler SHALL poll at the discovery interval (`PollingOptions.DiscoveryInterval`, default 20 minutes). After two consecutive data changes are detected (two different `lastChangeUtc` values), the actor SHALL compute `cycle = lastChangeUtc - prevChangeUtc` and transition to Steady phase.

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
When a cycle is known (phase = Steady), the PollScheduler SHALL schedule the next poll at `lastChangeUtc + cycle + 1 minute`. If the expected data change does not occur (hash unchanged), the actor SHALL retry with exponential backoff (1 min, 2 min, 4 min, 8 min, capped at 15 min). After 5 consecutive misses, the actor SHALL fall back to Discovery phase.

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

### Requirement: State is persisted and recovered via Akka.Persistence
The PollScheduler SHALL persist `PersistedDataChanged` events to the configured Akka.Persistence SQL journal (SQLite by default). On recovery, the actor SHALL rebuild all `ModelPollState` entries from the event stream. If a recovered `nextPollUtc` is in the past, the actor SHALL poll immediately. If a cycle is known from recovery, the actor SHALL enter Steady phase directly without re-discovery.

The PollScheduler SHALL save a snapshot of its full `PollSchedulerState.States` dictionary every 50 persisted events. The snapshot SHALL be a dedicated `PersistedPollSchedulerState` record containing all `ModelPollState` entries. On `SaveSnapshotSuccess`, the actor SHALL delete all journal entries up to the snapshot's sequence number and delete all previous snapshots. On `SaveSnapshotFailure`, the actor SHALL log a warning. Recovery SHALL prefer the latest snapshot and replay only events after it.

#### Scenario: Recovery skips discovery for known cycles
- **WHEN** PollScheduler recovers with persisted events containing learned cycles
- **THEN** it enters Steady phase for those models without re-discovery

#### Scenario: Past nextPollUtc triggers immediate poll
- **WHEN** PollScheduler recovers and a model's nextPollUtc is in the past
- **THEN** it polls that model immediately

#### Scenario: Recovery with no prior events starts in Discovery
- **WHEN** PollScheduler recovers with no persisted events and no snapshot
- **THEN** all models start in Discovery phase

#### Scenario: Snapshot saved after 50 persisted events
- **WHEN** 50 DataChanged events have been persisted since the last snapshot
- **THEN** the actor saves a snapshot of the full PollSchedulerState.States dictionary

#### Scenario: Journal and old snapshots cleaned after snapshot success
- **WHEN** a snapshot save succeeds
- **THEN** journal entries up to the snapshot sequence number are deleted
- **THEN** all previous snapshots are deleted

#### Scenario: Snapshot failure is logged without crash
- **WHEN** a snapshot save fails
- **THEN** the actor logs a warning and continues operating

#### Scenario: Recovery from snapshot plus events
- **WHEN** PollScheduler recovers with both a snapshot and subsequent events
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

### Requirement: PollScheduler sends FetchForecast to the gateway
`PollScheduler` (src/Njord.Pipeline/PollScheduler.cs, a `ReceivePersistentActor` with persistence id `scheduler`, marker `IPollScheduler`) SHALL resolve the gateway with `Context.GetActor<IForecastGateway>()` in its constructor and watch it. When a `ScheduledPoll(LocationId, ModelId)` fires it SHALL send `FetchForecast(location, modelId)` to the gateway via Tell. It SHALL NOT build a weighted target, own a queue or reference `IForecastPipeline`.

The actor SHALL have two behaviors: `Ready` (the initial behavior) and `WaitingForGateway`. On `Terminated` for the gateway it SHALL enter `WaitingForGateway` and re-resolve with `GetActorAsync<IForecastGateway>()`; a resolved ref equal to the terminated one SHALL be treated as dead and retried with jittered exponential backoff (1 s up to 30 s). While waiting, `ScheduledPoll` and every other message not explicitly handled SHALL be stashed and replayed on the transition back to `Ready`, after the poll states are re-initialized.

#### Scenario: Scheduler sends FetchForecast to the gateway
- **WHEN** a scheduled timer fires for (lucerne, icon_d2)
- **THEN** the actor sends `FetchForecast(lucerne, "icon_d2")` to the gateway via Tell

#### Scenario: Gateway terminated triggers re-resolve
- **WHEN** the gateway terminates
- **THEN** the actor enters `WaitingForGateway` and re-resolves with backoff

#### Scenario: ScheduledPoll stashed during WaitingForGateway
- **WHEN** a `ScheduledPoll` arrives while the actor is in `WaitingForGateway`
- **THEN** it is stashed and replayed on the transition to `Ready`

### Requirement: PollScheduler manages per-model poll timing
`PollScheduler` SHALL maintain a `ModelPollState` per configured (location, model) pair in an immutable `PollSchedulerState`. Each state SHALL track `LastHash` (int?), `LastChangeUtc`, `PrevChangeUtc`, `NextPollUtc`, `MissCount`, `Phase` (`Discovery` or `Steady`), `Cycle` and `TransientFailureCount`. The actor SHALL schedule each pair individually with a cancelable `ScheduleTellOnce` of `ScheduledPoll`, replacing any pending timer for that pair; a `NextPollUtc` that is not in the future SHALL be sent to itself immediately. On first initialization (no prior persisted state) all pairs SHALL start with `NextPollUtc = now` — there is no stagger delay; the gateway's throttle is the only rate-limiting gate.

#### Scenario: Each model gets its own timer
- **WHEN** 1 location and 8 models are configured
- **THEN** the actor maintains 8 independent `ModelPollState` entries, each with its own timer

#### Scenario: Initial polls are sent without stagger delay
- **WHEN** the actor initializes 27 (location, model) pairs with no prior persisted state
- **THEN** all 27 pairs are polled immediately and the gateway throttle shapes the requests

#### Scenario: Recovered state preserves existing NextPollUtc
- **WHEN** the actor recovers with persisted state for a model
- **THEN** the recovered `NextPollUtc` is used as-is (no stagger applied)

### Requirement: Fetch feedback from the gateway updates the schedule
`PollScheduler` SHALL handle `FetchForecastCompleted(Location, ModelId, Hash)` from the gateway by comparing the hash with `LastHash`. If it changed, the actor SHALL persist a `PersistedDataChanged` event, update `LastChangeUtc`/`PrevChangeUtc`, reset `MissCount` and `TransientFailureCount`, record the poll in `NjordHealthMonitor`, and schedule the next poll. If it is unchanged, the actor SHALL increment `MissCount` and schedule a retry. In both cases it SHALL reply `Ack` to the sender. `FetchForecastFailed` SHALL be routed to reason-specific retry logic (see `failure-routing`).

#### Scenario: Changed hash triggers persist and reschedule
- **WHEN** `FetchForecastCompleted` arrives with a hash different from `LastHash`
- **THEN** a `PersistedDataChanged` event is persisted, the state is updated, the next poll is scheduled and `Ack` is returned

#### Scenario: Unchanged hash increments miss count
- **WHEN** `FetchForecastCompleted` arrives with a hash equal to `LastHash`
- **THEN** `MissCount` is incremented, a retry is scheduled and `Ack` is returned

#### Scenario: Fetch failure triggers reason-based retry
- **WHEN** `FetchForecastFailed` with reason `Transport` arrives
- **THEN** the actor records a transient failure and schedules a backoff retry

### Requirement: PollScheduler iterates resolved models per location
The `PollScheduler` SHALL resolve effective models per location as the case-insensitive union of the global `Models` list and the location's own `Models` and iterate over the
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

### Requirement: QueryPollStates is answered in every behavior
`PollScheduler` SHALL handle `QueryPollStates` in both `Ready` and `WaitingForGateway` by replying immediately with `PollStatesResult` built from the current `PollSchedulerState.States`. The handler SHALL NOT stash, delay or drop the message.

#### Scenario: Query returns current state while waiting for the gateway
- **WHEN** `QueryPollStates` is received while the actor is in `WaitingForGateway`
- **THEN** the actor replies with `PollStatesResult` (which may be empty if no states are initialized yet)

#### Scenario: Query returns current state in Ready
- **WHEN** `QueryPollStates` is received in `Ready` with 6 model poll states
- **THEN** the actor replies with `PollStatesResult` containing 6 entries

#### Scenario: Query is read-only
- **WHEN** `QueryPollStates` is received in any behavior
- **THEN** no events are persisted and no timers are scheduled

### Requirement: PollScheduler accepts TriggerImmediatePoll
`PollScheduler` SHALL handle `TriggerImmediatePoll(LocationId? Location, string Model)` in `Ready` by sending itself a `ScheduledPoll` for each matching configured pair and replying `TriggerImmediatePollCompleted(Count, Targets)`, where each target is `"{location}/{model}"`. A null `Location` or an empty `Model` SHALL expand to every configured location or model; an unknown location SHALL yield zero targets. In `WaitingForGateway` the message SHALL be stashed until the actor is `Ready`.

#### Scenario: Immediate poll for one pair
- **WHEN** the actor receives `TriggerImmediatePoll(home, "icon_d2")`
- **THEN** it sends itself `ScheduledPoll(home, "icon_d2")` and replies `TriggerImmediatePollCompleted(1, ["home/icon_d2"])`

#### Scenario: Empty filters expand to all pairs
- **WHEN** the actor receives `TriggerImmediatePoll(null, "")` with 2 locations of 3 models each
- **THEN** it triggers 6 polls and replies with a count of 6

