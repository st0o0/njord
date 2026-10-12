## MODIFIED Requirements

### Requirement: BudgetTracker persists API call events
`BudgetTracker` (renamed from `BudgetTrackerActor`) SHALL be a `ReceivePersistentActor` with `PersistenceId` `"budget-tracker"` residing in `Njord.Budget`. When it receives a `RecordApiCall(double Weight)` command, it SHALL persist an `ApiCallRecordedDto` event and update its in-memory monthly and daily usage counters. The actor SHALL be registered under the `IBudgetTracker` key via `BudgetSetupContainer`.

#### Scenario: Recording a single API call
- **WHEN** the actor receives `RecordApiCall(Weight: 1)`
- **THEN** it SHALL persist an `ApiCallRecordedDto` with weight 1 and the current UTC timestamp
- **AND** increment `MonthlyUsed` and `DailyUsed` by 1

#### Scenario: Recording a weighted API call
- **WHEN** the actor receives `RecordApiCall(Weight: 3)`
- **THEN** it SHALL persist an `ApiCallRecordedDto` with weight 3
- **AND** increment `MonthlyUsed` and `DailyUsed` by 3

### Requirement: BudgetTracker responds to usage queries
When the actor receives a `QueryBudgetUsage` command, it SHALL reply with a `QueryBudgetUsageCompleted(long MonthlyUsed, long DailyUsed)` message reflecting current counters.

#### Scenario: Query returns current counters
- **WHEN** the actor has recorded 10 calls (weight 1 each) and receives `QueryBudgetUsage`
- **THEN** it SHALL reply with `QueryBudgetUsageCompleted(MonthlyUsed: 10, DailyUsed: 10)`

## RENAMED Requirements

### Requirement: BudgetTrackerActor renamed to BudgetTracker
- **FROM:** `BudgetTrackerActor` class in `Njord.Pipeline`
- **TO:** `BudgetTracker` class in `Njord.Budget`

### Requirement: IBudgetTrackerActor key renamed to IBudgetTracker
- **FROM:** `IBudgetTrackerActor` marker interface
- **TO:** `IBudgetTracker` marker interface in `Njord.Core/Actors/ActorKeys.cs`

### Requirement: Registration moves from PipelineSetupContainer to BudgetSetupContainer
- **FROM:** `PipelineSetupContainer` registers `BudgetTrackerActor` under `IBudgetTrackerActor`
- **TO:** `BudgetSetupContainer` registers `BudgetTracker` under `IBudgetTracker`
