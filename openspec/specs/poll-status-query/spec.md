# poll-status-query Specification

## Purpose

Query interface for an overview of all model poll states from the `PollManager`, built from the poll workers' reports (`QueryPollStates`, `PollStatesResult` in `Njord.Messages.Pipeline`), used by the gRPC Ops service to expose per-model poll phase, timing and cycle information.
## Requirements
### Requirement: QueryPollStates query returns an overview of all model poll states
The poll manager (`IPollManager`) SHALL handle a `QueryPollStates` message and respond with a `PollStatesResult` containing one `PollStateEntry` per configured target, in configuration order. Each entry SHALL include `Location` (`LocationId`), `ModelId` (string), `Phase` (`PollPhase` enum), `NextPollUtc` (DateTimeOffset), `LastChangeUtc` (DateTimeOffset?), `MissCount` (int), and `CycleSeconds` (long?), in the configured spelling. The overview SHALL be the latest poll state each target's worker reported (or the target's initial state before its first report) at the time of the Ask.

#### Scenario: All configured models appear in the overview
- **WHEN** 3 locations x 2 models each are configured (6 targets)
- **THEN** `PollStatesResult.Entries` SHALL contain exactly 6 entries

#### Scenario: Overview reflects current state after data changes
- **WHEN** the worker of "icon_d2" at "lucerne" has reported Steady with cycle 3h, miss count 0, and last change at 09:30
- **THEN** the corresponding entry SHALL have Phase=Steady, CycleSeconds=10800, MissCount=0, and LastChangeUtc=09:30

#### Scenario: Discovery models report null cycle
- **WHEN** the worker of "gfs_seamless" at "lucerne" is still in Discovery phase
- **THEN** the corresponding entry SHALL have Phase=Discovery and CycleSeconds=null

#### Scenario: Miss count follows the reports
- **WHEN** the worker of (lucerne, icon_d2) reported a change and then an unchanged forecast
- **THEN** the entry of (lucerne, icon_d2) has MissCount=1 and a LastChangeUtc

### Requirement: Poll status messages live in Njord.Messages
`QueryPollStates`, the response hierarchy `PollStatesResponse` (`PollStatesResult(IReadOnlyList<PollStateEntry> Entries)`, `PollStatesFailed(Exception Cause)`) and `PollStateEntry` SHALL be sealed records in the `Njord.Messages.Pipeline` namespace (src/Njord.Messages/Pipeline/QueryPollStates.cs and PollStateEntry.cs).

#### Scenario: Messages follow existing conventions
- **WHEN** `QueryPollStates` and its replies are inspected
- **THEN** they are sealed records in `Njord.Messages.Pipeline`, and the replies derive from the abstract `PollStatesResponse`

