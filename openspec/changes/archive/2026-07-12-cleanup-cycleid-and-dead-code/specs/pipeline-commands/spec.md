## MODIFIED Requirements

### Requirement: Each expanded target carries a pre-calculated API weight
The expand logic SHALL assign each `WeightedTarget` an integer weight computed as `ceil(hourlyVariableCount / 10) x ceil(forecastDays / 14)` based on the current configuration. Each `WeightedTarget` SHALL also carry a `CycleId` assigned at creation time by the SchedulerActor. All targets produced by a single timer fire or manual refresh SHALL share the same `CycleId`.

#### Scenario: Default configuration yields weight 1
- **WHEN** the configuration has 9 hourly variables and 4 forecast days
- **THEN** each target's weight is `ceil(9/10) x ceil(4/14)` = 1

#### Scenario: Extended configuration yields higher weight
- **WHEN** the configuration has 15 hourly variables and 4 forecast days
- **THEN** each target's weight is `ceil(15/10) x ceil(4/14)` = 2

#### Scenario: Extended forecast days increase weight
- **WHEN** the configuration has 9 hourly variables and 16 forecast days
- **THEN** each target's weight is `ceil(9/10) x ceil(16/14)` = 2

#### Scenario: All targets from a scheduled poll share the same CycleId
- **WHEN** a scheduled poll fires for (lucerne, icon_d2) at 12:00:00 UTC
- **THEN** the offered `WeightedTarget` carries a `CycleId` with timestamp 12:00:00 UTC

#### Scenario: All targets from a RefreshLocation share the same CycleId
- **WHEN** a `RefreshLocation("lucerne")` is received with 8 models configured
- **THEN** all 8 `WeightedTarget` elements carry the same `CycleId` timestamp

## REMOVED Requirements

### Requirement: Typed command protocol with exhaustive variants
**Reason**: The ExpandStage that mapped commands to targets has been removed. The SchedulerActor now directly creates `WeightedTarget` elements in its command handlers — the command-to-target expansion logic is internal to the actor, not a separate stage.
**Migration**: No external consumers of ExpandStage exist. The SchedulerActor's `OnScheduledPoll`, `OnRefreshModel`, and `OnRefreshLocation` handlers create targets directly.
