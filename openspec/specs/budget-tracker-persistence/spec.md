# budget-tracker-persistence Specification

## Purpose

Persistent actor for tracking API call budget usage across restarts. Uses Akka.Persistence to journal call events, snapshot state periodically, and handle day/month boundary resets so budget counters survive process restarts.
## Requirements
### Requirement: BudgetTracker persists API call events
`BudgetTracker` SHALL be a `ReceivePersistentActor` with `PersistenceId` `"budget-tracker"`. When it receives a `RecordApiCall(int Weight)` command, it SHALL use the `ProcessCommand` extension method on its state to produce both the new state and the persistence event, then call `Persist` with the event.

#### Scenario: Recording a single API call
- **WHEN** the actor receives `RecordApiCall(Weight: 1)`
- **THEN** it SHALL call `_state.ProcessCommand(cmd, now)` to get the new state and `ApiCallRecorded` event
- **AND** persist the event and update `_state` in the persist callback

#### Scenario: Recording a weighted API call
- **WHEN** the actor receives `RecordApiCall(Weight: 4)`
- **THEN** it SHALL persist an `ApiCallRecorded` with weight 4 via the ProcessCommand pattern

### Requirement: BudgetTracker responds to usage queries
When the actor receives a `QueryBudgetUsage` command, it SHALL reply with a `BudgetUsageResult(long MonthlyUsed, long DailyUsed)` message reflecting current counters.

#### Scenario: Query returns current counters
- **WHEN** the actor has recorded 10 calls (weight 1 each) and receives `QueryBudgetUsage`
- **THEN** it SHALL reply with `BudgetUsageResult(MonthlyUsed: 10, DailyUsed: 10)`

### Requirement: BudgetTracker recovers state from persisted events
On recovery, the actor SHALL replay persisted `ApiCallRecorded` events to rebuild its monthly and daily counters. Events whose timestamp falls in a month earlier than the current UTC month SHALL be skipped during recovery. The initial state SHALL be constructed from `BudgetTrackerState.Empty` (a static readonly field with zeroed counters) plus an initialization step that sets the current month/day from TimeProvider.

#### Scenario: Recovery after restart within same month
- **WHEN** the actor recovers and the journal contains 25 events all from the current month
- **THEN** `MonthlyUsed` SHALL equal the sum of all event weights
- **AND** `DailyUsed` SHALL equal the sum of weights from events whose timestamp matches the current UTC day

#### Scenario: Recovery after month rollover
- **WHEN** the actor recovers and all journal events are from a previous month
- **THEN** `MonthlyUsed` SHALL be 0 and `DailyUsed` SHALL be 0

### Requirement: BudgetTracker snapshots and cleans up
The actor SHALL save a `PersistedBudgetTrackerState` snapshot whenever `LastSequenceNr % 50 == 0`. On `SaveSnapshotSuccess`, it SHALL delete all prior events and prior snapshots.

#### Scenario: Snapshot triggers after 50 events
- **WHEN** the actor has persisted exactly 50 events since the last snapshot
- **THEN** it SHALL call `SaveSnapshot` with the current state

#### Scenario: Snapshot cleanup deletes old data
- **WHEN** a `SaveSnapshotSuccess` is received
- **THEN** the actor SHALL call `DeleteMessages` up to the snapshot sequence number
- **AND** call `DeleteSnapshots` for all snapshots before the current one

### Requirement: BudgetTracker recovers from snapshots
When a `SnapshotOffer` is received during recovery, the actor SHALL restore its state from the `PersistedBudgetTrackerState` using `BudgetTrackerState.FromPersistence(dto, now)` — a static method on the record, not an extension method. If the snapshot's stored month differs from the current UTC month, it SHALL reset both counters to zero. If only the day differs, it SHALL reset `DailyUsed` to zero.

#### Scenario: Snapshot recovery same month same day
- **WHEN** a snapshot offers month=7, day=209, monthly=500, daily=30 and the current UTC is also month 7 day 209
- **THEN** the actor SHALL set `MonthlyUsed=500` and `DailyUsed=30`

#### Scenario: Snapshot recovery after month rollover
- **WHEN** a snapshot offers month=6 and the current UTC month is 7
- **THEN** the actor SHALL set `MonthlyUsed=0` and `DailyUsed=0`

#### Scenario: Snapshot recovery after day rollover within same month
- **WHEN** a snapshot offers month=7, day=208 and the current UTC is month 7 day 209
- **THEN** the actor SHALL set `MonthlyUsed` to the snapshot's monthly value and `DailyUsed=0`

### Requirement: BudgetTracker resets counters on day and month boundaries
During live operation, before processing each `RecordApiCall`, the actor SHALL check whether the current UTC month or day has changed. If the month changed, it SHALL reset both counters. If only the day changed, it SHALL reset `DailyUsed`.

#### Scenario: Day boundary during operation
- **WHEN** the actor has `DailyUsed=50` and a `RecordApiCall` arrives on a new UTC day (same month)
- **THEN** `DailyUsed` SHALL be reset to 0 before recording the new call
- **AND** `MonthlyUsed` SHALL be unchanged (plus the new call's weight)

#### Scenario: Month boundary during operation
- **WHEN** a `RecordApiCall` arrives on a new UTC month
- **THEN** both `MonthlyUsed` and `DailyUsed` SHALL be reset to 0 before recording the new call

### Requirement: Budget persistence records follow extend-only conventions
The budget journal event SHALL be the positional record `ApiCallRecorded(int Weight, DateTimeOffset Utc)` and the snapshot SHALL be `PersistedBudgetTrackerState(int Month, int Day, long MonthlyUsed, long DailyUsed)`, both in `Njord.Persistence/Events/Budget/`, without `[JsonProperty]` attributes and without a `Version` property. New properties in future versions SHALL be nullable or have defaults.

#### Scenario: Event wire format
- **WHEN** `ApiCallRecorded(3, 2024-08-15T00:00:00Z)` is serialized with Newtonsoft.Json
- **THEN** the JSON is `{"Weight":3,"Utc":"2024-08-15T00:00:00+00:00"}`

