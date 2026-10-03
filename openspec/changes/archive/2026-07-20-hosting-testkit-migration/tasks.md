## 1. Infrastructure Setup

- [x] 1.1 Add `Akka.Hosting.TestKit` package to `src/Directory.Packages.props` and `src/Njord.Tests/Njord.Tests.csproj`
- [x] 1.2 Create `TestPersistenceConfig` static class in `src/Njord.Tests.Shared/` providing in-memory journal + snapshot HOCON config
- [x] 1.3 Verify xUnit v3 compatibility: migrate `SchedulerActorSpec` as the pilot (includes persistence, DI, timing — exercises all patterns). Delete `TestableSchedulerActor`, use real `SchedulerActor` with `DiscoveryInterval = 50ms`. Run: `dotnet run --project Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Pipeline.SchedulerActorSpec"`

## 2. Migrate Non-Persistence Actor Tests (TestKit → Hosting.TestKit)

- [x] 2.1 Migrate `src/Njord.Tests/Enrichment/EnrichmentActorSpec.cs` — override `ConfigureServices`/`ConfigureAkka`, register real `EnrichmentActor` via DI, keep fake collaborators
- [x] 2.2 Migrate `src/Njord.Tests/Egress/EgressActorSpec.cs`
- [x] 2.3 Migrate `src/Njord.Tests/Egress/ModelStateActorSpec.cs`
- [x] 2.4 Migrate `src/Njord.Tests/Grpc/ConfigGrpcServiceSpec.cs`
- [x] 2.5 Migrate `src/Njord.Tests/Grpc/ForecastGrpcServiceSpec.cs`
- [x] 2.6 Migrate `src/Njord.Tests/Mqtt/MqttConnectionActorSpec.cs`
- [x] 2.7 Migrate `src/Njord.Tests/Mqtt/DiscoveryActorSpec.cs`
- [x] 2.8 Migrate `src/Njord.Tests/Pipeline/PollPipelineSpec.cs`

## 3. Migrate Persistence Actor Tests (PersistenceTestKit → Hosting.TestKit + InMemory HOCON)

- [x] 3.1 Migrate `src/Njord.Tests/Enrichment/ForecastHistoryActorSpec.cs`
- [x] 3.2 Migrate `src/Njord.Tests/Grpc/EnrichmentSnapshotActorSpec.cs`
- [x] 3.3 Migrate `src/Njord.Tests/Grpc/ForecastSnapshotActorSpec.cs`
- [x] 3.4 Migrate `src/Njord.Tests/Pipeline/BudgetThrottleStageSpec.cs`
- [x] 3.5 Migrate `src/Njord.Tests/Pipeline/PipelineConnectionSpec.cs`
- [x] 3.6 Migrate `src/Njord.Tests/Pipeline/SinkRefConnectionSpec.cs`

## 4. Keep PersistenceTestKit (no migration — verify only)

- [x] 4.1 Verify `src/Njord.Tests/Grpc/ForecastSnapshotRecoverySpec.cs` still passes (stays on `PersistenceTestKit`)
- [x] 4.2 Verify `src/Njord.Tests/Grpc/EnrichmentSnapshotRecoverySpec.cs` still passes (stays on `PersistenceTestKit`)

## 5. Cleanup and Spec Sync

- [x] 5.1 Remove `Akka.TestKit.Xunit` package reference from `src/Njord.Tests/Njord.Tests.csproj` if no test class still uses it (the two recovery specs use `PersistenceTestKit` which has its own TestKit dependency)
- [x] 5.2 Update `openspec/specs/test-project-structure/spec.md` with the new base class requirements

## 6. Validation

- [x] 6.1 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj`
- [x] 6.2 Run slopwatch: `dotnet slopwatch`
