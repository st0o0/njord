## Why

`PipelineConnectionSpec.Requests_have_no_serialization_gap_from_scheduler` fails
reproducibly on Linux (first gap reported as 1.2-7 s against a < 1 s limit).
Investigation shows the pipeline is not slow: the measured gap is a unit bug in
the test (Stopwatch ticks are interpreted as 100 ns `TimeSpan` ticks), so the
test currently guards nothing and fails depending on machine speed.

## What Changes

- Fix the time conversion in the spec so gaps are measured in real milliseconds
  (`Stopwatch.GetElapsedTime(start, end)` instead of dividing raw Stopwatch
  ticks by `TimeSpan.TicksPerMillisecond`).
- No production code changes. The `akka-failure-hygiene` retry/backoff paths are
  not involved and need no change.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. No spec-level requirement changes (`skip_specs: true`).

## Impact

- `src/Njord.Tests/Pipeline/PipelineConnectionSpec.cs` only.
- API budget: 0 requests (test-only change, fake client, no polling change).

## Non-goals

- Changing scheduler, pipeline, stream-consumer retry or backoff behavior.
- Loosening the < 1000 ms threshold to make the test pass.
- Reworking the other tests in `PipelineConnectionSpec`.
