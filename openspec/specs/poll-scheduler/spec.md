# poll-scheduler Specification

## Purpose

Adaptive per-model poll scheduling: a persistent actor that learns each weather model's update cycle from data hash changes, schedules polls via ScheduleOnce timers, and persists learned rhythms across restarts via Akka.Persistence + SQLite.

## Requirements

### Requirement: The SchedulerActor manages per-model poll timing
A `SchedulerActor` (ReceivePersistentActor) SHALL maintain a `ModelPollState` per configured (location, model) pair. Each state SHALL track: `lastHash` (int?), `lastChangeUtc` (DateTimeOffset?), `prevChangeUtc` (DateTimeOffset?), `nextPollUtc` (DateTimeOffset), `missCount` (int), and `phase` (Discovery or Steady). The actor SHALL use `ScheduleTellOnce` to fire polls at each model's individually calculated time.

#### Scenario: Each model gets its own timer
- **WHEN** 1 location and 8 models are configured
- **THEN** the SchedulerActor maintains 8 independent `ModelPollState` entries, each with its own `ScheduleOnce` timer

#### Scenario: Timer fires push a target into the pipeline
- **WHEN** a `ScheduleOnce` timer fires for (lucerne, icon_d2)
- **THEN** the actor offers a `WeightedTarget(lucerne, icon_d2)` into the Source.Queue obtained from the PipelineActor

### Requirement: The SchedulerActor obtains a StreamRef from the PipelineActor
The SchedulerActor SHALL request a StreamRef (Source.Queue handle) from the PipelineActor during startup. The actor SHALL stash all timer messages until the StreamRef is received. Only after obtaining the StreamRef SHALL timers be scheduled.

#### Scenario: Scheduler waits for pipeline readiness
- **WHEN** the SchedulerActor starts and the PipelineActor has not yet responded
- **THEN** the actor stashes timer messages and does not schedule any polls

#### Scenario: StreamRef received triggers initial scheduling
- **WHEN** the PipelineActor responds with a StreamRef
- **THEN** the SchedulerActor schedules `ScheduleOnce` for every (location, model) pair and unstashes pending messages

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
The SchedulerActor SHALL handle `HashResult(LocationModelKey, int Hash)` messages from the pipeline's Ask flow. On receipt, the actor SHALL compare the hash with `lastHash`. If changed: persist a `DataChanged` event, update `lastChangeUtc`/`prevChangeUtc`, reset `missCount`, and schedule the next poll. If unchanged: increment `missCount` and schedule retry. The actor SHALL respond with `Ack` after processing.

#### Scenario: Changed hash triggers persist and reschedule
- **WHEN** a `HashResult` arrives with a hash different from `lastHash`
- **THEN** a `DataChanged` event is persisted, the state is updated, and `Ack` is returned

#### Scenario: Unchanged hash increments miss count
- **WHEN** a `HashResult` arrives with a hash equal to `lastHash`
- **THEN** `missCount` is incremented, next retry is scheduled, and `Ack` is returned

### Requirement: Manual refresh commands bypass the schedule
The SchedulerActor SHALL accept `RefreshModel(location, model)` and `RefreshLocation(location)` commands. These SHALL immediately offer the corresponding `WeightedTarget`(s) into the Source.Queue without affecting the scheduled timers. The schedule SHALL NOT be reset by a manual refresh.

#### Scenario: RefreshModel bypasses schedule
- **WHEN** a `RefreshModel("lucerne", "icon_d2")` is received and the next scheduled poll is in 2 hours
- **THEN** a target is offered immediately and the 2-hour timer remains active

#### Scenario: RefreshLocation fans out to all models
- **WHEN** a `RefreshLocation("lucerne")` is received with 8 models configured
- **THEN** 8 targets are offered immediately into the queue

### Requirement: State is persisted and recovered via Akka.Persistence
The SchedulerActor SHALL persist `DataChanged` events to a SQLite journal via Akka.Persistence. On recovery, the actor SHALL rebuild all `ModelPollState` entries from the event stream. If a recovered `nextPollUtc` is in the past, the actor SHALL poll immediately. If a cycle is known from recovery, the actor SHALL enter Steady phase directly without re-discovery.

#### Scenario: Recovery skips discovery for known cycles
- **WHEN** the actor recovers with a persisted cycle of 3h and `lastChangeUtc = 09:30`
- **THEN** the actor enters Steady phase and schedules the next poll at `09:30 + 3h + 1min = 12:31`

#### Scenario: Past nextPollUtc triggers immediate poll
- **WHEN** the actor recovers and `nextPollUtc = 08:00` but the current time is 10:00
- **THEN** the actor polls immediately

#### Scenario: Recovery with no prior events starts in Discovery
- **WHEN** the actor recovers with an empty event journal
- **THEN** all models start in Discovery phase at 20-minute intervals
