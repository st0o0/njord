## Why

The test suites carry duplicated helpers and scenarios (poll-state specs), 14 near-identical alert rule specs, repeated HTTP stub ceremony and missing error-path coverage in the OpenMeteo client specs. Single-project helpers also live in `Njord.Tests.Shared`.

## What Changes

- Extract a shared `PollStateFixture` in `Njord.Pipeline.Tests`; drop duplicate poll-state scenarios; replace `!.` with `Assert.NotNull`.
- Collapse the 14 alert rule specs into one `AlertRuleThresholdSpec` Theory covering Yellow, Orange and Red boundaries.
- Add `StubOpenMeteoHandler` and `OpenMeteoFixtures`; remove weaker URI assertions; add 5xx, malformed JSON, cancellation and time-axis-longer-than-data tests; split multi-concern series mapping tests.
- Rename and fold the `PollManagerStateSpec` last-successful-poll tests into a Theory; reduce `GatewayRestartSpec` boilerplate.
- Move single-project helpers out of `Njord.Tests.Shared`, delete the unused `FakePipelineOutput`, merge the Akka test config extensions into `NjordActorSpec.cs`.

No production code changes.

## Impact

- Test code only. Test count stays roughly constant.
