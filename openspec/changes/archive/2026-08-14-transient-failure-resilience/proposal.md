# Transient Failure Resilience for Poll Scheduler

## Problem

`ModelPollState.WithTransientFailure` has two defects discovered during a real
network outage (2026-08-13 23:00 – 2026-08-14 04:00, arpege_europe/Vreden):

### 1. No fallback to Discovery

`WithMiss` falls back to Discovery after 5 consecutive misses.
`WithTransientFailure` never falls back — it retries at the 15-minute cap
indefinitely. During a sustained outage this is not harmful per se, but it
means the system never re-enters the Discovery cadence (20 min) which is
more polite to the API during extended failures.

### 2. MissCount poisoning (the real bug)

`WithTransientFailure` increments the shared `MissCount` without limit.
When the network recovers and the first successful fetch returns unchanged
data, `WithMiss` sees `newMissCount = N+1` (where N may be 20+) and
immediately triggers fallback to Discovery — wiping the learned cycle.

A 30-minute network blip can destroy an 8-hour cycle that took a full day
to learn. The cycle is perfectly valid (model update schedules don't change
because your network went down), but it gets thrown away due to counter
contamination.

## Solution (Option C)

Separate the transient failure counter from the data-miss counter so they
cannot interfere. Add a fallback for transient failures that caps the retry
interval at `discoveryInterval` but **preserves** the learned `Phase` and
`Cycle`.

### Design

Add a `TransientFailureCount` field to `ModelPollState` (int, default 0).

**`WithTransientFailure`:**
- Increments `TransientFailureCount` (not `MissCount`).
- Uses `RetryBackoff(transientFailureCount)` for the delay.
- After `MaxTransientBeforeThrottle` (e.g. 5) consecutive failures, caps
  the delay at `discoveryInterval` instead of `MaxRetryBackoff` — more
  polite to the API, acknowledges the issue is persistent.
- Does NOT touch `Phase`, `Cycle`, or `MissCount`.

**`WithMiss`:** unchanged — still uses `MissCount` only.

**`WithDataChange`:** resets both `MissCount` and `TransientFailureCount`
to 0 (successful fetch = clean slate).

**Recovery path:** After a network outage, the first successful fetch
either finds changed data (`WithDataChange` → reset both counters, cycle
may be recomputed) or unchanged data (`WithMiss` → `MissCount` goes from
0 to 1, normal backoff sequence, cycle is safe).

### What does NOT change

- The `DataChanged` persistence event and its DTO — no schema change.
- `WithMiss` logic, `RetryBackoff`, `MaxRetryBackoff`.
- `SchedulerActor` call sites — they already distinguish `WithTransientFailure`
  from `WithMiss`; only `ModelPollState` internals change.

## Scope

- `ModelPollState.cs` — add field, modify `WithTransientFailure` and
  `WithDataChange`
- `ModelPollStateSpec.cs` — new tests for transient failure isolation,
  MissCount poisoning prevention, throttle cap after N failures
- No config changes (threshold is a const like `MaxMissesBeforeFallback`)
