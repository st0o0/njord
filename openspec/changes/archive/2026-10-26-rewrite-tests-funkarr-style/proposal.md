## Why

The test codebase uses njord-specific conventions (NjordActorSpec base class, `*Spec` suffix,
TestTimeouts, AddTestTimefactor, spec base classes) that diverge from the FunkArr reference project.
This makes it harder to apply patterns from FunkArr and creates unnecessary cognitive overhead when
switching between projects. The conventions are already documented in AGENTS.md but the code has not
been migrated.

## What Changes

- **Rename** all `*Spec` test classes and files to `*Tests` (130 files).
- **Remove `NjordActorSpec`** base class. Actor tests inherit `Akka.TestKit.Xunit.TestKit` directly,
  as in FunkArr.
- **Remove spec base classes**: `PollManagerSpecBase`, `PollWorkerSpecBase`,
  `PollingConnectionTestKit`, `PipelineConnectionTestKit`, `SinkRefTestKit`. Inline their setup into
  each test class.
- **Remove `TestTimeouts` and `AddTestTimefactor`**. Use inline timeout values or TestKit defaults.
- **Clean up `Njord.Tests.Shared`**: delete `NjordActorSpec.cs`, `TestTimeouts.cs`,
  `TestAkkaConfigExtensions.cs`, `TestTimeConstants.cs`. Keep fixtures, fakes, builders and
  `FeatureRunner`.
- **No behavior changes.** Every test asserts the same thing before and after.

## Non-goals

- Rewriting test logic or assertions.
- Adding new tests.
- Changing production code.
- Migrating integration tests away from `IAsyncLifetime` (that matches FunkArr).

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `test-conventions`: Test classes use `*Tests` suffix, inherit `TestKit` directly (no shared base),
  no timeout constants.

## Impact

- All 12 test projects (131 test files, ~1006 tests).
- `Njord.Tests.Shared`: 4 files deleted, no new files.
- No production code changes.
- No API-budget impact.
