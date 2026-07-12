## MODIFIED Requirements

### Requirement: Typed command protocol with exhaustive variants
The pipeline SHALL accept commands via a sealed type hierarchy: `RefreshLocation(LocationId)` (fetch all models for one location) and `RefreshModel(LocationId, ModelId)` (fetch a single target). The `PollAll` variant is removed — scheduled polling is handled by the SchedulerActor's per-model timers. Commands are sent to the SchedulerActor, which resolves them to `WeightedTarget` elements and offers them into the pipeline's Source.Queue.

#### Scenario: RefreshLocation expands to one location's models
- **WHEN** a `RefreshLocation("lucerne")` command is sent to the SchedulerActor with 4 models configured
- **THEN** 4 `WeightedTarget` elements are offered into the Source.Queue, all for location "lucerne"

#### Scenario: RefreshModel expands to a single target
- **WHEN** a `RefreshModel("lucerne", "icon_d2")` command is sent to the SchedulerActor
- **THEN** exactly 1 `WeightedTarget` element is offered into the Source.Queue for (lucerne, icon_d2)

### Requirement: Each expanded target carries a pre-calculated API weight
The expand logic SHALL assign each `WeightedTarget` an integer weight computed as `ceil(hourlyVariableCount / 10) x ceil(forecastDays / 14)` based on the current configuration. The weight SHALL be determined at expansion time, not at fetch time.

#### Scenario: Default configuration yields weight 1
- **WHEN** the configuration has 9 hourly variables and 4 forecast days
- **THEN** each target's weight is `ceil(9/10) x ceil(4/14)` = 1

### Requirement: Commands with invalid references are silently dropped
The SchedulerActor SHALL discard commands that reference a location or model not present in the current configuration. No error SHALL be raised; a structured log at Warning level SHALL be emitted.

#### Scenario: Unknown location is dropped
- **WHEN** a `RefreshLocation("atlantis")` command is received and "atlantis" is not configured
- **THEN** zero targets are offered and a warning is logged

## REMOVED Requirements

### Requirement: PollAll expands to full target grid
**Reason**: `PollAll` is removed. Scheduled polling is now per-model via the SchedulerActor's `ScheduleOnce` timers. There is no "poll everything at once" command.
**Migration**: Remove the `PollAll` command variant and the `PollAll` case from the expand logic. The SchedulerActor schedules each model individually.
