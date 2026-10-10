## 1. Define IAnalysisStage and wire into PipelineActor

- [x] 1.1 Add `IAnalysisStage` interface to `src/Njord.Core/Pipeline/IAnalysisStage.cs` — single method `CreateFlow(IUntypedActorContext context)` returning `Flow<ModelForecast, IReadOnlyList<PipelineEvent>, NotUsed>`. Add `using Akka.Streams.Dsl;` and `Njord.Messages.Egress`.
- [x] 1.2 Update `PipelineActor` constructor in `src/Njord.Pipeline/PipelineActor.cs` to accept `IAnalysisStage`. In `MaterializePipeline`, replace the simple `Collect(Success).Select(ModelForecastUpdate)` branch with: `Collect(Success) → Select(s => s.Forecast) → Via(analysisStage.CreateFlow(Context)) → SelectMany(events => events) → RunWith(pipelineOutputSink)`.
- [x] 1.3 Add `PipelineActorSpec` test in `src/Njord.Pipeline.Tests/` verifying that a fake `IAnalysisStage` is called with each `ModelForecast` and its returned events appear on the `BroadcastHub<PipelineEvent>` output.

## 2. Implement AnalysisStage in the Host

- [x] 2.1 Create `src/Njord/Pipeline/AnalysisStage.cs` implementing `IAnalysisStage`. Constructor takes `ConsensusSnapshotFactory`, `AlertAnalyzer`, `DerivedAnalyzer`, `TrendAnalyzer`, `IndexAnalyzer`, `HistoryTracker`, `IOptions<NjordOptions>`, `IOptions<EnrichmentOptions>`, `TimeProvider`. `CreateFlow` resolves `IForecastHistoryRegion` and `ISensorHubActor` from context.
- [x] 2.2 Internal flow: `Scan(ModelSnapshot.Update)` → `SelectAsync(1, async snapshot => { ask sensors, compute consensus, build PollContext with previous tracking, run all features, ask history actor, return List<PipelineEvent> })`. Use a closure-captured `ConsensusSnapshot? previous` for trend tracking (updated each cycle).
- [x] 2.3 Register `AnalysisStage` as `IAnalysisStage` singleton in `src/Njord/Configuration/AnalysisSetupContainer.cs`.
- [x] 2.4 Create `src/Njord.Tests/Pipeline/AnalysisStageSpec.cs` — test with real `ConsensusSnapshotFactory`, real feature analyzers (using test fixtures), mock `ForecastHistoryActor` via TestProbe. Verify: (a) consensus computed, (b) each enabled feature produces an AnalysisResult, (c) disabled feature produces no event, (d) first cycle has no trend result.

## 3. Remove ForecastSnapshotActor and route GetForecast through GrpcConsumerActor

- [x] 3.1 Expand `GrpcConsumerActor.LatestResults` to hold `IReadOnlyDictionary<WeatherModel, ModelForecast> Forecasts` instead of single `LastForecast`. Update `UpdateState` to accumulate per-model per-location.
- [x] 3.2 Add `QueryForecast` handler to `GrpcConsumerActor` — returns the forecast for the requested location+model from in-memory state, or a "not available" response.
- [x] 3.3 Update `WeatherGrpcService.GetForecast` in `src/Njord.Grpc/WeatherGrpcService.cs` to query `GrpcConsumerActor` via `IGrpcConsumerActor` instead of `IForecastSnapshotRegion`.
- [x] 3.4 Delete `src/Njord.Grpc/ForecastSnapshotActor.cs`, `src/Njord.Grpc/ForecastSnapshotMapping.cs`.
- [x] 3.5 Delete `src/Njord.Persistence/Grpc/ForecastSnapshotDto.cs`, `ForecastPointDto.cs`, `DailyForecastPointDto.cs`, `ModelForecastDto.cs`.
- [x] 3.6 Remove `IForecastSnapshotRegion` from `src/Njord.Core/Actors/ActorKeys.cs`. Remove shard registration from `src/Njord.Grpc/Configuration/GrpcSetupContainer.cs`.
- [x] 3.7 Update `src/Njord.Grpc.Tests/WeatherGrpcServiceSpec.cs` — replace `ForecastSnapshotActor`-based test setup with `GrpcConsumerActor` queries.
- [x] 3.8 Delete `src/Njord.Grpc.Tests/ForecastSnapshotActorSpec.cs`, `ForecastSnapshotRecoverySpec.cs`. Remove `src/Njord.Persistence.Tests/` snapshot DTO specs that reference deleted DTOs.

## 4. Wire MQTT AnalysisResult mapping

- [x] 4.1 Implement `AnalysisResult` case in `MqttStateActor.MapToMqttMessages` in `src/Njord.Mqtt/MqttStateActor.cs` — switch on `TypeName` (`"alerts"`, `"consensus"`, `"derived"`, `"trends"`, `"indices"`, `"history"`), delegate to existing `StatePayloadBuilder` methods. Emit enrichment state messages to per-feature MQTT topics.
- [x] 4.2 Add/update tests in `src/Njord.Mqtt.Tests/MqttStateActorSpec.cs` — verify `AnalysisResult` events produce the correct MQTT messages via the `StatePayloadBuilder` path.

## 5. Update architecture tests and solution

- [x] 5.1 Update `src/Njord.Architecture.Tests/LayerReferenceSpec.cs` — remove `IForecastSnapshotRegion` from expected types if referenced. Verify `Njord.Pipeline` references `Njord.Core` (for `IAnalysisStage`).
- [x] 5.2 Update `src/Njord.Architecture.Tests/ZoneArchitectureSpec.cs` if the removed shard breaks existing rules.
- [x] 5.3 Update `AGENTS.md` — remove `ForecastSnapshotActor` references, document `IAnalysisStage` pattern.

## 6. Validation

- [x] 6.1 Build: `dotnet build Njord.slnx` from `src/`
- [x] 6.2 Run all test projects:
```bash
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
```
- [x] 6.3 Run architecture tests: `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj`
