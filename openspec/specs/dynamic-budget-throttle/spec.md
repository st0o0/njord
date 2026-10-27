# dynamic-budget-throttle Specification

## Purpose

Budget-aware throttling of the `OpenMeteoGateway` fetch stream: an `IBudgetProvider` supplies the current request rate, `GatewayBudgetGate` encapsulates the token bucket and usage tracking, and `GatewayThrottleStage` shapes fetch targets by delegating to the gate.
## Requirements
### Requirement: BudgetGateStage is a custom GraphStage that shapes and accounts fetch requests
`BudgetGateStage<T>` SHALL be an `internal sealed` class extending `GraphStage<FlowShape<T, T>>` with a `TimerGraphStageLogic`. It SHALL take an `IBudgetProvider`, an `IActorRef` for the budget tracker, a `TimeProvider`, a `Func<T, int>` cost function, and a `maxCallWeight` floor for the burst. Its token bucket SHALL use `TimeProvider.GetUtcNow()` for time, not `DateTime.Now`. A periodic timer SHALL refresh the rate from `IBudgetProvider` every 5 seconds. The `MaxBurst` SHALL be at least `maxCallWeight`. After each successful acquisition the stage SHALL send `RecordApiCall(weight)` to the budget tracker via fire-and-forget `Tell`. The stage SHALL never reject an element -- it SHALL park elements and retry via `ScheduleOnce` until tokens are available.

#### Scenario: Element passes immediately when tokens available
- **WHEN** the token bucket holds enough tokens for the element's cost
- **THEN** the stage pushes the element downstream immediately and sends `RecordApiCall`

#### Scenario: Element is parked when tokens insufficient
- **WHEN** the token bucket does not hold enough tokens for the element's cost
- **THEN** the stage parks the element and schedules a retry timer for the estimated delay

#### Scenario: Parked element is released when tokens refill
- **WHEN** the retry timer fires and the token bucket has been refilled by elapsed time
- **THEN** the stage pushes the parked element downstream and sends `RecordApiCall`

#### Scenario: Retry reschedules when tokens still insufficient
- **WHEN** the retry timer fires but the token bucket still does not hold enough tokens
- **THEN** the stage reschedules the retry timer with a new delay estimate

#### Scenario: Rate refresh updates the token rate periodically
- **WHEN** the 5-second periodic refresh timer fires
- **THEN** the stage reads `IBudgetProvider.GetCurrentRate()` and updates its internal rate and max burst

#### Scenario: Upstream completion drains the parked element
- **WHEN** upstream completes while an element is parked
- **THEN** the stage waits for the parked element to be released, then completes

#### Scenario: MaxBurst accommodates the heaviest domain client
- **WHEN** the heaviest domain client has CallWeight=18 and the base burst formula yields 16
- **THEN** `MaxBurst` SHALL be 18, not 16

