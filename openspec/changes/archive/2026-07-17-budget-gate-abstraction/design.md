## Context

The `BudgetThrottleStage<T>` currently has 3 constructor parameters and ~100 lines of token-bucket logic inside the `GraphStageLogic`. The stage mixes flow-control concerns (when to push/pull/schedule timers) with budget concerns (token refill, rate polling, usage tracking, cost calculation).

## Goals / Non-Goals

**Goals:**
- Stage has one dependency: `IBudgetGate<T>`.
- All budget/throttle logic lives in the gate implementation.
- Stage logic is ~30 lines: grab element → call gate → push on callback.
- Gate is independently testable without Akka.Streams.

**Non-Goals:**
- Changing the throttle behaviour or budget semantics.

## Decisions

### Decision 1: IBudgetGate<T> with AcquireAsync

```csharp
public interface IBudgetGate<in T>
{
    Task AcquireAsync(T element, CancellationToken ct = default);
}
```

The gate blocks until the element is allowed. Internally it manages tokens, polls the provider, and calls `RecordCall()`. The caller doesn't know or care how the decision is made.

The method takes the full element (not just cost) so the gate can extract cost via its own logic. This avoids passing `Func<T, int>` to the stage.

### Decision 2: Stage uses GetAsyncCallback for async dispatch

The stage calls `gate.AcquireAsync(element)` and uses `GetAsyncCallback<T>` to marshal the result back into the stage's thread-safe context:

```
OnPush:
  element = Grab(In)
  asyncCallback = GetAsyncCallback<T>(OnAcquired)
  gate.AcquireAsync(element).ContinueWith(_ => asyncCallback(element))

OnAcquired(element):
  Push(Out, element)
  if upstream finished → CompleteStage
  else if downstream pulled → Pull(In)
```

No timers needed in the stage. The gate's `AcquireAsync` handles the waiting internally (via `Task.Delay` or a similar mechanism).

### Decision 3: WeightedBudgetGate encapsulates everything

```csharp
public sealed class WeightedBudgetGate : IBudgetGate<WeightedTarget>
{
    // constructor: IBudgetProvider, BudgetTracker
    // internal: token bucket, periodic rate refresh, cost = element.Weight
}
```

The gate uses `SemaphoreSlim` or `Task.Delay` for async waiting — no Akka dependencies. It's a plain .NET class testable with xUnit directly.

### Decision 4: Rate refresh inside the gate, not via stage timer

The gate refreshes its rate from `IBudgetProvider` on its own schedule (e.g., every 5 seconds, checked on each `AcquireAsync` call). The stage has no timer at all — it's fully reactive.

## Risks / Trade-offs

- **[Risk] Thread safety**: `AcquireAsync` may be called from the stage's dispatcher thread. The gate must be thread-safe. Token bucket state protected by a lock or interlocked operations.

- **[Trade-off] Gate holds async callers**: When tokens are insufficient, the gate delays via `Task.Delay`. Multiple concurrent callers (from `SelectAsyncUnordered`) could queue up. With `SelectAsyncUnordered(2)` the max concurrent callers is 2 — manageable.
