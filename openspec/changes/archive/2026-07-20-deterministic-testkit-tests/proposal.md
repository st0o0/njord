## Why

28 test assertions across 6 specs use `AsyncAssert.WaitUntil` — a polling loop
with a timeout (default 10s). These are inherently non-deterministic: they pass
locally but flake on CI runners with thread starvation. The root cause is that
actor-based tests use shared mutable state (`List<T>`, `TaskCompletionSource`)
polled from the test thread, instead of Akka TestKit's deterministic message
inspection (`TestProbe`, `ExpectMsg`).

## What Changes

- Replace `AsyncAssert.WaitUntil` / `StaysTrue` with TestProbe-based assertions
  in all 6 affected specs.
- Use `TestProbe.ExpectMsg<T>` for "something happened" assertions.
- Use `TestProbe.ExpectNoMsg` for "nothing happened" assertions (replaces
  `StaysTrue`).
- Remove `AsyncAssert` class if no remaining usages.

## Non-goals

- Rewriting test logic or tested behavior.
- Adding new test coverage.
- Changing production code.

## API-budget impact

No impact — test-only change.

## Capabilities

### New Capabilities

_(none)_

### Modified Capabilities

- `test-project-structure`: Removing polling-based `AsyncAssert` in favor of
  TestKit deterministic assertions.

## Impact

- `src/Njord.Tests/Pipeline/SchedulerActorSpec.cs` (3 usages)
- `src/Njord.Tests/Pipeline/SinkRefConnectionSpec.cs` (1 usage)
- `src/Njord.Tests/Mqtt/DiscoveryActorSpec.cs` (13 usages)
- `src/Njord.Tests/Mqtt/MqttConnectionActorSpec.cs` (4 usages)
- `src/Njord.Tests/Egress/ModelStateActorSpec.cs` (5 usages)
- `src/Njord.Tests/Enrichment/ForecastHistoryActorSpec.cs` (2 usages)
- `src/Njord.Tests.Shared/AsyncAssert.cs` — remove if empty
