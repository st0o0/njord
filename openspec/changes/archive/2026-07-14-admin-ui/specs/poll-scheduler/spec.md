# poll-scheduler Delta Specification (admin-ui)

## ADDED Requirements

### Requirement: SchedulerActor handles AddTargets command
The `SchedulerActor` SHALL handle an `AddTargets(IReadOnlyList<(Location, WeatherModel)> Targets)` message. For each target not already in `_states`, it SHALL create a new `ModelPollState` in Discovery phase, persist a `TargetsAdded` event, recompute `_weight`, and schedule the first poll via `ScheduleTellOnce`.

#### Scenario: New target added and persisted
- **WHEN** `AddTargets([("lucerne", "metno_nordic")])` is received and no state exists for that pair
- **THEN** a `ModelPollState` is created, `TargetsAdded` is persisted, and a Discovery-phase poll is scheduled

#### Scenario: Duplicate target ignored
- **WHEN** `AddTargets([("lucerne", "icon_d2")])` is received and the target already exists
- **THEN** no state change occurs and no event is persisted

### Requirement: SchedulerActor handles RemoveTargets command
The `SchedulerActor` SHALL handle a `RemoveTargets(IReadOnlyList<(Location, WeatherModel)> Targets)` message. For each target present in `_states`, it SHALL cancel its timer, remove from `_states`, and persist a `TargetsRemoved` event.

#### Scenario: Existing target removed
- **WHEN** `RemoveTargets([("lucerne", "icon_d2")])` is received
- **THEN** the state entry is removed, the timer is cancelled, and `TargetsRemoved` is persisted

#### Scenario: Non-existent target removal ignored
- **WHEN** `RemoveTargets([("lucerne", "nonexistent")])` is received
- **THEN** no state change occurs and no event is persisted

### Requirement: SchedulerActor recovers target lifecycle events
During recovery, the `SchedulerActor` SHALL process `TargetsAdded` events by creating `ModelPollState` entries and `TargetsRemoved` events by removing them. The net effect of all events SHALL produce the correct `_states` dictionary.

#### Scenario: Recovery rebuilds state from lifecycle events
- **WHEN** the journal contains `TargetsAdded(A)`, `TargetsAdded(B)`, `TargetsRemoved(A)`
- **THEN** after recovery, only target B exists in `_states`

## MODIFIED Requirements

### Requirement: The SchedulerActor manages per-model poll timing
A `SchedulerActor` (ReceivePersistentActor) SHALL maintain a `ModelPollState` per active (location, model) pair. Each state SHALL track: `lastHash` (int?), `lastChangeUtc` (DateTimeOffset?), `prevChangeUtc` (DateTimeOffset?), `nextPollUtc` (DateTimeOffset), `missCount` (int), and `phase` (Discovery or Steady). The actor SHALL use `ScheduleTellOnce` to fire polls at each model's individually calculated time. The set of active pairs SHALL be mutable at runtime via `AddTargets`/`RemoveTargets` commands, not only determined at initialization.

#### Scenario: Each model gets its own timer
- **WHEN** 1 location and 8 models are configured
- **THEN** the SchedulerActor maintains 8 independent `ModelPollState` entries, each with its own `ScheduleOnce` timer

#### Scenario: Timer fires offer a target into the local queue
- **WHEN** a `ScheduleOnce` timer fires for (lucerne, icon_d2)
- **THEN** the actor offers a `WeightedTarget(lucerne, icon_d2)` into its own local `Source.Queue`, which drains through the SinkRef into the PipelineActor's MergeHub

#### Scenario: Dynamically added target polls alongside existing ones
- **WHEN** `AddTargets([("lucerne", "metno_nordic")])` is received while 8 models are already polling
- **THEN** the new model gets its own timer and polls independently alongside the existing 8
