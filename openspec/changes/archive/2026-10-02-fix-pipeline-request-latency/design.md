## Context

See proposal.md. The spec records `Stopwatch.GetTimestamp()` per fetch and
computes `gapMs = (t[i] - t[i-1]) / TimeSpan.TicksPerMillisecond`.
`Stopwatch.Frequency` is 1e9 on Linux (1 tick = 1 ns), while
`TimeSpan.TicksPerMillisecond` is 10 000 (1 tick = 100 ns). Every reported
"millisecond" is therefore 100x the real value: the 1000 ms limit really
asserts < 10 ms, and a real gap of 18 ms reports as 1800 ms.

## Root cause (evidence)

- Temporary instrumentation in a throwaway worktree at c721152 (Environment.TickCount64
  at refs received, initial offer, ConnectionEstablished, each offer, each fetch):
  refs received -> first FETCH = 144 ms, last FETCH 64 ms later, all 8 fetches
  within ~210 ms. The same runs reported `Max gap ... 6945ms`, `6891ms`, `7096ms`
  (real gap ~65 ms; 65 ms * 1e6 ns / 1e4 = 6500+).
- No retry/backoff path runs: no `RetryPipelineResolve`, `RetryResolve` or
  failure warnings appear in the log. `ScheduleRetryResolve` and
  `_pipelineRetryCount` are only reached on failed refs or a dead pipeline ref.
  First resolve still sends `RequestPipelineSink/Source` immediately
  (SchedulerActor `WaitingForPipeline`).
- Bisect (worktree, 4 runs each): 62f1536 (before akka-failure-hygiene) already
  fails 1/4 (1038 ms), c162a8a 4/4, e81792a 0/1, fee13a6 1/4. The pass/fail
  outcome is flaky and follows scheduling/JIT jitter around the effective
  10 ms limit, not the commits. The change merely shifted timing slightly.

Conclusion: test assumption bug (platform-dependent unit), not a production
regression.

## Decisions

- Compute gaps with `Stopwatch.GetElapsedTime(previous, next).TotalMilliseconds`
  (portable across Stopwatch frequencies). Keep the 1000 ms limit: the original
  bug was ~1.5 s of actor serialization, and real gaps are ~10-70 ms (cold JIT),
  so the limit keeps its meaning with ample margin.
- Rejected: dividing by `Stopwatch.Frequency` manually (reimplements
  `GetElapsedTime`).
- Rejected: raising the limit or deleting the assertion (hides the unit bug and
  weakens the guard; slopwatch-style shortcut).
- Rejected: touching the retry backoff in SchedulerActor/StreamConsumerActor
  (not on this path, no evidence of regression).
- Rejected: converting to `FakeTimeProvider`; the test measures real actor
  serialization latency, so real time is intended.

## Risks / Trade-offs

- [Cold-start JIT makes first gap up to a few hundred ms on slow CI] -> margin
  vs 1000 ms is ~10x locally; revisit only if it flakes after the fix.

## Open Questions

None.
