## 1. SchedulerActorSpec — remove OfferCollector

- [x] 1.1 Replace `FakePipelineActor` to accept a TestProbe and route offers via `Sink.ForEach(t => probe.Tell(t))`. Fix `.Result` blocking calls with `PipeTo`.
- [x] 1.2 Remove `OfferCollector` class. Replace `_collector` field with a TestProbe created in `CreateScheduler`. Replace all `WaitForOffer(N)` calls with `ExpectMsg<WeightedTarget>()` calls.
- [x] 1.3 Rewrite `Transport_failure_triggers_backoff_retry` and `Trigger_immediate_poll_actually_offers_target_to_pipeline` — replace `countBefore`/`WaitFor(countBefore + 1)` with `ExpectMsg` after the action.

## 2. ModelStateActorSpec — remove MessageCollector

- [x] 2.1 Replace fake actors (`FakeEgressSinkProvider`, `FakePipelineSource`, `FeedingPipelineSource`) to accept TestProbe parameters. Route request signals and stream events via `probe.Tell()`. Fix `.Result` blocking calls with `PipeTo`.
- [x] 2.2 Remove `MessageCollector<T>` class. Replace all `WaitFor`/`WaitForMatch` calls with `ExpectMsg`/`FishForMessage` on TestProbe.

## 3. DiscoveryActorSpec — remove MessageCollector

- [x] 3.1 Replace fake actors (`FakeMqttConnection`, `MqttMessageProbe`) to accept TestProbe parameters. Route request signals and published messages via `probe.Tell()`.
- [x] 3.2 Remove `MessageCollector<T>` class. Replace `WaitFor`/`WaitForMatch` calls with `ExpectMsg`/`FishForMessage`.
- [x] 3.3 Redesign `Ha_birth_re_publishes_with_learned_state` — use `ReceiveWhile` to drain the first batch, then `ExpectMsg` after birth message.
- [x] 3.4 Redesign `Late_capability_triggers_incremental_publish` — use `ExpectMsg` × 2 instead of `WaitFor(2)`.

## 4. Validation

- [x] 4.1 Run `dotnet build Njord.slnx` from `src/` and confirm clean build.
- [x] 4.2 Run `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/` and confirm all tests pass.
