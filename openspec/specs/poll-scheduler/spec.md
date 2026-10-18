# poll-scheduler Specification

## Purpose

Adaptive per-target poll scheduling: a `PollManager` cluster singleton starts one persistent `PollWorker` shard entity per (location, model) target; each worker learns its model's update cycle from data hash changes, schedules its polls with a timer, keeps at most one fetch in flight, persists its full poll state across restarts via Akka.Persistence and reports every outcome to the manager.
## Requirements
### Requirement: Discovery phase polls at a fixed interval until the cycle is learned
When no cycle is known for a (location, model) target (phase = Discovery), the target's poll worker SHALL poll at the discovery interval it was started with (`PollingOptions.DiscoveryInterval`, default 20 minutes). After two consecutive data changes are detected (two different `lastChangeUtc` values), the worker SHALL compute `cycle = lastChangeUtc - prevChangeUtc` and transition to Steady phase.

#### Scenario: Discovery polls every 20 minutes
- **WHEN** a target has no known cycle (phase = Discovery)
- **THEN** the next poll is scheduled 20 minutes from now

#### Scenario: First data change is recorded but stays in Discovery
- **WHEN** the first hash change is detected for a target
- **THEN** `lastChangeUtc` is set, `prevChangeUtc` remains null, and the phase stays Discovery

#### Scenario: Second data change computes the cycle
- **WHEN** a second hash change is detected with `prevChangeUtc = 07:00` and `lastChangeUtc = 10:00`
- **THEN** `cycle = 3h` is computed and the phase transitions to Steady

### Requirement: Steady phase schedules polls based on the learned cycle
When a cycle is known (phase = Steady), the poll worker SHALL schedule the next poll at `lastChangeUtc + cycle + 1 minute`. If the expected data change does not occur (hash unchanged), the worker SHALL retry with exponential backoff (1 min, 2 min, 4 min, 8 min, capped at 15 min). After 5 consecutive misses, the worker SHALL fall back to Discovery phase.

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
Each poll worker SHALL persist every change of its target's poll state as an event in the configured Akka.Persistence SQL journal (SQLite by default) under its own persistence id `poll-worker-{location}|{model}`: the start of polling with its discovery interval, the stop of polling, a changed forecast (with its hash), an unchanged forecast, a failed or rejected fetch (with its minimum delay) and a deferred fetch. On recovery the worker SHALL rebuild exactly the state it had before (target, phase, learned cycle, next poll time, last hash, miss and transient-failure counts, discovery interval, started or stopped). If the recovered next poll time is in the past, the worker SHALL poll immediately; otherwise it SHALL wait until that time. If a cycle is known from recovery, the worker SHALL stay in Steady phase without re-discovery. A recovered worker that was started SHALL resume polling on its own, without waiting for a new start.

The worker SHALL save a snapshot of its full state every 50 persisted events (`LastSequenceNr % 50 == 0`). On `SaveSnapshotSuccess`, the worker SHALL delete all journal entries up to the snapshot's sequence number and all previous snapshots. On `SaveSnapshotFailure` or a failed deletion, the worker SHALL log a warning. Recovery SHALL prefer the latest snapshot and replay only events after it.

The journal of the removed single scheduler (persistence id `scheduler`) SHALL NOT be read.

#### Scenario: Recovery skips discovery for known cycles
- **WHEN** a poll worker recovers with persisted events containing a learned cycle
- **THEN** it is in Steady phase with the same cycle, without re-discovery

#### Scenario: Past nextPollUtc triggers immediate poll
- **WHEN** a poll worker recovers and its next poll time is in the past
- **THEN** it polls its target immediately

#### Scenario: Recovery with no prior events starts in Discovery
- **WHEN** a poll worker is started with no persisted events and no snapshot
- **THEN** its target starts in Discovery phase and is polled immediately

#### Scenario: Snapshot saved after 50 persisted events
- **WHEN** a poll worker has persisted its 50th event since the last snapshot
- **THEN** it saves a snapshot of its full state

#### Scenario: Journal and old snapshots cleaned after snapshot success
- **WHEN** a snapshot save succeeds
- **THEN** journal entries up to the snapshot sequence number are deleted
- **THEN** all previous snapshots are deleted

#### Scenario: Snapshot failure is logged without crash
- **WHEN** a snapshot save fails
- **THEN** the worker logs a warning and continues operating

#### Scenario: Recovery from snapshot plus events
- **WHEN** a poll worker recovers with both a snapshot and subsequent events
- **THEN** the snapshot is restored first, then remaining events are replayed

#### Scenario: Restarted worker keeps its next poll and phase
- **WHEN** a started poll worker in Steady phase with a next poll in the future is stopped and started again
- **THEN** it reports the same phase, cycle and next poll time as before and polls at that time

### Requirement: Transient failures use an isolated counter and preserve learned cycles
The poll state SHALL track transient failures with a dedicated `TransientFailureCount` that is independent of `MissCount`. A transient failure SHALL only increment `TransientFailureCount` and SHALL never modify `Phase`, `Cycle` or `MissCount`. A learned cycle SHALL survive any number of consecutive transient failures.

From the 5th consecutive transient failure on (`MaxTransientBeforeThrottle`), the retry delay SHALL be the discovery interval instead of the exponential backoff.

A data change SHALL reset both `MissCount` and `TransientFailureCount` to 0.

#### Scenario: Transient failures do not poison MissCount
- **WHEN** 20 consecutive transient failures occur in Steady phase
- **AND** the network recovers and the first successful fetch returns unchanged data
- **THEN** the miss SHALL see `MissCount = 0` and produce `MissCount = 1` with a 1-minute backoff

#### Scenario: Learned cycle survives a network outage
- **WHEN** a target is in Steady phase with `Cycle = 8h`
- **AND** 20 consecutive transient failures occur
- **THEN** `Phase` remains Steady and `Cycle` remains 8h

#### Scenario: Transient failure backoff escalates then caps at discoveryInterval
- **WHEN** consecutive transient failures occur
- **THEN** the retry delays are 1m, 2m, 4m, 8m (exponential backoff)
- **AND** from the 5th failure onward, the delay caps at `discoveryInterval` (20m)

#### Scenario: Data change resets transient failure count
- **WHEN** a data change is detected after a series of transient failures
- **THEN** both `MissCount` and `TransientFailureCount` are reset to 0

### Requirement: Fetch feedback from the gateway updates the schedule
The poll worker SHALL handle `FetchForecastCompleted(Location, ModelId, Hash)` by comparing the hash with its `LastHash`. If it changed, the worker SHALL persist a changed-forecast event, update `LastChangeUtc`/`PrevChangeUtc`, reset `MissCount` and `TransientFailureCount` and schedule the next poll. If it is unchanged, the worker SHALL persist an unchanged-forecast event, increment `MissCount` and schedule a retry. It SHALL NOT reply to the gateway. `FetchForecastFailed` and `FetchForecastRejected` SHALL be routed to reason-specific retry logic (see `failure-routing`). The worker SHALL report every outcome (changed, unchanged or failed; a rejection counts as failed) with its new poll state to the poll manager, which records a changed or unchanged outcome as a successful poll in `NjordHealthMonitor` and counts each outcome in the location's poll cycle.

#### Scenario: Changed hash triggers persist and reschedule
- **WHEN** `FetchForecastCompleted` arrives with a hash different from `LastHash`
- **THEN** a changed-forecast event is persisted, the state is updated, the next poll is scheduled and the manager receives a changed report

#### Scenario: Unchanged hash increments miss count
- **WHEN** `FetchForecastCompleted` arrives with a hash equal to `LastHash`
- **THEN** `MissCount` is incremented, a retry is scheduled and the manager receives an unchanged report

#### Scenario: Fetch failure triggers reason-based retry
- **WHEN** `FetchForecastFailed` with reason `Transport` arrives
- **THEN** the worker records a transient failure, schedules a backoff retry and the manager receives a failed report

#### Scenario: A rejection counts as a failed cycle outcome
- **WHEN** every model of a location has reported and one of the reports came from a `FetchForecastRejected`
- **THEN** the location's completed poll cycle reports that model as failed

#### Scenario: A successful poll updates the poll health
- **WHEN** the manager receives a changed or unchanged report
- **THEN** the poll health's last successful poll is the manager's current time

### Requirement: Each configured target is polled by its own poll worker
Every configured (location, model) target SHALL be polled by its own poll worker, a persistent cluster-sharding entity in the poll shard region (marker `IPollRegion`) whose entity id is `"{location}|{model}"` in the configured spelling. Every message for a worker SHALL carry the target id (`IWithTargetId`) and be routed by the shared shard message extractor. Each worker SHALL keep its own timer, replacing a pending timer whenever it reschedules; a next poll time that is not in the future SHALL poll at once. A worker that starts with no persisted state SHALL poll immediately - there is no stagger delay; the gateway's throttle is the only rate-limiting gate. Workers SHALL NOT be passivated when idle and the region SHALL NOT remember entities: the poll manager starts the configured set.

#### Scenario: Each model gets its own timer
- **WHEN** 1 location and 8 models are configured
- **THEN** 8 poll workers run, each with its own poll state and timer

#### Scenario: Initial polls are sent without stagger delay
- **WHEN** 27 (location, model) targets are started with no prior persisted state
- **THEN** all 27 targets are polled immediately and the gateway throttle shapes the requests

#### Scenario: Recovered state preserves existing NextPollUtc
- **WHEN** a worker recovers with persisted state for its target
- **THEN** the recovered next poll time is used as-is (no stagger applied)

#### Scenario: Idle workers stay alive
- **WHEN** a worker waits hours for its next poll
- **THEN** it is not passivated and polls at its next poll time

### Requirement: Each poll worker asks the gateway for its own target
When its timer fires, a started poll worker SHALL resolve the gateway (`IForecastGateway`) from the actor registry and ask it with `FetchForecast(location, model)` of its target and the fetch reply timeout (120 s by default, covering the gateway's queue, throttle admission and HTTP timeout), piping the reply to itself. An Ask that fails or times out, and a gateway that is not registered, SHALL be handled as `FetchForecastFailed(location, model, Transport, detail)`. Every poll therefore ends in exactly one handled reply, and a stopped or replaced gateway never stops polling: the next poll uses whatever gateway is registered then. The worker SHALL NOT reference `IForecastPipeline`.

#### Scenario: Worker sends FetchForecast to the gateway
- **WHEN** the timer of the worker for (lucerne, icon_d2) fires
- **THEN** the worker asks the gateway with `FetchForecast(lucerne, "icon_d2")` and handles the piped reply

#### Scenario: Gateway that never answers
- **WHEN** the gateway does not answer a `FetchForecast` within the fetch reply timeout
- **THEN** the worker handles it as a `Transport` failure and schedules a backoff retry

#### Scenario: A replaced gateway is used by the next poll
- **WHEN** the registered gateway terminates and a new gateway is registered
- **THEN** the worker's next poll asks the new gateway

#### Scenario: A terminated gateway does not cause a poll loop
- **WHEN** the gateway terminates and no replacement is registered
- **THEN** each worker records one transient failure per poll and waits for its backoff, without a tight retry loop

### Requirement: A poll worker has at most one fetch in flight
A poll worker SHALL have at most one `FetchForecast` request in flight. A timer or trigger that fires while a request is in flight SHALL NOT send another request; the outcome of the request in flight reschedules the worker. A reply that does not belong to the current request SHALL be ignored.

#### Scenario: Trigger during a request in flight
- **WHEN** a worker has asked the gateway and not yet received a reply, and a trigger for its target arrives
- **THEN** no second `FetchForecast` is sent

#### Scenario: Next poll after the outcome
- **WHEN** the reply of the request in flight arrives
- **THEN** the worker schedules its next poll from that outcome and may send a new request then

### Requirement: Poll workers report to the poll manager
A poll worker SHALL report to the poll manager (`IPollManager`, resolved from the actor registry) after every start of polling, after recovering in the started state, and after every fetch outcome, with its target, the outcome (planned, changed, unchanged or failed) and its current poll state (phase, next poll time, last change, miss count, cycle). A worker whose manager is not registered SHALL keep polling.

#### Scenario: Start is reported
- **WHEN** a worker is started for (lucerne, icon_d2)
- **THEN** the manager receives a planned report for (lucerne, icon_d2) with the worker's current poll state

#### Scenario: Reply spelled differently is reported under the configured target
- **WHEN** the worker of (lucerne, icon_d2) receives `FetchForecastCompleted(LUCERNE, "ICON_D2", hash)`
- **THEN** its state is updated and its report names (lucerne, icon_d2)

### Requirement: The poll manager starts a worker for every resolved model of every location
The poll manager (a non-persistent cluster singleton behind a backoff-on-failure supervisor, marker `IPollManager`) SHALL derive the target set from the startup configuration: per location, the case-insensitive union of the global `Models` list and the location's own `Models`, in configuration order. On start it SHALL send `StartPolling(target, discovery interval)` for every target through the poll shard region. Locations and models are restart-required; the target set does not change while the manager runs.

#### Scenario: Location with extra models gets polled for all
- **WHEN** global Models is `["icon_global"]` and location "berlin" has Models `["icon_d2"]`
- **THEN** the manager SHALL start workers for both `("berlin", "icon_global")` and `("berlin", "icon_d2")`

#### Scenario: Location without extra models gets global only
- **WHEN** global Models is `["icon_global"]` and location "amsterdam" has no Models
- **THEN** the manager SHALL start a worker only for `("amsterdam", "icon_global")`

#### Scenario: A restarted manager restarts the set
- **WHEN** the poll manager restarts
- **THEN** it sends `StartPolling` to every configured target again, and workers that were already polling keep their state

### Requirement: The poll manager reconciles the running workers
The poll manager SHALL:
- send `StartPolling` with the new discovery interval to every target when `PollingChanged` carries a discovery interval different from the current one; a worker in Discovery phase without transient failures whose next poll is still ahead SHALL move its next poll to (planned time - old interval + new interval), never earlier than now, while steady and backoff delays stay as they are;
- after start, ask the poll shard region for its running entities and send `StopPolling` to every worker whose target is not configured; a stopped worker persists that it stopped, stops polling and stops, and its journal is kept;
- watch every worker that reports, and when a worker of a configured target terminates, send it `StartPolling` again after a jittered backoff (1 s up to 30 s), so a worker that stopped on a persistence failure resumes; a worker that stops while the node shuts down (the shard region hands off every entity) is not restarted.

#### Scenario: A shorter discovery interval replans the pending poll
- **WHEN** a target in Discovery waits for its next poll and `PollingChanged` shortens the discovery interval to 100 ms
- **THEN** the target is polled again within moments

#### Scenario: A longer discovery interval moves the next poll out
- **WHEN** a target in Discovery waits for a poll planned one discovery interval (1 h) from now and `PollingChanged` sets 2 h
- **THEN** its next poll is two hours from now

#### Scenario: Without a change the next discovery poll waits for the configured interval
- **WHEN** a target in Discovery has been polled and no `PollingChanged` arrives
- **THEN** its next poll is one discovery interval after the poll

#### Scenario: Removed target is stopped
- **WHEN** the shard region runs a worker for (atlantis, icon_d2) that is not configured
- **THEN** the manager sends it `StopPolling` and the worker stops polling

#### Scenario: Terminated worker is restarted
- **WHEN** the worker of a configured target terminates
- **THEN** the manager sends `StartPolling` for that target again and polling resumes from the persisted state

#### Scenario: Workers stopped by a shutdown are not restarted
- **WHEN** the node shuts down and the shard region stops every worker
- **THEN** the manager logs each stop at Debug and sends no `StartPolling`

### Requirement: The poll manager forwards TriggerImmediatePoll to the workers
The poll manager SHALL handle `TriggerImmediatePoll(LocationId? Location, string Model)` by sending `TriggerTargetPoll` to the worker of each matching configured target and replying `TriggerImmediatePollCompleted(Count, Targets)`, where each target is `"{location}/{model}"` in the configured spelling. Location and model SHALL match case-insensitively. A null `Location` or an empty `Model` SHALL expand to every configured location or model; an unknown location SHALL yield zero targets. A worker that receives a trigger while it is started and has no request in flight SHALL poll immediately. The reply SHALL NOT wait for the fetches. When the shard region is not available yet the manager SHALL reply `TriggerImmediatePollFailed`.

#### Scenario: Immediate poll for one pair
- **WHEN** the manager receives `TriggerImmediatePoll(home, "icon_d2")`
- **THEN** the worker of (home, icon_d2) receives a trigger and the manager replies `TriggerImmediatePollCompleted(1, ["home/icon_d2"])`

#### Scenario: Empty filters expand to all pairs
- **WHEN** the manager receives `TriggerImmediatePoll(null, "")` with 2 locations of 3 models each
- **THEN** it triggers 6 workers and replies with a count of 6

#### Scenario: Unknown location triggers nothing
- **WHEN** the manager receives `TriggerImmediatePoll(nonexistent, "")`
- **THEN** it replies `TriggerImmediatePollCompleted(0, [])`

#### Scenario: Trigger reaches the gateway
- **WHEN** the manager receives `TriggerImmediatePoll(lucerne, "icon_d2")` and the worker has no request in flight
- **THEN** the gateway receives `FetchForecast(lucerne, "icon_d2")`

### Requirement: The poll manager answers immediately in every state
The poll manager SHALL answer `QueryPollStates` and `TriggerImmediatePoll` without stashing or waiting for a worker, including before the poll shard region is resolved and before the first report. Before a target has reported, its entry SHALL be its initial state (Discovery, next poll = manager start time, no last change, miss count 0, no cycle). A query SHALL NOT persist anything, send anything to a worker or schedule a timer.

#### Scenario: Query before the first report
- **WHEN** the manager has just started and no worker has reported yet
- **THEN** `QueryPollStates` is answered with one Discovery entry per configured target

#### Scenario: Query is read-only
- **WHEN** `QueryPollStates` is received
- **THEN** no message is sent to the shard region and no timer is scheduled

