## MODIFIED Requirements

### Requirement: BudgetThrottleStage is internal to the OpenMeteoGateway
`BudgetThrottleStage<T>` SHALL reside in `Njord.OpenMeteo` and be used exclusively within the `OpenMeteoGateway` actor's internal stream. It SHALL NOT be accessible from other projects. The stage SHALL delegate to the gateway's internal token bucket for rate-limiting decisions. The `IBudgetGate<T>` interface and `WeightedBudgetGate` DI service SHALL be removed — the token bucket is a private concern of the gateway.

#### Scenario: BudgetThrottleStage lives in Njord.OpenMeteo
- **WHEN** the codebase is searched for `BudgetThrottleStage`
- **THEN** it SHALL exist only in the `Njord.OpenMeteo` project

#### Scenario: No IBudgetGate interface exists
- **WHEN** the codebase is searched for `IBudgetGate`
- **THEN** no results SHALL be found

#### Scenario: No WeightedBudgetGate DI registration exists
- **WHEN** the DI container is inspected
- **THEN** no `WeightedBudgetGate` service SHALL be registered

### Requirement: IBudgetProvider is internal to OpenMeteoGateway
The `IBudgetProvider` interface and its `OptionsBudgetProvider` implementation SHALL reside in `Njord.OpenMeteo` as internal types used only by the gateway. They SHALL NOT be registered as public DI services.

#### Scenario: IBudgetProvider is internal
- **WHEN** `Njord.OpenMeteo` is inspected
- **THEN** `IBudgetProvider` SHALL be an internal interface

## REMOVED Requirements

### Requirement: IBudgetGate encapsulates throttling, tracking, and cost calculation
**Reason**: The `IBudgetGate<T>` abstraction and `WeightedBudgetGate` DI service are replaced by the gateway's internal token bucket. The gateway directly manages the token bucket, cost calculation, and `RecordApiCall` Tell to BudgetTracker.
**Migration**: Remove `IBudgetGate<T>`, `WeightedBudgetGate`, and the DI registration in `PipelineSetupContainer`. The token bucket logic moves into the gateway actor.

### Requirement: BudgetThrottleStage shapes elements according to the current budget rate
**Reason**: The stage's contract changes — it no longer depends on `IBudgetGate<T>`. It delegates to the gateway's internal token bucket instead. The previous spec described the stage calling `IBudgetGate<T>.AcquireAsync()` — this is replaced by a simpler `TryAcquire`/`EstimateDelay` pattern on the internal bucket.
**Migration**: The stage is rewritten as a gateway-internal component using the gateway's own token bucket field.
