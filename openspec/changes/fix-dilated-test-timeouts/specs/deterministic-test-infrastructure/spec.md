## MODIFIED Requirements

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

## ADDED Requirements

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
