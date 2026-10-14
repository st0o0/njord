## Why

Akka.TestKit dilates every wait by `akka.test.timefactor` (3 in njord), including the explicit
durations passed to `AwaitAssertAsync`, `AwaitConditionAsync`, `ExpectMsgAsync` and
`ExpectNoMsgAsync`. `TestTimeouts.AwaitAssertMax` (15 s) therefore becomes a 45 s wait, while the
xUnit safety net `TestTimeouts.Hosted` is 30 s: a hosted spec that should fail with a descriptive
Akka assertion is instead killed by the xUnit timeout with no useful message. The invariant that
AGENTS.md states ("the xUnit timeout must exceed every Akka wait it wraps") does not hold today,
and nothing checks it.

## What Changes

- `TestTimeouts` (`Njord.Tests.Shared`) becomes the single source of the timefactor
  (`TimeFactor = 3`); `AddTestTimefactor()` reads it instead of a literal.
- `TestTimeouts.AwaitAssertMax` is redefined as an un-dilated TestKit argument (5 s, dilated to
  15 s), and a new `TestTimeouts.RealTimeWaitMax` (15 s, `AwaitAssertMax × TimeFactor`) bounds
  waits that TestKit does not dilate (`Task.WaitAsync`, manual deadline loops). The effective
  wall-clock bounds stay at 15 s, so no assertion is loosened and no wait gets longer.
- A guard spec asserts, against a TestKit configured with `AddTestTimefactor()`, that the
  configured timefactor equals `TestTimeouts.TimeFactor` and that the dilated `AwaitAssertMax`,
  `RealTimeWaitMax` and the dilated TestKit default each stay at least 10 s below `Hosted`.
- Specs whose waits break the budget are aligned: real-time waits in `ForecastReadModelSpec` use
  `RealTimeWaitMax`; `StreamConsumerActorSpec` (literal 5 s/30 s/60 s timeouts around dilated
  waits) and the raw 5 s/10 s `AwaitConditionAsync` bounds move to `TestTimeouts.Hosted` /
  `TestTimeouts.AwaitAssertMax`.
- AGENTS.md and the `njord-actor-spec` skill state the corrected rule.

## Non-goals

- Changing the timefactor itself or `TestTimeouts.Hosted`.
- Changing production code, actor behavior or any assertion's expected value.
- A source-scanning architecture rule for raw `TimeSpan` waits in specs (considered in
  design.md, rejected as too heuristic).
- Making the suite faster; the change only must not make it slower.

## API budget

None. The change touches test infrastructure only; polling and Open-Meteo requests are
unaffected (0 additional requests/month against the 300k free-tier limit).

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `deterministic-test-infrastructure`: the timefactor requirement gains a single source of truth
  and states that explicit durations dilate too; a new requirement makes "every dilated Akka wait
  stays below the xUnit timeout" an enforced invariant of the shared timeout constants.

## Impact

- `src/Njord.Tests.Shared/TestTimeouts.cs`, `TestTimefactorConfig.cs`
- New guard spec `src/Njord.Core.Tests/TestTimeoutsSpec.cs`
- `src/Njord.Pipeline.Tests/ForecastReadModelSpec.cs`, `PollSchedulerSpec.cs`,
  `src/Njord.Core.Tests/Actors/StreamConsumerActorSpec.cs`
- `AGENTS.md`, `.claude/skills/njord-actor-spec/SKILL.md`
