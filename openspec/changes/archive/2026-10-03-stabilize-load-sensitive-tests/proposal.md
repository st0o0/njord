## Why

The 815-test suite is not stable under CPU contention. Measured on the committed tree at 628b880 (8 cores, `dotnet run --no-build`, full suite from `src/`): 3 of 8 sequential runs failed (the first three runs after the build; runs 4-8 were green, 0 failures), and 4 of 4 runs with 7 busy-loop processes on the 8 cores failed (5-6 failing tests each). Agents saw the same pattern as "first run red, rerun green" and "red while `docker build` runs". Failures are timing assumptions, not logic bugs, but they erode trust in the suite and invite blanket rerun habits.

## What Changes

- Make the load-sensitive specs deterministic or give them bounds that scale with load, per cause (see design.md): xUnit `Timeout` values that cut off Akka `AwaitAssert`/`ExpectMsg` windows, a name-reuse race after `GracefulStop`, cold-start-sensitive host startup, and wall-clock retry waits.
- Add a test-only way to shorten the requester retry backoff so retry specs stop waiting up to 4 s of real time.
- Tests only. No production behavior change, except (if chosen in design decision 4) a defaulted, internally-injected backoff parameter on `StreamConsumerActor` / `SchedulerActor`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. This change alters tests only; no requirement changes (`skip_specs: true`).

## Non-goals

- No CI change (discuss separately).
- No test deletion or disabling; no `[Fact(Skip=...)]`.
- No retry / rerun-on-fail attributes or runner flags.
- No blanket increase of every `Timeout`.
- Not redoing the already fixed Stopwatch tick conversion test bug.

## Impact

- Affected: `src/Njord.Tests/**` (Pipeline, Configuration, Health, Egress, Mqtt, Grpc, Actors, Enrichment specs), `src/Njord.Tests.Shared/TestTimefactorConfig.cs`, possibly `src/Njord.Core/Actors/StreamConsumerActor.cs` (injectable backoff).
- API budget: 0 requests (no Open-Meteo calls; tests only).
- Acceptance: 30 consecutive full-suite runs with 0 failures (about 35 min at ~65 s per run), plus the red-first stress commands below going green.
