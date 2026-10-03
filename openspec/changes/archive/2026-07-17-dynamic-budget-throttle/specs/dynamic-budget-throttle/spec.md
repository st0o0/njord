## ADDED Requirements

### Requirement: IBudgetProvider supplies the current rate to the throttle stage
An `IBudgetProvider` interface SHALL expose `GetCurrentRate()` returning a `BudgetRate(int CostPerMinute, int MaxBurst)`. The default implementation SHALL derive the rate from `NjordOptions.EffectiveBudget.RequestsPerMinute * 0.8` and a burst of 4. When `BudgetOverride` is changed via gRPC, the next call to `GetCurrentRate()` SHALL reflect the new rate.

#### Scenario: Default rate from free-tier budget
- **WHEN** no `BudgetOverride` is set and the free-tier budget is 600 req/min
- **THEN** `GetCurrentRate()` SHALL return `BudgetRate(480, 4)`

#### Scenario: Override changes rate immediately
- **WHEN** `BudgetOverride` is set to 60 req/min via gRPC
- **THEN** the next call to `GetCurrentRate()` SHALL return `BudgetRate(48, 4)`

### Requirement: BudgetThrottleStage shapes elements according to the current budget rate
`BudgetThrottleStage<T>` SHALL be a custom `GraphStage<FlowShape<T, T>>` implementing weighted token-bucket throttling. Tokens SHALL replenish at `CostPerMinute / 60` per second. Each element's cost SHALL be determined by a `costFunction`. Elements SHALL be held until sufficient tokens are available (shaping mode, no drops).

#### Scenario: Elements pass at budget rate
- **WHEN** the budget is 120 req/min and elements have weight 1
- **THEN** elements SHALL pass at approximately 2 per second

#### Scenario: Weighted elements consume proportional tokens
- **WHEN** an element has weight 2 and the budget is 120 req/min
- **THEN** the element SHALL consume 2 tokens, equivalent to delaying twice as long as a weight-1 element

#### Scenario: Burst allows initial fast throughput
- **WHEN** the bucket has accumulated tokens up to `MaxBurst` and a burst of elements arrives
- **THEN** up to `MaxBurst` cost-units SHALL pass immediately before rate-limiting takes effect

### Requirement: BudgetThrottleStage adapts to rate changes at runtime
The stage SHALL periodically poll `IBudgetProvider.GetCurrentRate()` and adjust its token refill rate accordingly. The polling interval SHALL be approximately 5 seconds. No graph re-materialization SHALL be required.

#### Scenario: Rate decrease takes effect within polling interval
- **WHEN** the budget is changed from 600 to 60 req/min
- **THEN** within 5 seconds the stage SHALL throttle at the new rate

#### Scenario: Rate increase takes effect within polling interval
- **WHEN** the budget is changed from 60 to 600 req/min
- **THEN** within 5 seconds the stage SHALL allow higher throughput

### Requirement: BudgetThrottleStage records API usage
After each element passes through the stage, it SHALL call `BudgetTracker.RecordCall(weight)` to track actual API usage.

#### Scenario: Usage is tracked per element
- **WHEN** a weight-1 element passes through the stage
- **THEN** `BudgetTracker.RecordCall(1)` SHALL be called
- **AND** `BudgetTracker.GetUsage()` SHALL reflect the new count
