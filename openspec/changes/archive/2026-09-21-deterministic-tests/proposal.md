## Why

CI tests fail intermittently on GitHub Actions runners while passing locally. The root cause is wall-clock timing dependencies throughout the test suite: `TimeProvider.System`, `Thread.Sleep`, `Task.Delay` races, and Akka TestKit default timeouts that are too tight for slower CI environments. This has been an ongoing issue across multiple fix attempts and currently produces 82 failed CI runs — all on code that passes locally.

## What Changes

- Set `akka.test.timefactor = 3` in a shared TestKit base configuration so all default TestKit timeouts scale automatically for CI
- Replace `TimeProvider.System` with `FakeTimeProvider` in all test files (~15 sites) and use `Advance()` instead of `Thread.Sleep` / `Task.Delay`
- Replace `Task.Delay` / `Task.WhenAny` timing races in `StreamConsumerActorSpec` and `SchedulerActorSpec` with deterministic signals (TestProbe, AwaitCondition, TaskCompletionSource)
- Replace all `DateTimeOffset.UtcNow` in test data construction (~10 sites) with fixed deterministic values
- Add `TimeProvider` injection seam to `SensorGrpcService` and remove `TimeProvider.System` fallback from `HistoryAnalyzer`
- Widen `ExpectNoMsgAsync` windows that are currently 100–300ms

## Non-goals

- Changing production behavior or features
- Restructuring the test project layout
- Adding new tests — this is purely about making existing tests deterministic
- Addressing the `hadolint/hadolint-action@v3` CI infrastructure issue (separate concern)
- No API-budget impact — this change does not alter polling

## Capabilities

### New Capabilities

- `deterministic-test-infrastructure`: Shared TestKit base configuration (timefactor, default timeouts) and conventions for deterministic time control in all actor and stream tests

### Modified Capabilities

- `sensor-hub`: `SensorGrpcService` needs `TimeProvider` injection instead of `DateTimeOffset.UtcNow` fallback

## Impact

- **Test code**: ~25 test files across `Njord.Tests/` need timing-related changes
- **Production code**: `SensorGrpcService.cs` (add `TimeProvider` parameter), `HistoryAnalyzer.cs` (remove `TimeProvider.System` fallback)
- **Shared test infrastructure**: `Njord.Tests.Shared/` may gain a shared HOCON config or base class for timefactor
- **CI**: Expected result is zero flaky test runs from timing dependencies
