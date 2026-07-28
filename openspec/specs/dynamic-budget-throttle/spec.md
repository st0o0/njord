# dynamic-budget-throttle Specification

## Purpose

Dynamic budget-aware throttling for the poll pipeline. Provides an `IBudgetProvider` abstraction that exposes the current rate limit, an `IBudgetGate<T>` that encapsulates token-bucket throttling, provider polling, cost extraction, and usage tracking behind a single async acquisition method, and a `BudgetThrottleStage` Akka.Streams graph stage that delegates entirely to the gate to shape elements without containing any throttling logic itself.

## Requirements

### Requirement: IBudgetProvider supplies the current rate to the throttle stage
An `IBudgetProvider` interface SHALL expose `GetCurrentRate()` returning a `BudgetRate(int CostPerMinute, int MaxBurst)`. The default implementation SHALL derive the rate from `NjordOptions.EffectiveBudget.RequestsPerMinute * 0.8` and a burst of 4. When `BudgetOverride` is changed via gRPC, the next call to `GetCurrentRate()` SHALL reflect the new rate.

#### Scenario: Default rate from free-tier budget
- **WHEN** no `BudgetOverride` is set and the free-tier budget is 600 req/min
- **THEN** `GetCurrentRate()` SHALL return `BudgetRate(480, 4)`

#### Scenario: Override changes rate immediately
- **WHEN** `BudgetOverride` is set to 60 req/min via gRPC
- **THEN** the next call to `GetCurrentRate()` SHALL return `BudgetRate(48, 4)`

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
