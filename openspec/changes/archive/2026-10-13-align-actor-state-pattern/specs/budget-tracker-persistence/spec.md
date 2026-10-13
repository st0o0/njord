## MODIFIED Requirements

### Requirement: BudgetTrackerActor persists API call events
`BudgetTracker` SHALL be a `ReceivePersistentActor` with `PersistenceId` `"budget-tracker"`. When it receives a `RecordApiCall(int Weight)` command, it SHALL use the `ProcessCommand` extension method on its state to produce both the new state and the persistence event, then call `Persist` with the event.

#### Scenario: Recording a single API call
- **WHEN** the actor receives `RecordApiCall(Weight: 1)`
- **THEN** it SHALL call `_state.ProcessCommand(cmd, now)` to get the new state and `ApiCallRecorded` event
- **AND** persist the event and update `_state` in the persist callback

#### Scenario: Recording a weighted API call
- **WHEN** the actor receives `RecordApiCall(Weight: 4)`
- **THEN** it SHALL persist an `ApiCallRecorded` with weight 4 via the ProcessCommand pattern

### Requirement: BudgetTrackerActor recovers state from persisted events
On recovery, the actor SHALL replay persisted `ApiCallRecorded` events to rebuild its monthly and daily counters. Events whose timestamp falls in a month earlier than the current UTC month SHALL be skipped during recovery. The initial state SHALL be constructed from `BudgetTrackerState.Empty` (a static readonly field with zeroed counters) plus an initialization step that sets the current month/day from TimeProvider.

#### Scenario: Recovery after restart within same month
- **WHEN** the actor recovers and the journal contains 25 events all from the current month
- **THEN** `MonthlyUsed` SHALL equal the sum of all event weights
- **AND** `DailyUsed` SHALL equal the sum of weights from events whose timestamp matches the current UTC day

#### Scenario: Recovery after month rollover
- **WHEN** the actor recovers and all journal events are from a previous month
- **THEN** `MonthlyUsed` SHALL be 0 and `DailyUsed` SHALL be 0

### Requirement: BudgetTrackerActor recovers from snapshots
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
