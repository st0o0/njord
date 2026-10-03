## Context

See proposal.md for motivation. Evidence comes from runs in a throwaway worktree at 628b880 (logs were under `/tmp/claude-1000/-home-st0o0-GIT-njord/flake/`, not kept in the repo). Machine: 8 cores.

### Measured failure rates

| Scenario | Runs | Red runs | Failed tests per red run |
|---|---|---|---|
| Sequential, idle machine (first three runs after build were slow: 70-87 s; later runs 52-63 s) | 8 | 3 (runs 1-3; runs 4-8 green) | 1-2 |
| 7 busy-loop processes on 8 cores | 4 | 4 | 5-6 (suite time 122-128 s vs 52 s) |
| Single class under `taskset -c 0` | 2 each | GetPollStatesSpec 2/2 (1 of 4 tests), ActorKeyRegistrationSpec 2/2 (1 of 13), HealthEndpointSpec 2/2 (2 of 3); SchedulerActorSnapshotSpec 0/2, BudgetTrackerActorSpec 0/2 | |

Failure frequency across all 12 full runs: `SchedulerActorGetPollStatesSpec.Get_poll_states_returns_all_configured_models` 7/12, `ActorKeyRegistrationSpec.Registry_resolves_every_actor_marker(ISchedulerActor)` 6/12, `HealthEndpointSpec.Unknown_path_returns_404` 4/12 (all load runs), `SchedulerActorGetPollStatesSpec.Get_poll_states_reflects_discovery_phase_initially` 4/12, `SchedulerActorSnapshotSpec.State_recovers_from_snapshot_after_restart` 3/12, `SchedulerActorGetPollStatesBeforeReadySpec.Get_poll_states_responds_while_waiting_for_pipeline_refs` 1/12, `BudgetTrackerActorSpec.Recovery_skips_events_from_previous_month` 1/12. The previously reported `SchedulerActorStartupOrderSpec.Scheduler_reaches_ready_and_initializes_states` did not fail in these 12 runs but has the same structure (cause A). Every full run also printed one "Errors: 1" (test cleanup failure from the cancelled `ExpectMsgAsync` of a timed-out test), a follow-on of cause A.

### Causes

**A. xUnit `Timeout` shorter than the Akka wait it wraps (timing assumption, short windows).**
`AddTestTimefactor()` sets `akka.test.timefactor = 3`, so TestKit's default `ExpectMsg` (3 s) and `AwaitAssert` (3 s) windows dilate to 9 s, but the specs carry `[Fact(Timeout = 5000)]`. xUnit kills the test at 5 s with "Test execution timed out after 5000 milliseconds" (then a cleanup failure from the cancelled TestKit receive) before the Akka-level wait can finish. Affected: `SchedulerActorGetPollStatesSpec` (all four facts call `ExpectMsg<WeightedTarget>` twice before asking), `SchedulerActorGetPollStatesBeforeReadySpec`, `ActorKeyRegistrationSpec` (`AwaitAssertAsync` with default 9 s inside a 5 s `Timeout`; only the first theory row, `ISchedulerActor`, pays the cold cost: it waits for the scheduler's first pipeline resolve), `SchedulerActorSnapshotSpec` (51 sequential `Ask`s at 2 s each inside 10 s), `SchedulerActorStartupOrderSpec` (3 s `AwaitCondition` + 1 s `Ask` inside 5 s). Under `taskset -c 0` the same tests fail deterministically, so this is the red-first reproducer. The slow part is host/actor-system startup, JIT of Akka.Streams/persistence on the first test of a run, and thread-pool starvation; it is largest at the beginning of a run, which matches "first run after build is red".

**B. Real wall-clock retry delays.** `StreamConsumerActor.ScheduleRetryResolve()` uses `min(2^n, 30)` s real scheduler time (`Njord.Core/Actors/StreamConsumerActor.cs:65`); `SchedulerActor.RetryPipelineResolve` uses the same formula. Specs from `akka-failure-hygiene` wait for the first or second retry with `ExpectMsgAsync(TimeSpan.FromSeconds(4))` inside `[Fact(Timeout = 5000)]` (`Egress/ModelStateActorSpec.cs:73,87`, `Mqtt/MqttEgressActorSpec.cs:165,179`, `Mqtt/DiscoveryActorSpec.cs:119,133`, `Grpc/GrpcSnapshotConsumerTerminatedSpec.cs:62,76`, `Pipeline/SchedulerActorRefFailureSpec.cs:63`). These did not fail in the 12 runs but have at most 1 s of slack against a 1 s + 2 s backoff chain and are the next to break; they also add roughly 25 s of pure waiting to the suite.

**C. Ordering assumption after `GracefulStop` (race).** `BudgetTrackerActorSpec.Recovery_skips_events_from_previous_month` stops the actor named `stale-month-test`, then immediately `ActorOf`s the same name. `GracefulStop` completes on the `Terminated` delivered to its watcher, which can happen before the parent has released the child name, so the second `ActorOf` throws `InvalidActorNameException: Actor name "stale-month-test" is not unique!` (seen once under load). Persistence id is the constant `budget-tracker` (`Njord.Pipeline/BudgetTrackerActor.cs:13`), so the actor name is irrelevant to recovery and can be unique per incarnation.

**D. HTTP host start in the fixture constructor.** `HealthEndpointSpec` builds `WebApplicationFactory<Program>` and `CreateClient()` in the constructor; the first request in the class also triggers host startup (Serilog, Akka.Hosting, gRPC, Servus startup gates), inside a 5 s `Timeout`. Failures: `Unknown_path_returns_404` (4/4 load runs), 2 of 3 facts under `taskset -c 0`.

**Not found:** shared static state, port or file collisions, or `FakeTimeProvider` misuse were not implicated in the observed failures. (MQTT connection attempts to `localhost:1883` in specs only log; they do not fail tests.)

## Goals / Non-Goals

**Goals:** green under contention (`taskset -c 0`, 7 busy loops on 8 cores) and 30 consecutive clean runs, without hiding real failures.

**Non-Goals:** see proposal.md; additionally no change to production retry policy (default backoff stays `min(2^n, 30)` s).

## Decisions

### 1. Make the xUnit `Timeout` an outer safety net that exceeds the Akka waits
The inner waits (TestKit `ExpectMsg`, `AwaitAssert`, `Ask`) already scale with `akka.test.timefactor`. For every class in cause A, set the xUnit `Timeout` to at least the sum of its inner bounds (e.g. 30 000 for scheduler hosting specs), so the Akka wait fails with a descriptive message first and an idle machine is not slowed (a passing test returns immediately). Put the value in one shared constant (`TestTimeouts.Hosted`) in `Njord.Tests.Shared` to avoid magic numbers.
*Rejected:* blanket raising every `Timeout` (hides hangs, slows genuine failures); `RetryFact`-style attributes (hides flakiness, explicitly out of scope).

### 2. Replace "sleep until ready" with explicit readiness signals
Where a spec only needs the scheduler to be past discovery, wait on one deterministic signal with `AwaitAssertAsync(..., TimeSpan.FromSeconds(15))` against `QueryPollStates` instead of counting two `ExpectMsg<WeightedTarget>` offers plus 2 s `Ask`s; in `SchedulerActorSnapshotSpec` send the 51 `HashResult`s without per-message 2 s asks serialised inside a tight outer limit (keep the asks, widen the bounds per decision 1). `ActorKeyRegistrationSpec` keeps `AwaitAssertAsync` but with an explicit `max:` and a theory-level shared startup so each row does not re-pay it.

### 3. Fix the name-reuse race without sleeping
`BudgetTrackerActorSpec.Recovery_skips_events_from_previous_month` uses a fresh unique name for the recovered actor (persistence id is constant, so recovery is still exercised). `CreateActor(name)` stays for tests that need a name; add an assertion that both incarnations share `budget-tracker` persistence only through behavior (month rollover result).
*Rejected:* `Task.Delay` between stop and restart.

### 4. Shorten the retry backoff through configuration, not real waiting
Akka test configuration alone is not enough because the delay is computed in code. Introduce a small `RetryBackoff` setting read by `StreamConsumerActor` and `SchedulerActor`: a defaulted constructor/`protected virtual` hook `RetryDelay(int attempt)` whose production default is `min(2^n, 30)` s. Specs override it to `TimeSpan.FromMilliseconds(10)` through a test subclass or an options value registered in the test host, so retry specs assert the second request within the normal 3 s dilated window and run in milliseconds. Production behavior and the AGENTS.md "reuse existing backoff" rule stay unchanged; add one characterization spec asserting the default sequence 1, 2, 4, ... 30 s using `FakeTimeProvider`-independent arithmetic (pure function test).
*Alternatives:* inject `IScheduler` fake (heavier, Akka's `TestScheduler` needs config on every spec); keep real waits and widen `Timeout` (rejected: slow and still racy).
Open choice for implementation: virtual hook vs options. Default to the virtual hook (no DI/config surface) unless the options route is cheaper in `SchedulerActor`.

### 5. Warm the heavy host once per class, not per fact
`HealthEndpointSpec`: do `CreateClient()` plus one warm-up request in `InitializeAsync` (xUnit v3 `IAsyncLifetime`) so the host start is not charged to a fact's `Timeout`, and set the fact `Timeout` per decision 1.

### 6. Collection/parallelism limits are a last resort
Only if after decisions 1-5 the 30-run loop still shows failures attributable to starvation, cap `maxParallelThreads` for the assembly. Not planned; documented so it is not the first reflex.

## Risks / Trade-offs

- [Larger `Timeout`s let a real hang run longer] -> inner Akka waits still fail fast at their own bounds; the outer limit is only a backstop.
- [Test-only hook leaks into production code] -> default delegates to the existing formula; characterization spec pins it.
- [Cause analysis based on 12 full runs] -> the 30-run acceptance loop and the load/taskset reproducers are the real check; new causes found there are added as tasks before closing.

## Migration Plan

Tests only (plus the defaulted hook). Land per test class, each commit leaves the suite green. Rollback: `git revert` per commit.

## Open Questions

- Hook vs options for the backoff override (decision 4). Does not change what is built at task level; resolved during task 3.1.
