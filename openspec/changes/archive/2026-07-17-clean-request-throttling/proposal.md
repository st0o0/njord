## Why

The pipeline currently has two uncoordinated rate-limiting mechanisms: the SchedulerActor staggers initial polls with a fixed `1 + i*2s` delay (producing strictly sequential requests ~2s apart), while the PipelineActor applies a weighted Akka.Streams `Throttle` at 80% of the per-minute budget (480/min = 8 req/sec). The Throttle never activates because the stagger already limits throughput to 0.5 req/sec. With 27 configured models the startup discovery phase takes 54 seconds of sequential requests with ~30ms actual work each. Replacing these two layers with a single, explicit politeness throttle (2 req/sec) makes the rate limiting predictable, cuts startup time to ~14s, and keeps the service well below Open-Meteo's free-tier limits.

## What Changes

- Remove the startup stagger logic (`1 + i*2s` delay) from `SchedulerActor.InitializeStates()` — all initial polls are offered immediately.
- Replace the budget-percentage-based `Throttle(budgetPerMinute, 1 min, burst: 4)` in `PipelineActor` with a fixed-rate throttle of **2 elements/sec, burst 4**.
- Reduce `SelectAsyncUnordered` parallelism from 4 to 2 to match the throttle rate and limit concurrent connections to Open-Meteo.
- The throttle rate (2 req/sec = 120 req/min) stays at 20% of the free-tier limit (600/min), leaving 480 req/min headroom.

## Non-goals

- Changing steady-state poll scheduling (cycle learning, miss backoff, discovery interval) — those remain as-is.
- Making the throttle rate configurable — a hardcoded politeness rate is intentional; the budget override mechanism controls monthly/minute ceilings separately.
- Batching multiple models into a single multi-model API request — that is a separate optimisation.

## API-budget impact

No change to request volume. The same 27 models are polled at the same intervals. Only the timing of the initial burst changes: requests that were spread over 54s now complete in ~14s. Steady-state budget usage is unchanged (~27 requests per poll cycle, well within 300k/month).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `poll-pipeline`: Throttle changes from budget-percentage-based weighted throttle to a fixed 2 req/sec politeness rate; `SelectAsyncUnordered` parallelism reduced from 4 to 2.
- `poll-scheduler`: Startup stagger removed — initial polls are offered to the pipeline immediately, relying on the pipeline throttle as the single rate-limiting gate.

## Impact

- `Njord/Pipeline/PipelineActor.cs` — throttle and parallelism parameters change.
- `Njord/Pipeline/SchedulerActor.cs` — `InitializeStates()` simplified (no stagger calculation).
- `Njord.Tests/Pipeline/SchedulerActorSpec.cs` — tests that assert stagger timing need updating.
- Existing poll-pipeline and poll-scheduler specs get delta spec files reflecting the new behaviour.
