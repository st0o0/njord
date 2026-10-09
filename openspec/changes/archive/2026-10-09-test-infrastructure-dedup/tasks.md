## 1. Shared helpers

- [ ] 1.1 Add `TestTimeConstants` to `src/Njord.Tests.Shared/` with named `DateTimeOffset` constants (`PipelineEpoch`, `BudgetEpoch`, etc.) replacing the 7+ hardcoded values.
- [ ] 1.2 Add `TestTimeouts.AtEpoch(DateTimeOffset? offset = null)` factory method returning a `FakeTimeProvider`.

## 2. NjordActorSpec base class

- [ ] 2.1 Add `NjordActorSpec : Akka.Hosting.TestKit.TestKit` to `src/Njord.Tests.Shared/` with default `ConfigureServices` (TestOptionsMonitor, FakeTimeProvider, shared fakes) and `ConfigureAkka` (in-memory persistence, timefactor = 3).
- [ ] 2.2 Add `GetActor<TKey>()` helper method.
- [ ] 2.3 Add virtual override points for per-spec customization.

## 3. Migrate Pipeline specs

- [ ] 3.1 Migrate `SchedulerActorSpec` to extend `NjordActorSpec`, removing boilerplate.
- [ ] 3.2 Migrate `SchedulerActorRefFailureSpec`, `SchedulerActorGetPollStatesSpec`, `SchedulerActorSnapshotSpec`, `SchedulerActorStartupOrderSpec`.
- [ ] 3.3 Migrate `BudgetTrackerActorSpec`.
- [ ] 3.4 Replace hardcoded `DateTimeOffset` values with `TestTimeConstants` across all Pipeline specs (including non-actor specs like `SchedulerStateSpec`, `ModelPollStateSpec`).
- [ ] 3.5 Replace inline `FakeTimeProvider` instantiations with `TestTimeouts.AtEpoch()`.

## 4. Migrate other project specs

- [ ] 4.1 Migrate `EnrichmentActorSpec` and `ForecastHistoryActorSpec` in `src/Njord.Enrichment.Tests/`.
- [ ] 4.2 Migrate `MqttConnectionActorSpec`, `MqttDiscoveryActorSpec`, `MqttStateActorSpec` in `src/Njord.Mqtt.Tests/`.
- [ ] 4.3 Migrate `ModelStateActorSpec` in `src/Njord.Egress.Tests/`.
- [ ] 4.4 Migrate `GrpcSnapshotConsumerTerminatedSpec` and similar in `src/Njord.Grpc.Tests/`.
- [ ] 4.5 Replace inline `FakeTimeProvider` in non-actor specs (Compute, Core) with the factory.

## Validation

- [ ] 5.1 `dotnet build Njord.slnx` (run from `src/`)
- [ ] 5.2 Run all test projects sequentially (bash loop from AGENTS.md) to confirm zero regressions.
- [ ] 5.3 `dotnet slopwatch analyze -d . --fail-on warning` (run from repo root)
