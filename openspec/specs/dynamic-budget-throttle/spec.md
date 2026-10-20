# dynamic-budget-throttle Specification

## Purpose

Budget-aware throttling of the `OpenMeteoGateway` fetch stream: an `IBudgetProvider` supplies the current request rate, `GatewayBudgetGate` encapsulates the token bucket and usage tracking, and `GatewayThrottleStage` shapes fetch targets by delegating to the gate.
## Requirements
### Requirement: GatewayThrottleStage shapes fetch targets according to the current budget rate
`GatewayThrottleStage` SHALL be an internal `GraphStage` built on `TimerGraphStageLogic` that takes fetch targets and emits, per target, either an admitted target or a budget rejection. Its dependencies SHALL be `IGatewayBudgetGate` and the gateway's `TimeProvider`. For each target it SHALL call `TryAcquire(weight)` and emit the target as admitted when it succeeds. Otherwise, when the target's admission deadline is already past it SHALL emit an `Expired` rejection; when `now + EstimateDelay(weight)` lies beyond the deadline it SHALL emit a rejection at once — `Budget` when the token wait alone would not fit the window counted from the target's receipt, `Expired` when the window was used up while the target queued; else it SHALL park the target and schedule a timer for `EstimateDelay(weight)`, retrying on each timer tick until acquisition succeeds or the deadline can no longer be met, and SHALL record the wait of an admitted target in the throttle-wait histogram. A rejection SHALL NOT consume tokens. The stage SHALL NOT contain token-bucket logic, budget-provider polling or usage tracking.

#### Scenario: Elements pass when gate allows
- **WHEN** an element arrives and `TryAcquire` succeeds
- **THEN** the stage emits the element as admitted without delay

#### Scenario: Elements wait when gate delays
- **WHEN** an element arrives, `TryAcquire` fails, `EstimateDelay` returns 500 ms and the admission deadline is 20 s away
- **THEN** the stage emits the element as admitted after the timer fires and `TryAcquire` succeeds

#### Scenario: Stage completes after pending element is emitted
- **WHEN** upstream completes while an element is parked
- **THEN** the stage emits the parked element when the gate allows or its deadline can no longer be met, then completes

#### Scenario: Element that spent its window in the queue expires
- **WHEN** an element received 18 s ago with a 20 s window arrives, `TryAcquire` fails and `EstimateDelay` returns 5 s
- **THEN** the stage emits an `Expired` rejection for it, not a `Budget` rejection

#### Scenario: Element rejected when the budget cannot admit it in time
- **WHEN** an element arrives, `TryAcquire` fails and `EstimateDelay` returns 60 s while the admission deadline is 20 s away
- **THEN** the stage emits a `Budget` rejection for it immediately and no tokens are consumed

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

