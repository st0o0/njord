# deterministic-test-infrastructure Specification

## Purpose

Shared TestKit configuration and conventions ensuring all actor and stream tests are 100% deterministic — no wall-clock dependencies, timing races, or non-reproducible timestamps.
## Requirements
### Requirement: TestKit timefactor configuration
All Akka TestKit-derived test classes SHALL configure `akka.test.timefactor = 3` so that all TestKit timeouts (ExpectMsg, ExpectNoMsg, FishForMessage, Within, AwaitAssert, AwaitCondition) scale automatically for slower CI environments. The dilation SHALL apply to the TestKit defaults and to every explicit duration passed to these waits. The timefactor value SHALL be defined once in the shared test timeout constants (`TestTimeouts`) and the shared TestKit configuration SHALL read it from there.

#### Scenario: Default timeout scales with timefactor
- **WHEN** a test calls `ExpectMsgAsync<T>()` without an explicit timeout
- **THEN** the effective timeout SHALL be 9 seconds (default 3s × timefactor 3)

#### Scenario: ExpectNoMsgAsync scales with timefactor
- **WHEN** a test calls `ExpectNoMsgAsync(TimeSpan.FromMilliseconds(500))`
- **THEN** the effective wait SHALL be 1500ms (500ms × timefactor 3)

#### Scenario: Explicit AwaitAssert bound scales with timefactor
- **WHEN** a test passes an explicit duration to `AwaitAssertAsync` or `AwaitConditionAsync`
- **THEN** the effective wait SHALL be that duration × timefactor 3

#### Scenario: Configured timefactor matches the shared constant
- **WHEN** a TestKit is configured through the shared timefactor configuration
- **THEN** its effective timefactor SHALL equal the shared timefactor constant

### Requirement: No wall-clock TimeProvider in tests
All test code SHALL use `Microsoft.Extensions.Time.Testing.FakeTimeProvider` with a fixed seed timestamp instead of `TimeProvider.System`. The standard fixed epoch SHALL be `new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero)`.

#### Scenario: Token bucket refill test uses FakeTimeProvider
- **WHEN** a test needs to verify time-based token refill in `GatewayBudgetGate`
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

### Requirement: Dilated waits stay below the xUnit timeout
Every Akka wait a hosted spec makes SHALL end, after dilation, well before the xUnit timeout that wraps it, so that a failing wait reports its descriptive Akka assertion instead of being killed by xUnit. The shared bound for explicit `AwaitAssert`/`AwaitCondition`/`ExpectMsg` waits (`TestTimeouts.AwaitAssertMax`) SHALL be an un-dilated value whose dilated duration is at least 10 seconds below the hosted xUnit timeout (`TestTimeouts.Hosted`). Waits that TestKit does not dilate (task waits, manual deadline loops) SHALL use a separate real-time bound (`TestTimeouts.RealTimeWaitMax`) equal to the dilated `AwaitAssertMax`. Specs with dilated waits SHALL use `TestTimeouts.Hosted` instead of a literal xUnit timeout. A guard spec SHALL enforce these relations.

#### Scenario: Dilated AwaitAssertMax stays below Hosted
- **WHEN** `TestTimeouts.AwaitAssertMax` is dilated by the configured timefactor
- **THEN** the result plus a 10-second margin SHALL NOT exceed `TestTimeouts.Hosted`

#### Scenario: Real-time bound stays below Hosted
- **WHEN** a spec bounds a non-dilated wait with `TestTimeouts.RealTimeWaitMax`
- **THEN** that bound SHALL equal the dilated `AwaitAssertMax` and, plus a 10-second margin, SHALL NOT exceed `TestTimeouts.Hosted`

#### Scenario: Dilated TestKit default stays below Hosted
- **WHEN** a hosted spec makes a TestKit wait without an explicit duration
- **THEN** the dilated default (9 s) plus a 10-second margin SHALL NOT exceed `TestTimeouts.Hosted`

#### Scenario: Wrong constants fail the guard
- **WHEN** `TestTimeouts.AwaitAssertMax` is set so that its dilated value exceeds the budget (e.g. 15 s dilated to 45 s against a 30 s `Hosted`)
- **THEN** the guard spec SHALL fail with a message naming the dilated value and `Hosted`

