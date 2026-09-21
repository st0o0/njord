# deterministic-test-infrastructure Specification

## Purpose

Shared TestKit configuration and conventions ensuring all actor and stream tests are 100% deterministic — no wall-clock dependencies, timing races, or non-reproducible timestamps.

## Requirements

### Requirement: TestKit timefactor configuration
All Akka TestKit-derived test classes SHALL configure `akka.test.timefactor = 3` so that all default TestKit timeouts (ExpectMsg, FishForMessage, Within, AwaitCondition) scale automatically for slower CI environments.

#### Scenario: Default timeout scales with timefactor
- **WHEN** a test calls `ExpectMsgAsync<T>()` without an explicit timeout
- **THEN** the effective timeout SHALL be 9 seconds (default 3s × timefactor 3)

#### Scenario: ExpectNoMsgAsync scales with timefactor
- **WHEN** a test calls `ExpectNoMsgAsync(TimeSpan.FromMilliseconds(500))`
- **THEN** the effective wait SHALL be 1500ms (500ms × timefactor 3)

### Requirement: No wall-clock TimeProvider in tests
All test code SHALL use `Microsoft.Extensions.Time.Testing.FakeTimeProvider` with a fixed seed timestamp instead of `TimeProvider.System`. The standard fixed epoch SHALL be `new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero)`.

#### Scenario: Token bucket refill test uses FakeTimeProvider
- **WHEN** a test needs to verify time-based token refill in `WeightedBudgetGate`
- **THEN** it SHALL use `FakeTimeProvider.Advance()` instead of `Thread.Sleep`

#### Scenario: No custom FakeTimeProvider shims
- **WHEN** a test class needs a controllable time source
- **THEN** it SHALL use `Microsoft.Extensions.Time.Testing.FakeTimeProvider`, not a locally-defined shim class

### Requirement: No wall-clock timestamps in test data
Test data construction SHALL use fixed `DateTimeOffset` values or `FakeTimeProvider.GetUtcNow()` instead of `DateTimeOffset.UtcNow`.

#### Scenario: CycleId uses fixed timestamp
- **WHEN** a test constructs a `CycleId` for test data
- **THEN** it SHALL use a fixed `DateTimeOffset` value, not `DateTimeOffset.UtcNow`

#### Scenario: ForecastPoint uses fixed timestamp
- **WHEN** a test constructs a `ForecastPoint` for test data
- **THEN** it SHALL use a fixed `DateTimeOffset` value, not `DateTimeOffset.UtcNow`

### Requirement: No timing races in test assertions
Tests SHALL NOT use `Task.Delay` or `Task.WhenAny` with real-time delays for assertion timing. Instead, tests SHALL use deterministic signals: `AwaitConditionAsync`, `TaskCompletionSource`, `TestProbe`, or `ExpectMsgAsync`.

#### Scenario: Tight-loop detection without Task.Delay
- **WHEN** a test needs to verify an actor does not enter a tight retry loop
- **THEN** it SHALL use `AwaitConditionAsync` to poll the expected condition with a generous timeout, not `Task.Delay` with a fixed window

#### Scenario: Retry verification without Task.WhenAny race
- **WHEN** a test needs to verify an actor retries after a scheduled delay
- **THEN** it SHALL use `AwaitConditionAsync(() => tcs.Task.IsCompleted)` with a timeout that is generous enough for slow CI, not `Task.WhenAny(tcs, Task.Delay(ms))`

### Requirement: ExpectNoMsgAsync minimum window
`ExpectNoMsgAsync` calls SHALL use a minimum window of 500ms to avoid false negatives on slow CI runners.

#### Scenario: No sub-500ms ExpectNoMsgAsync
- **WHEN** a test asserts that no message arrives within a time window
- **THEN** the window SHALL be at least 500ms (before timefactor scaling)
