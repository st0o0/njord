## RENAMED Requirements

- FROM: `### Requirement: GetPollStates query returns a snapshot of all model poll states`
- TO: `### Requirement: QueryPollStates query returns a snapshot of all model poll states`

## REMOVED Requirements

### Requirement: GetPollStates is stashed until the actor reaches Ready
**Reason**: Contradicts the code and the `poll-scheduler` requirement "QueryPollStates is handled in all behaviors": `SchedulerActor` answers `QueryPollStates` in every behavior (`WaitingForPipeline`, `WaitingForRefs`, `Connecting`, `WaitingForConnection`, `Ready`).
**Migration**: `poll-scheduler`: "QueryPollStates is handled in all behaviors".

## MODIFIED Requirements

### Requirement: QueryPollStates query returns a snapshot of all model poll states

The `SchedulerActor` SHALL handle a `QueryPollStates` message and respond with a
`PollStatesResult` containing a list of `PollStateEntry` records. Each entry
SHALL include `Location` (string), `ModelId` (string), `Phase` (PollPhase enum),
`NextPollUtc` (DateTimeOffset), `LastChangeUtc` (DateTimeOffset?), `MissCount`
(int), and `CycleSeconds` (long?). The snapshot SHALL reflect the current
`_states` dictionary at the time of the Ask.

#### Scenario: All configured models appear in the snapshot
- **WHEN** the SchedulerActor has 3 locations x 2 models each (6 poll states)
- **THEN** `PollStatesResult.Entries` SHALL contain exactly 6 entries

#### Scenario: Snapshot reflects current state after data changes
- **WHEN** model "icon_d2" at "lucerne" has transitioned to Steady with cycle 3h, miss count 0, and last change at 09:30
- **THEN** the corresponding entry SHALL have Phase=Steady, CycleSeconds=10800, MissCount=0, and LastChangeUtc=09:30

#### Scenario: Discovery models report null cycle
- **WHEN** model "gfs_seamless" at "lucerne" is still in Discovery phase
- **THEN** the corresponding entry SHALL have Phase=Discovery and CycleSeconds=null

### Requirement: Message types are defined in SchedulerMessages

`QueryPollStates`, `PollStatesResult`, and `PollStateEntry` SHALL be defined as
sealed records in `SchedulerMessages.cs` alongside existing message types.

#### Scenario: Messages follow existing conventions
- **WHEN** `QueryPollStates` and `PollStatesResult` are defined
- **THEN** they SHALL be sealed records in the `Njord.Pipeline` namespace
