## Context

See proposal.md - Why. Verified behavior (Akka.Hosting.TestKit 1.5.73, `akka.test.timefactor = 3`
via `AddTestTimefactor()`): a throwaway spec measured `AwaitConditionAsync(..., 1 s)`,
`AwaitAssertAsync(..., 1 s)`, `ExpectMsgAsync(1 s)` and `ExpectNoMsgAsync(1 s)` at ~3.0 s each,
and `Dilated(TestTimeouts.AwaitAssertMax)` at 45 s. TestKit routes explicit durations through
`RemainingOrDilated`, so only the waits TestKit does not own (`Task.WaitAsync`, `Ask`, manual
`DateTimeOffset.UtcNow` deadlines) run un-dilated.

Every TestKit spec in the solution configures the timefactor through `AddTestTimefactor()`
(directly or via `NjordActorSpec`), so the factor is uniformly 3.

Current users of the shared constants:
- `AwaitAssertMax` passed to TestKit waits: `BudgetTrackerSpec`, `ConfigManagerSpec`,
  `ForecastReadModelSpec`, `ForecastPipelineSettingsSpec`, `PollSchedulerPollingChangedSpec`,
  `PollSchedulerStartupOrderSpec` (all `[Fact(Timeout = TestTimeouts.Hosted)]`).
- `AwaitAssertMax` used as a real-time bound: `ForecastReadModelSpec` (`Task.WaitAsync`, a
  manual polling deadline, failure messages).
- Budget breaches outside the constants: `StreamConsumerActorSpec.Untracked_terminated_is_ignored`
  (`Timeout = 5000` around a dilated default `ExpectTerminatedAsync` = 9 s and a 5 s task wait),
  `Retry_count_resets_on_successful_transition` (`Timeout = 60000`, raw `AwaitConditionAsync` 10 s =
  30 s), the other `StreamConsumerActorSpec` facts with literal `30000`, and the raw 5 s
  `AwaitConditionAsync` in `PollSchedulerSpec` (15 s, within budget but not via the constant).

## Goals / Non-Goals

**Goals:**
- The relation "dilated wait + margin <= xUnit timeout" is checked by a spec, not by prose.
- Effective wall-clock bounds stay the same as the originally intended 15 s.

**Non-Goals:**
- Auditing every literal `TimeSpan` in every spec by a source scanner.

## Decisions

### D1: `AwaitAssertMax` becomes an un-dilated TestKit argument (5 s)

`AwaitAssertMax = 5 s`; TestKit dilates it to 15 s, the bound the original rule intended. The
xUnit timeout stays 30 s, leaving 15 s for host start-up and the waits before the failing one.

Alternatives:
- *Raise `Hosted` to > 45 s*: keeps the constant but makes a hanging spec take 45 s+ to report and
  slows a failing CI run; the 45 s wait is far beyond what any spec needs.
- *Read the timefactor from config at call sites* (`AwaitAssertMax / TestKitSettings.TestTimeFactor`):
  needs a TestKit instance at every use and hides the budget in arithmetic; a single constant is
  simpler and the guard spec ties it to the real configuration.
- *Helper method `AwaitAssertBounded(...)`*: a wrapper over four TestKit APIs (and probes) adds
  surface for little gain once the constant itself is right.

### D2: `TimeFactor` lives in `TestTimeouts`, `AddTestTimefactor()` reads it

One source of truth for the factor, so the guard spec's arithmetic and the HOCON cannot drift. The
guard also reads `TestKitSettings.TestTimeFactor` from a TestKit configured with
`AddTestTimefactor()` and asserts it equals `TestTimeouts.TimeFactor`.

### D3: `RealTimeWaitMax = AwaitAssertMax × TimeFactor` for non-dilated waits

`ForecastReadModelSpec` uses the bound for `Task.WaitAsync` and a manual deadline loop. Those must
keep their 15 s real-time budget, so they get their own constant rather than the shrunken
`AwaitAssertMax`. Naming the two separately makes the dilated/un-dilated distinction visible at
the call site.

### D4: Guard spec in `Njord.Core.Tests` (`TestTimeoutsSpec`)

It needs a running TestKit to read the effective timefactor and `Dilated(...)`, which the
architecture project (static ArchUnit rules, no TestKit) does not have; `Njord.Core.Tests` already
hosts TestKit specs and references `Njord.Tests.Shared`. Margin: 10 s, the slack left for host
start-up and preceding waits. TDD: the spec is written first and fails against the old
`AwaitAssertMax = 15 s` (45 s + 10 s > 30 s).

### D5: Align the specs that breach or bypass the budget

- `StreamConsumerActorSpec`: all async facts use `TestTimeouts.Hosted`; the raw 10 s
  `AwaitConditionAsync` becomes `TestTimeouts.AwaitAssertMax` (15 s dilated instead of 30 s; the
  condition is a ~1 s retry).
- `PollSchedulerSpec`: the raw 5 s `AwaitConditionAsync` bound becomes `TestTimeouts.AwaitAssertMax`
  (same value).
- `ForecastReadModelSpec`: real-time uses switch to `TestTimeouts.RealTimeWaitMax`.

Specs whose waits are all un-dilated or already well within their literal timeout (e.g. the MQTT
specs with task waits only, `SinkRefConnectionSpec`) stay unchanged.

Alternative rejected: an architecture rule scanning `*Spec.cs` for literal `TimeSpan`s inside
TestKit waits; reliable parsing of call chains (probes, lambdas, helpers) by text is too
heuristic and would need exceptions.

## Risks / Trade-offs

- [A spec that passed only because it had up to 45 s now fails at 15 s on a slow runner] → 15 s
  is the documented intent; the full loop is run twice to check for new flakes.
- [A new spec passes a raw large `TimeSpan` to a TestKit wait] → AGENTS.md states the rule; the
  guard covers the shared constants only.
