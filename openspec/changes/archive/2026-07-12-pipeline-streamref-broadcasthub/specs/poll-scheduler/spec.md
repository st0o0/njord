## MODIFIED Requirements

### Requirement: The SchedulerActor obtains a SinkRef from the PipelineActor
The SchedulerActor SHALL request a `SinkRef<WeightedTarget>` from the PipelineActor during startup via `RequestPipelineSink`. The actor SHALL stash all timer messages until the SinkRef is received. Only after obtaining the SinkRef SHALL the actor materialize a local `Source.Queue<WeightedTarget>` connected to the SinkRef and start scheduling timers.

#### Scenario: Scheduler waits for pipeline readiness
- **WHEN** the SchedulerActor starts and the PipelineActor has not yet responded
- **THEN** the actor stashes timer messages and does not schedule any polls

#### Scenario: SinkRef received triggers local queue materialization and scheduling
- **WHEN** the PipelineActor responds with a `PipelineSinkResponse` containing a `SinkRef<WeightedTarget>`
- **THEN** the SchedulerActor materializes a local `Source.Queue<WeightedTarget>` connected to `sinkRef.Sink`, schedules `ScheduleOnce` for every (location, model) pair, and unstashes pending messages

### Requirement: The SchedulerActor manages per-model poll timing
A `SchedulerActor` (ReceivePersistentActor) SHALL maintain a `ModelPollState` per configured (location, model) pair. Each state SHALL track: `lastHash` (int?), `lastChangeUtc` (DateTimeOffset?), `prevChangeUtc` (DateTimeOffset?), `nextPollUtc` (DateTimeOffset), `missCount` (int), and `phase` (Discovery or Steady). The actor SHALL use `ScheduleTellOnce` to fire polls at each model's individually calculated time.

#### Scenario: Each model gets its own timer
- **WHEN** 1 location and 8 models are configured
- **THEN** the SchedulerActor maintains 8 independent `ModelPollState` entries, each with its own `ScheduleOnce` timer

#### Scenario: Timer fires offer a target into the local queue
- **WHEN** a `ScheduleOnce` timer fires for (lucerne, icon_d2)
- **THEN** the actor offers a `WeightedTarget(lucerne, icon_d2)` into its own local `Source.Queue`, which drains through the SinkRef into the PipelineActor's MergeHub

### Requirement: Manual refresh commands bypass the schedule
The SchedulerActor SHALL accept `RefreshModel(location, model)` and `RefreshLocation(location)` commands. These SHALL immediately offer the corresponding `WeightedTarget`(s) into the local `Source.Queue` without affecting the scheduled timers. The schedule SHALL NOT be reset by a manual refresh.

#### Scenario: RefreshModel bypasses schedule
- **WHEN** a `RefreshModel("lucerne", "icon_d2")` is received and the next scheduled poll is in 2 hours
- **THEN** a target is offered into the local queue immediately and the 2-hour timer remains active

#### Scenario: RefreshLocation fans out to all models
- **WHEN** a `RefreshLocation("lucerne")` is received with 8 models configured
- **THEN** 8 targets are offered into the local queue

## REMOVED Requirements

### Requirement: The SchedulerActor obtains a StreamRef from the PipelineActor
**Reason:** Renamed and reworked — the actor now obtains a `SinkRef<WeightedTarget>` (not a raw queue handle called "StreamRef") and materializes a local Source.Queue connected to it.
**Migration:** Replace `RequestPipelineQueue`/`PipelineQueueResponse` with `RequestPipelineSink`/`PipelineSinkResponse`. Replace direct `_queue.OfferAsync` on a foreign handle with offers on the actor's own local queue.
