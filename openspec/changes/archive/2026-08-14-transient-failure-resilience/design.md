# Design: Transient Failure Resilience

## ModelPollState record change

```
Current:  ModelPollState(LastHash, LastChangeUtc, PrevChangeUtc, NextPollUtc, MissCount, Phase, Cycle)
Proposed: ModelPollState(LastHash, LastChangeUtc, PrevChangeUtc, NextPollUtc, MissCount, Phase, Cycle, TransientFailureCount)
```

`TransientFailureCount` defaults to 0 in `Initial()`.

## WithTransientFailure — new behavior

```
WithTransientFailure(now, discoveryInterval):
  tfc = TransientFailureCount + 1
  delay = tfc >= MaxTransientBeforeThrottle
        ? discoveryInterval           ← polite cap after sustained failure
        : RetryBackoff(tfc)           ← exponential: 1, 2, 4, 8, 15m
  return this with { TransientFailureCount = tfc, NextPollUtc = now + delay }
```

Key properties:
- `MissCount` is untouched → no poisoning of `WithMiss` fallback logic
- `Phase` and `Cycle` are untouched → learned cycle survives the outage
- After 5 transient failures: retries at `discoveryInterval` (20m) instead
  of `MaxRetryBackoff` (15m) — slightly slower, more polite

## WithDataChange — reset both counters

```
return new ModelPollState(
    ...,
    MissCount: 0,
    TransientFailureCount: 0,   ← NEW
    ...);
```

## WithMiss — unchanged

Still reads/writes only `MissCount`. After recovery from a transient failure
period, the first miss starts fresh at `MissCount = 1`.

## State machine visualization

```
                    ┌─────────────────────────────────────────────┐
                    │              STEADY                          │
                    │  Cycle = learned, Phase = Steady             │
                    │                                              │
                    │  WithMiss:                                   │
                    │    MissCount 1→2→3→4 → backoff 1,2,4,8m     │
                    │    MissCount 5 → fallback to Discovery       │
                    │                                              │
                    │  WithTransientFailure:                       │
                    │    TFC 1→2→3→4 → backoff 1,2,4,8m           │
                    │    TFC 5+ → discoveryInterval (20m)          │
                    │    Phase/Cycle PRESERVED                     │
                    │                                              │
                    │  WithDataChange:                             │
                    │    MissCount=0, TFC=0, cycle recomputed      │
                    └─────────────────────────────────────────────┘
                              │ 5 misses
                              ▼
                    ┌─────────────────────────────────────────────┐
                    │              DISCOVERY                       │
                    │  Cycle = null, Phase = Discovery             │
                    │                                              │
                    │  WithMiss: always discoveryInterval          │
                    │  WithTransientFailure: same as Steady        │
                    │  WithDataChange: may learn cycle → Steady    │
                    └─────────────────────────────────────────────┘
```

## Recovery scenario (the fix in action)

```
Steady, Cycle = 8h, MissCount = 0, TFC = 0

23:22  Network down
23:23  WithTransientFailure → TFC=1, MissCount=0, retry 1m
23:25  WithTransientFailure → TFC=2, MissCount=0, retry 2m
23:29  WithTransientFailure → TFC=3, MissCount=0, retry 4m
23:37  WithTransientFailure → TFC=4, MissCount=0, retry 8m
23:52  WithTransientFailure → TFC=5, MissCount=0, retry 20m  ← polite cap
00:12  WithTransientFailure → TFC=6, MissCount=0, retry 20m
  ...
04:00  Network recovers

Case A — data changed:
04:20  WithDataChange → TFC=0, MissCount=0, cycle recomputed ✓

Case B — data unchanged:
04:20  WithMiss → MissCount=1 (not 21!), retry 1m
04:21  WithMiss → MissCount=2, retry 2m
       ... normal backoff, cycle safe for 5 misses ✓
```

## Persistence

No change to persisted events. `TransientFailureCount` is runtime-only state
(not in `DataChanged` DTO). On recovery, `TransientFailureCount` starts at 0
(same as `MissCount`).

## SchedulerActor impact

The `OnFetchFailed` method already passes `discoveryInterval` for rate-limit
floor logic. `WithTransientFailure` now takes `discoveryInterval` as a
parameter (was parameterless before). Call sites:

- `FetchFailureReason.Transport` → `.WithTransientFailure(now, discoveryInterval)`
- `FetchFailureReason.RateLimited` → `.WithTransientFailure(now, discoveryInterval)` (+ existing floor)
- `FetchFailureReason.ModelUnavailable` → `.WithTransientFailure(now, discoveryInterval)`
- `FetchFailureReason.MalformedPayload` → `.WithTransientFailure(now, discoveryInterval)`
