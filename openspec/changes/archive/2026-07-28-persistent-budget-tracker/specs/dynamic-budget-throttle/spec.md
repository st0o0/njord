## MODIFIED Requirements

### Requirement: IBudgetGate encapsulates throttling, tracking, and cost calculation
`IBudgetGate<T>` SHALL expose `TryAcquire(T element)` and `EstimateDelay(T element)`. The implementation (`WeightedBudgetGate`) SHALL internally manage the token bucket, poll `IBudgetProvider` for rate changes, and extract cost from the element. After each successful acquisition, `WeightedBudgetGate` SHALL send `RecordApiCall(cost)` to the `BudgetTrackerActor` via `Tell` (fire-and-forget). The gate SHALL accept an `IActorRef` for the budget tracker actor in its constructor instead of a `BudgetTracker` instance.

#### Scenario: Gate acquires immediately when tokens available
- **WHEN** the token bucket has sufficient tokens for the element's cost
- **THEN** `TryAcquire` SHALL return true and send `RecordApiCall` to the actor

#### Scenario: Gate rejects when tokens insufficient
- **WHEN** the token bucket does not have sufficient tokens
- **THEN** `TryAcquire` SHALL return false and SHALL NOT send `RecordApiCall`

#### Scenario: Gate adapts to rate changes
- **WHEN** the budget is changed via `IBudgetProvider`
- **THEN** subsequent `TryAcquire` calls SHALL use the updated rate
