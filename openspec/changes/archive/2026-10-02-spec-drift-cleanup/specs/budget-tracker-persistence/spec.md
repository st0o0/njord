## MODIFIED Requirements

### Requirement: BudgetTrackerActor responds to usage queries
When the actor receives a `QueryBudgetUsage` command, it SHALL reply with a `BudgetUsageResult(long MonthlyUsed, long DailyUsed)` message reflecting current counters.

#### Scenario: Query returns current counters
- **WHEN** the actor has recorded 10 calls (weight 1 each) and receives `QueryBudgetUsage`
- **THEN** it SHALL reply with `BudgetUsageResult(MonthlyUsed: 10, DailyUsed: 10)`
