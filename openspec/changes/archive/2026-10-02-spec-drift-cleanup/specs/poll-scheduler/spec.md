## RENAMED Requirements

- FROM: `### Requirement: GetPollStates is handled in all behaviors`
- TO: `### Requirement: QueryPollStates is handled in all behaviors`

## MODIFIED Requirements

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

### Requirement: QueryPollStates is handled in all behaviors
The SchedulerActor SHALL handle `QueryPollStates` messages in ALL behaviors (`WaitingForPipeline`, `WaitingForRefs`, `Connecting`, `WaitingForConnection`, `Ready`) by responding immediately with a `PollStatesResult` of the current `_states` dictionary. The handler SHALL NOT stash, delay, or drop the message in any state.

#### Scenario: Query returns current state during pipeline resolution
- **WHEN** a `QueryPollStates` message is received while the actor is waiting for the PipelineActor reference to resolve
- **THEN** the actor SHALL respond with a `PollStatesResult` (which may be empty if no states are initialized yet)

#### Scenario: Query returns current state in Ready
- **WHEN** a `QueryPollStates` message is received in Ready state with 6 model poll states
- **THEN** the actor SHALL respond with `PollStatesResult` containing 6 entries

#### Scenario: Query is read-only
- **WHEN** a `QueryPollStates` message is received in any behavior
- **THEN** no events SHALL be persisted and no timers SHALL be scheduled

## ADDED Requirements

### Requirement: SchedulerActor accepts TriggerImmediatePoll message
The `SchedulerActor` SHALL handle a `TriggerImmediatePoll(string Location, string Model)` message in the `Ready` behavior by scheduling an immediate `ScheduledPoll` for each matching target and replying with `TriggerPollResult(Count, Targets)`, where each target is `"{location}/{model}"`. When `Location` or `Model` is empty, it SHALL expand to all matching configured pairs. In every other behavior the message SHALL be stashed until the actor is `Ready`.

#### Scenario: Immediate poll bypasses normal schedule
- **WHEN** `SchedulerActor` receives `TriggerImmediatePoll("home", "icon_d2")`
- **THEN** the actor SHALL schedule a `ScheduledPoll("home", "icon_d2")` immediately and reply with `TriggerPollResult(1, ["home/icon_d2"])`
- **AND** the normal schedule for that model SHALL NOT be disrupted
