# dynamic-budget-throttle Specification

## Purpose

Budget-aware throttling of the `OpenMeteoGateway` fetch stream: an `IBudgetProvider` supplies the current request rate, `GatewayBudgetGate` encapsulates the token bucket and usage tracking, and `GatewayThrottleStage` shapes fetch targets by delegating to the gate.

## Requirements
### Requirement: IBudgetProvider supplies the current rate to the throttle stage
An `IBudgetProvider` interface (src/Njord.OpenMeteo/GatewayBudgetGate.cs) SHALL expose `GetCurrentRate()` returning a `BudgetRate(int CostPerMinute, int MaxBurst)`. The default implementation `OptionsBudgetProvider` SHALL read `PollingOptions` on every call through `IOptionsMonitor<PollingOptions>.CurrentValue`, resolve the effective budget with `BudgetCalculator.GetEffectiveBudget`, and return `CostPerMinute = RequestsPerMinute * 0.8` (truncated) and `MaxBurst = max(CostPerMinute / 30, 16)`. When the budget override is changed via gRPC, the next call to `GetCurrentRate()` SHALL reflect the new rate.

#### Scenario: Default rate from free-tier budget
- **WHEN** no budget override is set and the free-tier budget is 600 req/min
- **THEN** `GetCurrentRate()` SHALL return `BudgetRate(480, 16)`

#### Scenario: Override changes rate immediately
- **WHEN** the budget override is set to 60 req/min via gRPC
- **THEN** the next call to `GetCurrentRate()` SHALL return `BudgetRate(48, 16)`

### Requirement: GatewayThrottleStage shapes fetch targets according to the current budget rate
`GatewayThrottleStage` SHALL be an internal `GraphStage<FlowShape<FetchTarget, FetchTarget>>` built on `TimerGraphStageLogic` with a single dependency, `IGatewayBudgetGate`. For each element it SHALL call `TryAcquire(weight)` and push the element when it succeeds; otherwise it SHALL park the element and schedule a timer for `EstimateDelay(weight)`, retrying on each timer tick until acquisition succeeds, and SHALL record the wait in the throttle-wait histogram. The stage SHALL NOT contain token-bucket logic, budget-provider polling or usage tracking.

#### Scenario: Elements pass when gate allows
- **WHEN** an element arrives and `TryAcquire` succeeds
- **THEN** the stage pushes the element downstream without delay

#### Scenario: Elements wait when gate delays
- **WHEN** an element arrives, `TryAcquire` fails and `EstimateDelay` returns 500 ms
- **THEN** the stage pushes the element after the timer fires and `TryAcquire` succeeds

#### Scenario: Stage completes after pending element is emitted
- **WHEN** upstream completes while an element is parked
- **THEN** the stage pushes the parked element when the gate allows, then completes

### Requirement: The gateway budget gate encapsulates the token bucket and usage tracking
`IGatewayBudgetGate` SHALL expose `TryAcquire(int weight)` and `EstimateDelay(int weight)`. Its implementation `GatewayBudgetGate`, created by `OpenMeteoGateway` with the `IBudgetProvider`, the `IBudgetTracker` actor ref and the `TimeProvider`, SHALL keep the token bucket in memory, refill it from the elapsed time, and refresh its rate from `IBudgetProvider` at most every 5 seconds. After each successful acquisition it SHALL send `RecordApiCall(weight)` to the `BudgetTracker` via fire-and-forget `Tell`. There SHALL be no generic `IBudgetGate<T>` and no `WeightedBudgetGate`.

#### Scenario: Gate acquires immediately when tokens available
- **WHEN** the token bucket holds enough tokens for the weight
- **THEN** `TryAcquire` returns true and sends `RecordApiCall` to the budget tracker

#### Scenario: Gate rejects when tokens insufficient
- **WHEN** the token bucket does not hold enough tokens
- **THEN** `TryAcquire` returns false and sends no `RecordApiCall`

#### Scenario: Gate adapts to rate changes
- **WHEN** `IBudgetProvider` returns a new rate
- **THEN** `TryAcquire` uses the new rate after the next 5-second refresh

