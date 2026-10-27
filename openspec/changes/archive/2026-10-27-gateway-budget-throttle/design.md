## Context

The `OpenMeteoGateway` dispatches fetch requests through an Akka.Streams pipeline. The budget gate (`GatewayBudgetGate`) sits in the stream as a synchronous `Select` that calls `TryAcquire(weight)`. When tokens are insufficient, it returns false immediately — the request becomes a `FetchDomainRejected(Budget)` reply. The PollWorker treats budget rejections as failures with a 5-minute minimum backoff.

The token bucket is configured by `OptionsBudgetProvider`: `costPerMinute = RequestsPerMinute * 0.8 = 480`, `maxBurst = max(costPerMinute / 30, 16) = 16`. With all features enabled, 16 ForecastDays and 3 weather models, a single Weather fetch has `CallWeight = ceil(23/10) * ceil(16/14) * 3 = 18`. Since 18 > 16 (MaxBurst), the fetch can never succeed — it is mathematically impossible to acquire, proven in E2E testing.

The existing `dynamic-budget-throttle` spec describes a custom `GatewayThrottleStage` (GraphStage with `TimerGraphStageLogic`, admission deadlines, parking) that was never implemented. This design replaces that approach with the simpler built-in `Throttle` operator.

## Goals / Non-Goals

**Goals:**
- Eliminate guaranteed budget rejections for high-weight Weather fetches
- Use native Akka.Streams backpressure (delay) instead of hard rejection for rate limiting
- Keep the `GatewayBudgetGate` as a safety net and usage accounting layer
- Ensure MaxBurst can always accommodate the heaviest single fetch

**Non-Goals:**
- Admission deadlines or expiry logic (future evolution)
- Throttle-wait histogram metrics (future evolution)
- Changing the PollWorker retry/backoff logic
- Changing the BudgetGuard startup validator

## Decisions

### 1. Native `Throttle` operator instead of custom GraphStage

Use `Throttle(cost, per, maximumBurst, costCalculation, ThrottleMode.Shaping)` — the built-in Akka.Streams operator.

**Why over custom GatewayThrottleStage:**
- Zero custom code — `Throttle` is battle-tested in Akka.Streams
- Cost-based overload natively supports per-domain weighting
- `ThrottleMode.Shaping` applies backpressure instead of throwing, which is exactly what we need
- No timer management, no parking logic, no deadline tracking

**Why not `ThrottleMode.Enforcing`:**
Enforcing throws `OverflowException` when rate is exceeded. Shaping delays the element until cost budget is available — this is the behavior we want.

**Trade-off:** No admission deadline handling. A request that waits too long in the throttle has no timeout. This is acceptable because: (a) the PollWorker already has a reply timeout (`FetchTimeouts`), and (b) poll intervals are 60 min — a few seconds of shaping delay is negligible.

### 2. Throttle placement: between Source.Queue and budget gate Select

```
Source.Queue<FetchDomain>(64, DropNew)
  .Select(request => (request, client))          // resolve domain client
  .Throttle(costPerMin, 1 min, maxBurst,         // NEW: shape by cost
      gated => gated.client?.CallWeight ?? 1,
      ThrottleMode.Shaping)
  .Select(gated => gate.TryAcquire(...))         // existing gate check
  .SelectAsyncUnordered(3, fetch)                 // existing concurrent fetch
  ...
```

The client resolution must happen before the throttle so the cost function can access `CallWeight`. The gate remains after the throttle as a safety net.

**Why not replace the gate entirely:** The gate handles usage accounting (`RecordApiCall` to `BudgetTracker`). It also catches edge cases where the throttle's internal token model diverges from the gate's (e.g., after a rate refresh). Removing it would lose the `BudgetTracker` integration.

### 3. MaxBurst floor: max(client.CallWeight) across all clients

`OptionsBudgetProvider.GetCurrentRate()` currently computes `maxBurst = max(costPerMinute / 30, 16)`. Change to `maxBurst = max(costPerMinute / 30, minBurst)` where `minBurst` comes from the heaviest registered domain client's `CallWeight`.

This requires `OptionsBudgetProvider` to know the max CallWeight. Options:
- **A) Inject `IEnumerable<IDomainClient>` into `OptionsBudgetProvider`** — clean DI, but creates a dependency on the client registration order.
- **B) Add a `MinBurst` field to `PollingOptions`** — explicit config, but requires manual maintenance.
- **C) Pass `maxCallWeight` as a constructor parameter** — simple, computed once at startup from the registered clients.

**Decision: Option C.** The gateway already has all clients. The `OptionsBudgetProvider` (or `GatewayBudgetGate` directly) takes the max weight as a constructor parameter. Simple, no new DI complexity.

### 4. Remove `EstimateDelay` from `IGatewayBudgetGate`

`EstimateDelay` was designed for the never-implemented `GatewayThrottleStage` parking logic. With the native `Throttle` handling delay estimation internally, this method has no callers. Remove it from the interface and implementation.

### 5. Throttle rate parameters from BudgetProvider

The `Throttle` stage is configured once at stream materialization with the budget rate. It does not dynamically update. The `GatewayBudgetGate` already refreshes its rate every 5 seconds from `IBudgetProvider`. Since `PollingOptions` are read at startup (no hot reload), the Throttle's static rate matches the gate's rate. If dynamic rate changes are needed in the future, the stream would need to be re-materialized — which already happens on gateway restart.

## Risks / Trade-offs

- **[Throttle adds latency to burst requests]** At startup, 3 locations x 6 domains = 18 requests arrive simultaneously. Weather (weight=18) will be delayed ~2-3 seconds per location. This is negligible compared to the current 5-minute rejection penalty. Lighter domains (weight=1) pass through nearly instantly.
- **[Queue pressure from Throttle backpressure]** The `Source.Queue(64, DropNew)` may fill up if the Throttle slows processing too much. At 480 tokens/min = 8/sec and max weight 18, a Weather fetch takes ~2.25 sec. With 6 domains per location, 3 locations = 18 requests — the queue handles this comfortably within its 64-element capacity.
- **[Throttle rate is static]** The Throttle stage rate is set at materialization time. If `BudgetOverride` could be changed at runtime (it cannot — options are startup-only), the throttle would use the old rate. The gate still catches overflow since it refreshes from the provider.

## Open Questions

(none — the design is straightforward and all decisions are resolved)
