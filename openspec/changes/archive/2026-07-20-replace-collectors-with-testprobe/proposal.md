## Why

Three specs contain hand-rolled `MessageCollector<T>` / `OfferCollector` classes
(~120 lines total) that duplicate what Akka TestKit's `TestProbe` already
provides. Now that every spec extends `TestKit` or `PersistenceTestKit`,
`CreateTestProbe()` is available everywhere — the custom collectors are
redundant infrastructure.

## What Changes

- Replace `OfferCollector` in SchedulerActorSpec with a TestProbe wired via
  `Sink.ForEach(t => probe.Tell(t))`. Replace `WaitFor(N)` calls with
  `ExpectMsg<T>()`, `Count`/`Items` access with ExpectMsg return values,
  and `SetProbe` + `ExpectNoMsg` stays as-is.
- Replace `MessageCollector<T>` in ModelStateActorSpec with TestProbe. Fake
  actors Tell the probe instead of calling `collector.Add()`. Replace
  `WaitFor`/`WaitForMatch` with `ExpectMsg`/`FishForMessage`.
- Replace `MessageCollector<T>` in DiscoveryActorSpec with TestProbe. Redesign
  count-based assertions (`Ha_birth`, `Late_capability`) to use
  `ReceiveWhile` as batch-drain followed by `ExpectMsg`.
- Also fix the `.Result` blocking call in SchedulerActorSpec's
  FakePipelineActor (same pattern fixed in DiscoveryActorSpec previously).

## Non-goals

- Replacing `RecordingTransport` in MqttConnectionActorSpec — that's an
  `IMqttTransport` implementation, not actor message flow.
- Changing production code.
- Adding new test coverage.

## API-budget impact

No impact — test-only change.

## Capabilities

### New Capabilities

_(none)_

### Modified Capabilities

- `test-project-structure`: Strengthening the deterministic-assertions
  requirement to prefer TestProbe over custom collector classes.

## Impact

- `src/Njord.Tests/Pipeline/SchedulerActorSpec.cs` — remove OfferCollector
- `src/Njord.Tests/Egress/ModelStateActorSpec.cs` — remove MessageCollector
- `src/Njord.Tests/Mqtt/DiscoveryActorSpec.cs` — remove MessageCollector
