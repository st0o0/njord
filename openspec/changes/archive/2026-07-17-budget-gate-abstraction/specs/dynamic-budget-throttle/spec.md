## MODIFIED Requirements

### Requirement: BudgetThrottleStage shapes elements according to the current budget rate
`BudgetThrottleStage<T>` SHALL be a custom `GraphStage<FlowShape<T, T>>` with a single dependency: `IBudgetGate<T>`. For each element, the stage SHALL call `IBudgetGate<T>.AcquireAsync(element)` and push the element downstream only after the gate returns. The stage SHALL use `GetAsyncCallback` to dispatch the async result back into the stage context. The stage SHALL NOT contain any token-bucket logic, budget-provider polling, or usage tracking.

#### Scenario: Elements pass when gate allows
- **WHEN** an element arrives and `IBudgetGate<T>.AcquireAsync` completes immediately
- **THEN** the stage SHALL push the element downstream without delay

#### Scenario: Elements wait when gate delays
- **WHEN** an element arrives and `IBudgetGate<T>.AcquireAsync` delays for 500ms
- **THEN** the stage SHALL push the element downstream after approximately 500ms

#### Scenario: Stage completes after pending element is emitted
- **WHEN** upstream completes while an element is waiting for gate acquisition
- **THEN** the stage SHALL push the pending element when the gate allows, then complete

### Requirement: IBudgetGate encapsulates throttling, tracking, and cost calculation
`IBudgetGate<T>` SHALL expose a single method `AcquireAsync(T element, CancellationToken ct)`. The implementation (`WeightedBudgetGate`) SHALL internally manage the token bucket, poll `IBudgetProvider` for rate changes, extract cost from the element, and call `BudgetTracker.RecordCall(cost)` after each acquisition. The stage SHALL NOT reference `IBudgetProvider`, `BudgetTracker`, or any cost function directly.

#### Scenario: Gate acquires immediately when tokens available
- **WHEN** the token bucket has sufficient tokens for the element's cost
- **THEN** `AcquireAsync` SHALL return immediately and record the call

#### Scenario: Gate delays when tokens insufficient
- **WHEN** the token bucket does not have sufficient tokens
- **THEN** `AcquireAsync` SHALL delay until tokens replenish, then return and record the call

#### Scenario: Gate adapts to rate changes
- **WHEN** the budget is changed via `IBudgetProvider`
- **THEN** subsequent `AcquireAsync` calls SHALL use the updated rate
