## MODIFIED Requirements

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
