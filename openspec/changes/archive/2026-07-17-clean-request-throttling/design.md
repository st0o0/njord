## Context

The pipeline has two independent rate-limiting layers that don't coordinate:

1. **SchedulerActor stagger** (`InitializeStates`): assigns each (location, model) pair an initial delay of `1 + i*2` seconds. With 27 models this means the last request fires after 53s, producing strictly sequential HTTP calls ~2s apart.
2. **PipelineActor Throttle**: `Throttle(budgetPerMinute, 1 min, burst: 4, weighted, Shaping)` where `budgetPerMinute = 80% * 600 = 480`. At 8 req/sec this never activates because the stagger only delivers 0.5 req/sec.

Additionally, `SelectAsyncUnordered(4)` allows 4 concurrent HTTP calls, but the stagger ensures only one is ever in flight.

The result: 27 discovery requests take 54s at startup, each with ~30-90ms actual HTTP time and ~1.9s idle wait.

## Goals / Non-Goals

**Goals:**
- Single source of truth for rate limiting: the Akka.Streams `Throttle` operator in PipelineActor.
- Fixed politeness rate of 2 req/sec (120 req/min = 20% of free-tier limit), independent of the budget configuration.
- Startup discovery completes in ~14s instead of ~54s.
- Maximum 2 concurrent connections to Open-Meteo.

**Non-Goals:**
- Making the politeness rate configurable (intentionally hardcoded).
- Changing steady-state scheduling logic (cycle learning, miss backoff, discovery interval).
- Multi-model request batching.

## Decisions

### Decision 1: Fixed rate throttle instead of budget-percentage

**Choice**: `Throttle(2, TimeSpan.FromSeconds(1), maximumBurst: 4, ThrottleMode.Shaping)` — a simple element-count throttle at 2/sec with burst 4.

**Why not budget-percentage**: The 80% factor made the throttle rate dependent on `EffectiveBudget`, which could change via `BudgetOverride`. A user setting a low budget override (e.g., 60 req/min) would tighten the throttle to 0.8 req/sec — reasonable but surprising. Conversely, a high override would blast Open-Meteo. A fixed politeness rate decouples "how fast we hit the API" from "how many requests we allow per month", which are separate concerns.

**Why not weighted**: The current weight calculation (`ceil(hourlyVars/10) * ceil(days/14)`) yields weight 1 for njord's default config (9 vars, 4 days). Weighted throttle adds complexity for no practical benefit at weight 1. If the weight ever exceeds 1, the fixed rate still provides adequate protection (2 weighted-units/sec is even more conservative).

**Alternative considered**: Keep weighted throttle but at a fixed rate. Rejected because weight 1 is the only realistic value and the weighted overload of `Throttle` requires more parameters.

### Decision 2: Remove stagger, don't replace it

**Choice**: `InitializeStates()` sets `NextPollUtc = now` for all new models (no stagger delay). All targets are offered to the queue immediately; the pipeline's Throttle shapes them.

**Why**: The stagger was a workaround for the pipeline having no effective throttle. With a proper 2/sec throttle, the queue's `OverflowStrategy.Backpressure` and the Throttle operator handle burst shaping. The `Source.Queue(32, Backpressure)` buffer is large enough for 27 initial targets.

### Decision 3: Reduce SelectAsyncUnordered from 4 to 2

**Choice**: `SelectAsyncUnordered(2)` to match the throttle rate.

**Why**: At 2 req/sec with ~30-90ms response times, 2 concurrent slots are sufficient. 4 slots would only help if responses took >500ms, which would indicate a problem rather than normal operation. Fewer concurrent connections is politer to a free API.

## Risks / Trade-offs

- **[Risk] Startup queue pressure**: 27 elements offered simultaneously into a 32-slot backpressure queue. The queue can hold them all (27 < 32), and the Throttle drains at 2/sec. If model count grows beyond 32, the `OfferAsync` calls will await backpressure — correct behaviour, no data loss.
  **Mitigation**: Queue size of 32 is adequate. If model count grows significantly, increase the queue buffer.

- **[Risk] Burst of 4 at startup**: The `maximumBurst: 4` allows 4 requests in the first second before settling to 2/sec. This is intentional — it speeds up the first few responses — and still far below the 600/min limit.
  **Mitigation**: None needed; 4 is well within tolerance.

- **[Trade-off] Slower than possible**: At 2/sec, startup takes ~14s for 27 models. Budget-wise, 8/sec would be safe (~4s startup). We choose politeness over speed — 14s is acceptable for a service that polls hourly.
