## Context

The pipeline currently uses `Throttle(2, TimeSpan.FromSeconds(1), maximumBurst: 4, ThrottleMode.Shaping)` — a static operator whose rate is fixed at graph materialization. The `RequestBudget` (default 600 req/min free tier, overridable via config or gRPC) is not connected to the pipeline at all. The `BudgetTracker` exists but `RecordCall()` is never invoked.

## Goals / Non-Goals

**Goals:**
- Pipeline throttle rate derives from the effective budget at all times.
- Budget changes via gRPC `SetConfig` take effect without pipeline restart.
- `BudgetTracker.RecordCall()` is invoked for every API request.
- The stage supports weighted elements (via `WeightedTarget.Weight`).

**Non-Goals:**
- Monthly quota enforcement (hard-stopping polls).
- Dynamic concurrency (SelectAsync parallelism stays fixed at 2).

## Decisions

### Decision 1: IBudgetProvider interface polled by the stage

```csharp
public interface IBudgetProvider
{
    BudgetRate GetCurrentRate();
}

public sealed record BudgetRate(int CostPerMinute, int MaxBurst);
```

The stage calls `GetCurrentRate()` periodically (every ~5 seconds or on each element, whichever is less frequent). The implementation reads from `IOptionsMonitor<NjordOptions>` which reflects gRPC config changes immediately. The rate returned is `EffectiveBudget.RequestsPerMinute * 0.8` — same 80% politeness margin as the original design.

**Why an interface:** Testability. Tests inject a fake provider with a known rate. No dependency on IOptionsMonitor in the stage itself.

### Decision 2: Custom GraphStage with internal token bucket

The stage is a `GraphStage<FlowShape<WeightedTarget, WeightedTarget>>` that implements a token bucket internally:

- **Tokens** replenish at `costPerMinute / 60` per second.
- **Max tokens** = `maxBurst` from the provider.
- **Cost per element** = `element.Weight`.
- **On pull from downstream**: if tokens >= cost, pass element immediately. Otherwise, schedule a timer for the deficit duration and emit when tokens are available.
- **On timer**: check `IBudgetProvider.GetCurrentRate()` to update refill rate, then try to emit buffered element.
- **Rate refresh**: every N seconds (via a periodic timer), re-poll the provider. If the rate changed, adjust the refill rate. No need to drain or reset — the bucket naturally adapts.

This mirrors what the built-in `Throttle` does internally (it's also a token bucket GraphStage), but with a mutable rate source.

### Decision 3: BudgetTracker.RecordCall() in the stage

After each element passes through (token acquired), the stage calls `BudgetTracker.RecordCall(weight)`. The tracker is passed to the stage at construction time. This closes the loop — `GetConfig` via gRPC now returns accurate usage numbers.

### Decision 4: Stage is generic over element type, weight extracted via function

The stage accepts a `Func<T, int> costFunction` parameter, making it reusable beyond `WeightedTarget`:

```csharp
.Via(new BudgetThrottleStage<WeightedTarget>(
    budgetProvider, budgetTracker, element => element.Weight))
```

## Risks / Trade-offs

- **[Risk] Timer precision**: Akka scheduler timers have ~50ms precision. At high rates (600/min = 10/sec), the token bucket may undershoot slightly. Mitigated by the burst allowance.

- **[Risk] Provider polling frequency**: If the provider is polled every 5 seconds, a budget change takes up to 5 seconds to take effect. Acceptable for a configuration change.

- **[Trade-off] Complexity vs built-in Throttle**: A custom GraphStage is more code than `Throttle()`. But it's the only way to get runtime-configurable rates without re-materializing the graph.
