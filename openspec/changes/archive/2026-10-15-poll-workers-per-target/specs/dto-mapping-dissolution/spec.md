## MODIFIED Requirements

### Requirement: No separate DtoMapping classes

Persistence conversion logic SHALL live directly in the actor's
StateExtensions class. Separate `*DtoMapping` static classes SHALL NOT exist.

#### Scenario: BudgetTracker persistence conversion
- **WHEN** `BudgetTrackerState.GetPersistenceState()` is called
- **THEN** it SHALL build `PersistedBudgetTrackerState` directly without delegating to a mapping class

#### Scenario: BudgetTracker event conversion
- **WHEN** an `ApiCallRecorded` event needs to be created or parsed
- **THEN** the conversion methods SHALL be extensions on `BudgetTrackerState` in `BudgetTrackerStateExtensions`

#### Scenario: PollScheduler persistence conversion
- **WHEN** the poll worker's snapshot record `PersistedPollWorkerState` needs to be created or parsed
- **THEN** the conversion methods SHALL be `PollWorkerState.FromPersistence` and the `GetPersistenceState` extension in `PollWorkerStateExtensions`

#### Scenario: Mapping files deleted
- **WHEN** the codebase is inspected
- **THEN** neither `BudgetTrackerDtoMapping.cs` nor `SchedulerDtoMapping.cs` SHALL exist
