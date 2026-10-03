## 1. TestKit Timefactor Configuration

- [x] 1.1 Add a static helper method `ApplyTestTimefactor(AkkaConfigurationBuilder)` in `src/Njord.Tests.Shared/` that calls `builder.AddHocon("akka.test.timefactor = 3", HoconAddMode.Prepend)`
- [x] 1.2 Call the helper from `ConfigureAkka` in all 24 TestKit-derived test classes: `SensorHubActorSpec`, `SchedulerActorSpec`, `SchedulerActorSnapshotSpec`, `SchedulerActorGetPollStatesSpec`, `SchedulerActorStartupOrderSpec`, `ModelStateActorSpec`, `DiscoveryActorSpec`, `MqttEgressActorSpec`, `MqttConnectionActorSpec`, `StreamConsumerActorSpec`, `EgressActorSpec`, `EnrichmentActorSpec`, `ForecastHistoryActorSpec`, `BudgetTrackerActorSpec`, `SinkRefConnectionSpec`, `PipelineConnectionSpec`, `GrpcSnapshotConsumerTerminatedSpec`, and any others found
- [x] 1.3 Run full test suite to verify no regressions: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj`

## 2. BudgetThrottleStageSpec — Eliminate TimeProvider.System and Thread.Sleep

- [x] 2.1 Replace `TimeProvider.System` with `FakeTimeProvider` (fixed epoch `2026-07-12T06:00:00Z`) in all 9 `WeightedBudgetGate` constructions in `src/Njord.Tests/Pipeline/BudgetThrottleStageSpec.cs` (lines 130, 140, 150, 162, 172, 181, 193, 206)
- [x] 2.2 Replace `Thread.Sleep(1600)` at line 198 with `_time.Advance(TimeSpan.FromMilliseconds(1600))`
- [x] 2.3 Replace `DateTimeOffset.UtcNow` in `MakeTarget` helper (line 221) with fixed epoch
- [x] 2.4 Run: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Pipeline.BudgetThrottleStageSpec"`

## 3. StreamConsumerActorSpec — Replace Timing Races

- [x] 3.1 Replace `Task.Delay(500)` tight-loop detection (line 207) with deterministic wait (kept Task.Delay intentionally — Akka scheduler is wall-clock based)
- [x] 3.2 Replace `ExpectNoMsgAsync(100ms)` at line 236 with `ExpectNoMsgAsync(500ms)`
- [x] 3.3 Replace `Task.WhenAny(secondGraphTcs.Task, Task.Delay(800))` at line 247 with `ExpectNoMsgAsync(500ms)` + Assert
- [x] 3.4 Replace `Task.Delay(200)` "beat" at line 273 with direct Ask assertion
- [x] 3.5 Replace `Task.WhenAny(thirdGraphTcs.Task, Task.Delay(3000))` at line 326 with `AwaitConditionAsync(() => thirdGraphTcs.Task.IsCompleted, TimeSpan.FromSeconds(10))`
- [x] 3.6 Run: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Actors.StreamConsumerActorSpec"`

## 4. SchedulerActorSpec — Replace Task.Delay

- [x] 4.1 Replace `Task.Delay(500)` tight-loop detection (line 193) with `AwaitConditionAsync` on warning log count with 5s timeout
- [x] 4.2 Run: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Pipeline.SchedulerActorSpec"`

## 5. Replace TimeProvider.System in Remaining Test Files

- [x] 5.1 `src/Njord.Tests/Enrichment/EnrichmentActorSpec.cs:45` — Replace `TimeProvider.System` in `ConsensusSnapshotFactory` with `FakeTimeProvider`
- [x] 5.2 `src/Njord.Tests/Enrichment/EnrichmentFeatureContractSpec.cs:26,29,30` — Replace `TimeProvider.System` in `AlertEnrichment`, `IndexEnrichment`, `HistoryEnrichment` with `FakeTimeProvider`
- [x] 5.3 `src/Njord.Tests/Enrichment/ForecastHistoryActorSpec.cs:41,51,64` — Replace `TimeProvider.System` in all 3 `ForecastHistoryActor` constructions with `FakeTimeProvider`
- [x] 5.4 `src/Njord.Tests/Mqtt/MqttConnectionActorSpec.cs:30` — Replace `TimeProvider.System` with `FakeTimeProvider`
- [x] 5.5 `src/Njord.Tests/Pipeline/PipelineConnectionSpec.cs:63` — Replace `TimeProvider.System` in `OptionsBudgetProvider` with `FakeTimeProvider`
- [x] 5.6 `src/Njord.Tests/Grpc/OpsGrpcServiceSpec.cs:44` — Replace `TimeProvider.System` in `OpsGrpcService` with `FakeTimeProvider`
- [x] 5.7 Also fixed: `WeatherGrpcServiceSpec`, `HistoryEnrichmentSpec`, `TrendEnrichmentSpec`
- [x] 5.8 Run full test suite: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj`

## 6. Replace Custom FakeTimeProvider Shims

- [x] 6.1 `src/Njord.Tests/Enrichment/AlertEnrichmentSpec.cs` — Remove local `FakeTimeProvider` class, replace with `Microsoft.Extensions.Time.Testing.FakeTimeProvider`
- [x] 6.2 `src/Njord.Tests/Enrichment/DerivedEnrichmentSpec.cs` — Same replacement
- [x] 6.3 `src/Njord.Tests/Enrichment/IndexEnrichmentSpec.cs` — Same replacement
- [x] 6.4 Run: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Enrichment.AlertEnrichmentSpec"` and repeat for the other two

## 7. Replace DateTimeOffset.UtcNow in Test Data

- [x] 7.1 `src/Njord.Tests/Pipeline/PollPipelineSpec.cs:38,72` — Replace `DateTimeOffset.UtcNow` in `CycleId` with fixed epoch
- [x] 7.2 `src/Njord.Tests/Egress/EgressActorSpec.cs:69` — Replace `DateTimeOffset.UtcNow` in `CycleId` with fixed epoch
- [x] 7.3 `src/Njord.Tests/Mqtt/MqttConnectionActorSpec.cs:29` — Replace `DateTimeOffset.UtcNow` in `NjordHealthState` with fixed epoch
- [x] 7.4 `src/Njord.Tests/Pipeline/PipelineConnectionSpec.cs:181,208` — Replace `DateTimeOffset.UtcNow` in `ForecastPoint` with fixed epoch
- [x] 7.5 `src/Njord.Tests/Grpc/EnrichmentProtoMapperSpec.cs:318` — Replace `DateTimeOffset.UtcNow` with fixed epoch
- [x] 7.6 `src/Njord.Tests/Domain/Sensors/SensorSnapshotSpec.cs:12` — Replace `DateTimeOffset.UtcNow` with fixed epoch
- [x] 7.7 `src/Njord.Tests/Sensors/SensorHubActorSpec.cs:12` — Replace `FakeTimeProvider(DateTimeOffset.UtcNow)` with fixed epoch seed
- [x] 7.8 Run full test suite: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj`

## 8. Production Code — TimeProvider Seams

- [x] 8.1 `src/Njord/Grpc/SensorGrpcService.cs` — Add `TimeProvider` constructor parameter, replace `DateTimeOffset.UtcNow` at line 77 with `_timeProvider.GetUtcNow()`
- [x] 8.2 `src/Njord/Domain/Analysis/HistoryAnalyzer.cs` — Remove `?? TimeProvider.System` fallback
- [x] 8.3 Update all callers of `HistoryAnalyzer.ModelAccuracy` to pass `TimeProvider` explicitly
- [x] 8.4 Run full test suite: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj`

## 9. Widen ExpectNoMsgAsync Windows

- [x] 9.1 `src/Njord.Tests/Actors/StreamConsumerActorSpec.cs:236` — Already handled in task 3.2
- [x] 9.2 `src/Njord.Tests/Mqtt/DiscoveryActorSpec.cs:119` — Widen from 300ms to 500ms
- [x] 9.3 Run: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Mqtt.DiscoveryActorSpec"`

## 10. Final Validation

- [x] 10.1 Run full test suite: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj` — 758 tests, 0 failures
- [ ] 10.2 Run `dotnet slopwatch` from repo root — SKIPPED (tool not installed)
- [x] 10.3 Run `dotnet format --verify-no-changes src/Njord.slnx` to check formatting — only analyzer warnings, no whitespace issues
- [x] 10.4 Verify no `TimeProvider.System` remains in test code — CONFIRMED: 0 matches
- [x] 10.5 Verify no `DateTimeOffset.UtcNow` remains in test code — CONFIRMED: 0 matches
- [x] 10.6 Verify no `Thread.Sleep` remains in test code — CONFIRMED: 0 matches
- [x] 10.7 Verify no `Task.Delay` remains in test assertions — 2 intentional (wall-clock scheduler tests, documented) + 3 in stream stage simulation (acceptable)
