# poll-status-query Specification

## Purpose

Query interface for retrieving a snapshot of all model poll states from the
SchedulerActor, enabling the gRPC status endpoint to expose per-model poll
phase, timing, and cycle information.

## Requirements

### Requirement: GetPollStates query returns a snapshot of all model poll states

The `SchedulerActor` SHALL handle a `GetPollStates` message and respond with a
`PollStatesSnapshot` containing a list of `PollStateEntry` records. Each entry
SHALL include `Location` (string), `ModelId` (string), `Phase` (PollPhase enum),
`NextPollUtc` (DateTimeOffset), `LastChangeUtc` (DateTimeOffset?), `MissCount`
(int), and `CycleSeconds` (long?). The snapshot SHALL reflect the current
`_states` dictionary at the time of the Ask.

#### Scenario: All configured models appear in the snapshot
- **WHEN** the SchedulerActor has 3 locations x 2 models each (6 poll states)
- **THEN** `PollStatesSnapshot.Entries` SHALL contain exactly 6 entries

#### Scenario: Snapshot reflects current state after data changes
- **WHEN** model "icon_d2" at "lucerne" has transitioned to Steady with cycle 3h, miss count 0, and last change at 09:30
- **THEN** the corresponding entry SHALL have Phase=Steady, CycleSeconds=10800, MissCount=0, and LastChangeUtc=09:30

#### Scenario: Discovery models report null cycle
- **WHEN** model "gfs_seamless" at "lucerne" is still in Discovery phase
- **THEN** the corresponding entry SHALL have Phase=Discovery and CycleSeconds=null

### Requirement: GetPollStates is stashed until the actor reaches Ready

The `GetPollStates` message SHALL be stashed in `WaitingForRefs`, `Connecting`,
and `WaitingForConnection` behaviors. It SHALL only be handled in the `Ready`
behavior to avoid returning partial state during startup.

#### Scenario: Query during startup is deferred
- **WHEN** a `GetPollStates` message arrives while the actor is in `WaitingForRefs`
- **THEN** the message SHALL be stashed and answered after the actor reaches Ready

### Requirement: Message types are defined in SchedulerMessages

`GetPollStates`, `PollStatesSnapshot`, and `PollStateEntry` SHALL be defined as
sealed records in `SchedulerMessages.cs` alongside existing message types.

#### Scenario: Messages follow existing conventions
- **WHEN** `GetPollStates` and `PollStatesSnapshot` are defined
- **THEN** they SHALL be sealed records in the `Njord.Pipeline` namespace
