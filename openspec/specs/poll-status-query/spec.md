# poll-status-query Specification

## Purpose

Query interface for a snapshot of all model poll states from the `PollScheduler` (`QueryPollStates`, `PollStatesResult` in `Njord.Messages.Pipeline`), used by the gRPC Ops service to expose per-model poll phase, timing and cycle information.

## Requirements
### Requirement: QueryPollStates query returns a snapshot of all model poll states
`PollScheduler` SHALL handle a `QueryPollStates` message and respond with a `PollStatesResult` containing a list of `PollStateEntry` records. Each entry SHALL include `Location` (`LocationId`), `ModelId` (string), `Phase` (`PollPhase` enum), `NextPollUtc` (DateTimeOffset), `LastChangeUtc` (DateTimeOffset?), `MissCount` (int), and `CycleSeconds` (long?). The snapshot SHALL reflect the current `PollSchedulerState.States` dictionary at the time of the Ask.

#### Scenario: All configured models appear in the snapshot
- **WHEN** the PollScheduler has 3 locations x 2 models each (6 poll states)
- **THEN** `PollStatesResult.Entries` SHALL contain exactly 6 entries

#### Scenario: Snapshot reflects current state after data changes
- **WHEN** model "icon_d2" at "lucerne" has transitioned to Steady with cycle 3h, miss count 0, and last change at 09:30
- **THEN** the corresponding entry SHALL have Phase=Steady, CycleSeconds=10800, MissCount=0, and LastChangeUtc=09:30

#### Scenario: Discovery models report null cycle
- **WHEN** model "gfs_seamless" at "lucerne" is still in Discovery phase
- **THEN** the corresponding entry SHALL have Phase=Discovery and CycleSeconds=null

### Requirement: Poll status messages live in Njord.Messages
`QueryPollStates`, the response hierarchy `PollStatesResponse` (`PollStatesResult(IReadOnlyList<PollStateEntry> Entries)`, `PollStatesFailed(Exception Cause)`) and `PollStateEntry` SHALL be sealed records in the `Njord.Messages.Pipeline` namespace (src/Njord.Messages/Pipeline/QueryPollStates.cs and PollStateEntry.cs).

#### Scenario: Messages follow existing conventions
- **WHEN** `QueryPollStates` and its replies are inspected
- **THEN** they are sealed records in `Njord.Messages.Pipeline`, and the replies derive from the abstract `PollStatesResponse`

