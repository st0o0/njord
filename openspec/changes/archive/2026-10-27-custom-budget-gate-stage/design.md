## Context

The gateway stream was recently improved by adding a built-in `Throttle` operator before the `GatewayBudgetGate` Select. This fixed the immediate problem (Weather CallWeight=18 exceeding MaxBurst=16), but introduced a dual-token-bucket architecture where two independent rate limiters run on different clocks (`DateTime.Now.Ticks` for Throttle, `TimeProvider` for gate).

The Akka.NET `Throttle<T>` implementation was decompiled (v1.5.73) and analyzed. It uses `TimerGraphStageLogic` with a simple pattern: `OnPush` tries `TokenBucket.Offer(cost)`, parks the element and schedules a timer if the delay is non-zero, and `OnTimer` pushes the parked element (no re-check needed because the internal bucket computed the exact delay). `OnPull` always calls `Pull(In)` -- downstream demand only arrives after a push, so there is no risk of overlapping elements.

## Goals / Non-Goals

**Goals:**
- Single token bucket with a single clock (`TimeProvider`)
- Integrated accounting (`RecordApiCall`) in the same stage
- Dynamic rate refresh via periodic timer (not static at materialization)
- Never reject -- always park until tokens available
- Follow the verified Akka.NET Throttle pattern exactly

**Non-Goals:**
- Changing `IBudgetProvider`, `BudgetRate`, or `RecordApiCall`
- Adding admission deadlines, expiry, or priority
- Making `BudgetGateStage` generic/reusable outside the gateway

## Decisions

### 1. Follow the Akka Throttle pattern with three additions

The decompiled Akka `Throttle<T>.Logic` pattern:
```
OnPush:  Grab → Offer(cost) → delay==0 ? Push : Park + ScheduleOnce(delay)
OnPull:  Pull(In)
OnTimer: Push(parked)
```

Our additions:
- **TimeProvider** for `Refill()` instead of `DateTime.Now.Ticks` in `TickTimeTokenBucket`
- **RecordApiCall** Tell after each successful acquisition
- **Periodic rate refresh** timer from `IBudgetProvider` every 5 seconds

### 2. Re-check in OnTimer (differs from Akka Throttle)

The built-in Throttle does NOT re-check in `OnTimer` because its `TokenBucket.Offer()` computes the exact delay using `DateTime.Now.Ticks` -- the timer fires at the precise moment tokens are available.

Our stage uses `TimeProvider` which in tests (`FakeTimeProvider`) does not advance with real time. The `ScheduleOnce` timer fires based on the Akka scheduler (real time), but `Refill()` reads `TimeProvider`. In production these align; in tests they may diverge. Therefore `OnTimer("retry")` re-checks: `Refill()` then test `_tokens >= weight`, and reschedules if still insufficient.

This makes the stage correct regardless of clock alignment and enables testing with high rates (short real-time delays) while `FakeTimeProvider` controls the token math separately when needed.

### 3. OnPull always pulls (verified from Akka source)

Akka's Throttle `OnPull` unconditionally calls `Pull(In)`. This is safe because:
- `OnPull` is only called when downstream has fresh demand (after a previous `Push`)
- If an element is parked, downstream has not received a Push, so no new `OnPull` arrives
- The timer handles the parked element independently

Our stage follows this exactly. No parking check in `OnPull`.

### 4. Stage placement in the gateway stream

```
Source.Queue<FetchDomain>(64, DropNew)
    .Select(request => new ResolvedRequest(request, clients.GetValueOrDefault(request.Domain)))
    .Via(new BudgetGateStage<ResolvedRequest>(
        provider, trackerActor, timeProvider,
        r => r.Client?.CallWeight ?? 1, maxCallWeight))
    .Select(resolved => resolved.Client is null
        ? new GatedRequest(resolved.Request, null, FetchRejectionReason.PipelineUnavailable)
        : new GatedRequest(resolved.Request, resolved.Client, null))
    .SelectAsyncUnordered(3, fetch)
    ...
```

The `ResolvedRequest` record stays -- the stage needs the resolved client to compute cost. Unknown domains (Client=null) pass through with cost 1 (negligible) and are rejected by the subsequent Select.

### 5. File location and naming

`BudgetGateStage.cs` in `src/Njord.OpenMeteo/Budget/` alongside the existing `IBudgetProvider` and `BudgetRate`. The stage is `internal sealed` -- not exposed outside the assembly.

### 6. Testing strategy

**Token bucket logic tests:** Use high rates (6000/min = 100 tokens/sec) so `ScheduleOnce` delays are short (< 200ms real time). Akka TestKit `ExpectMsg` with reasonable timeouts.

**Integration tests:** Verify the gateway stream end-to-end: high-weight request is delayed and eventually completes (not rejected). Same pattern as existing `OpenMeteoGatewayAttachTests`.

**Removed tests:** `GatewayBudgetGateTests` (replaced by `BudgetGateStageTests`), `EstimateDelay` tests (removed in prior change), budget rejection gateway test (no more rejections).

## Risks / Trade-offs

- **[Timer granularity]** Akka timer resolution is ~10ms. `EstimateDelay` returns minimum 10ms. For weight=18 at 480/min (8/sec), delay is ~2.25s -- well above granularity.
- **[Tell from GraphStage]** `IActorRef.Tell` is thread-safe and fire-and-forget. GraphStage callbacks are single-threaded per stage. Calling `Tell` from `OnPush`/`OnTimer` is safe.
- **[FakeTimeProvider divergence in tests]** The re-check in OnTimer handles this. Tests use high rates for short real delays, or real `TimeProvider` when precise timing is not the test's concern.
- **[Upstream completion with parked element]** The `_willStop` flag defers `CompleteStage()` until the parked element is released. Matches the Akka Throttle behavior.

## Open Questions

(none)
