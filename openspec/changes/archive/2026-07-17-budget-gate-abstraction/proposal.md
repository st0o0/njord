## Why

The `BudgetThrottleStage` currently takes 3 dependencies (`IBudgetProvider`, `BudgetTracker`, `Func<T, int> costFunction`) and contains all the token-bucket logic internally. The stage knows about budget rates, token refilling, call tracking, and cost calculation — four concerns mixed into one GraphStage. This makes it hard to test and violates single responsibility.

The stage should know only one thing: "ask a gate if an element may pass, wait if not." Everything else — budget reading, token management, cost calculation, usage tracking — belongs behind a single `IBudgetGate<T>` interface.

## What Changes

- Introduce `IBudgetGate<T>` with a single method `AcquireAsync(T element, CancellationToken ct)` that blocks until the element is allowed through.
- Implement `WeightedBudgetGate` for `WeightedTarget` — encapsulates token-bucket, `IBudgetProvider` polling, `BudgetTracker.RecordCall()`, and cost extraction (`element.Weight`).
- Simplify `BudgetThrottleStage<T>` to a single dependency: `IBudgetGate<T>`. The stage uses `GetAsyncCallback` to dispatch the async acquire result back into the stage.
- Remove `IBudgetProvider`, `BudgetTracker`, and `costFunction` from the stage constructor.
- Update `PipelineActor` to create a `WeightedBudgetGate` and pass it to the stage.
- Update tests to use a fake `IBudgetGate<T>`.

## Non-goals

- Changing the token-bucket algorithm or budget semantics.
- Changing the `IBudgetProvider` interface.
- No API-budget impact.

## Capabilities

### New Capabilities

(none — this is a refactor of internal implementation)

### Modified Capabilities

- `dynamic-budget-throttle`: Stage takes `IBudgetGate<T>` instead of 3 separate dependencies. Gate encapsulates all throttling logic.

## Impact

- New: `src/Njord/Pipeline/IBudgetGate.cs` — interface + `WeightedBudgetGate` implementation.
- Modified: `src/Njord/Pipeline/BudgetThrottleStage.cs` — simplified to single `IBudgetGate<T>` dependency, uses `GetAsyncCallback`.
- Modified: `src/Njord/Pipeline/PipelineActor.cs` — creates `WeightedBudgetGate`, passes to stage.
- Modified: `src/Njord.Tests/Pipeline/BudgetThrottleStageSpec.cs` — tests use fake gate.
- Removed: direct `IBudgetProvider`/`BudgetTracker`/`costFunction` references from stage.
