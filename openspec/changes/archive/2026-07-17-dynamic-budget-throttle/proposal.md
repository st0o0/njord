## Why

The pipeline throttle is hardcoded to 2 req/sec and ignores the configured `RequestBudget` entirely. When a user sets a `BudgetOverride` via gRPC (e.g., to throttle down to 30 req/min on a shared instance), the pipeline continues at 120 req/min. Additionally, the `BudgetTracker.RecordCall()` is never called — the budget usage counter is always zero.

The built-in `Throttle` operator in Akka.Streams is static (rate fixed at materialization). A custom `GraphStage` that polls an `IBudgetProvider` interface enables runtime rate adaptation without re-materializing the stream graph.

## What Changes

- Introduce `IBudgetProvider` interface with `GetCurrentRate()` returning cost-per-minute and max-burst.
- Implement `IBudgetProvider` backed by `IOptionsMonitor<NjordOptions>` so it reflects `BudgetOverride` changes immediately.
- Build a custom `GraphStage<FlowShape<T,T>>` (`BudgetThrottleStage`) that implements weighted token-bucket throttling, polling `IBudgetProvider` periodically to adjust its refill rate.
- Replace the hardcoded `Throttle(2, 1sec, burst: 4)` in `PipelineActor` with the new `BudgetThrottleStage`.
- Call `BudgetTracker.RecordCall(weight)` after each element passes through the stage.
- Keep `SelectAsyncUnordered(2)` for connection parallelism — the stage controls rate, SelectAsync controls concurrency.

## Non-goals

- Changing the `SchedulerActor` — it continues to plan polls by model cycle, unaware of budget.
- Monthly budget enforcement (pausing polls when monthly limit is reached). The stage is a rate limiter, not a quota enforcer.
- Changing the `WeightedTarget.Weight` calculation.
- No API-budget impact — the change controls how fast existing requests are sent, not how many.

## Capabilities

### New Capabilities

- `dynamic-budget-throttle`: Custom Akka.Streams GraphStage that throttles based on a runtime-configurable budget provider.

### Modified Capabilities

- `poll-pipeline`: Pipeline uses `BudgetThrottleStage` instead of static `Throttle`.

## Impact

- New: `src/Njord/Pipeline/IBudgetProvider.cs` — interface + `OptionsBudgetProvider` implementation.
- New: `src/Njord/Pipeline/BudgetThrottleStage.cs` — custom GraphStage.
- Modified: `src/Njord/Pipeline/PipelineActor.cs` — replace `Throttle` with `BudgetThrottleStage`.
- Modified: `src/Njord/Configuration/NjordServiceSetup.cs` — register `IBudgetProvider`.
- Modified: `src/Njord/Configuration/BudgetTracker.cs` — called from stage.
- New: `src/Njord.Tests/Pipeline/BudgetThrottleStageSpec.cs` — stage tests.
