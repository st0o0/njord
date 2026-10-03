# Delta: Poll Scheduler — Transient Failure Resilience

Parent spec: `openspec/specs/poll-scheduler/spec.md`

## New requirement: Isolated transient failure tracking

### TF-1: Separate counter

`ModelPollState` must track transient failures with a dedicated
`TransientFailureCount` that is independent of `MissCount`.
`WithTransientFailure` must only increment `TransientFailureCount`.
`WithMiss` must only increment `MissCount`.

### TF-2: Cycle preservation

`WithTransientFailure` must never modify `Phase` or `Cycle`.
A learned cycle must survive any number of consecutive transient failures.

### TF-3: Throttle cap

After `MaxTransientBeforeThrottle` (5) consecutive transient failures,
`WithTransientFailure` must cap the retry delay at `discoveryInterval`
instead of `MaxRetryBackoff`.

### TF-4: Clean slate on data change

`WithDataChange` must reset both `MissCount` and `TransientFailureCount`
to 0.

### TF-5: No MissCount poisoning

After N consecutive transient failures followed by a successful fetch
with unchanged data, `WithMiss` must see `MissCount = 0` (not the
accumulated transient failure count). The first miss after recovery
must produce `MissCount = 1` with a 1-minute backoff.
