## 1. Rename ModelForecast → Forecast

- [x] 1.1 Rename `src/Njord.Weather/ModelForecast.cs` to `Forecast.cs`, rename the record to `Forecast`. Update `ModelSnapshot`, `ForecastDataHash`, `ForecastSeries` references in `Njord.Weather`.
- [x] 1.2 Rename `src/Njord.Weather.Tests/ModelForecastSpec.cs` to `ForecastSpec.cs`. Update all `ModelForecast` references across `Njord.Weather.Tests`.
- [x] 1.3 Update `FetchOutcome.Success` to use `Forecast` (in `src/Njord.Weather/FetchOutcome.cs`). Update `Njord.Messages` references (`PipelineEvent`, `UpdateForecast`, `QueryForecast`, `QueryAllForecasts`).
- [x] 1.4 Update all `ModelForecast` references across remaining projects: `Njord.Compute.Tests`, `Njord.Core`, `Njord.Core.Tests`, `Njord.Ingest`, `Njord.Tests.Shared`, `Njord.Pipeline`, `Njord.Pipeline.Tests`, `Njord.Mqtt`, `Njord.Mqtt.Tests`, `Njord.Grpc`, `Njord.Grpc.Tests`, `Njord.Alerts.Tests`, `Njord.Architecture.Tests`, `Njord` (host).
- [x] 1.5 Build: `dotnet build Njord.slnx` — zero errors.

## 2. Rename FetchOutcome → PollOutcome

- [x] 2.1 Rename `src/Njord.Weather/FetchOutcome.cs` to `PollOutcome.cs`, rename the record and variants. Update `IOpenMeteoClient` return type in `src/Njord.Core/Ingest/IOpenMeteoClient.cs`.
- [x] 2.2 Update all `FetchOutcome` references: `Njord.Ingest` (`OpenMeteoClient`), `Njord.Tests.Shared` (`FakeOpenMeteoClient`), `Njord.Ingest.Tests`, `Njord.Pipeline` (`PipelineActor`), `Njord.Pipeline.Tests`.
- [x] 2.3 Build: `dotnet build Njord.slnx` — zero errors.

## 3. Add AccumulatedForecast and typed PipelineEvents

- [x] 3.1 Add `AccumulatedForecast(ModelSnapshot Snapshot, Forecast Forecast)` record to `src/Njord.Weather/AccumulatedForecast.cs`.
- [x] 3.2 Move `PipelineEvent` from `src/Njord.Messages/Egress/PipelineEvent.cs` to `src/Njord.Core/Egress/PipelineEvent.cs` (Core can reference Compute types). Rename `ModelForecastUpdate` → `ForecastReceived`. Replace `AnalysisResult` with typed variants: `ConsensusComputed`, `AlertComputed`, `DerivedComputed`, `TrendComputed`, `IndexComputed`. Keep `ModelCapability`. Update namespace from `Njord.Messages.Egress` to `Njord.Core.Egress`. Update all `using Njord.Messages.Egress` → `using Njord.Core.Egress` across the codebase.
- [x] 3.3 Build: `dotnet build Njord.slnx` — expect errors from consumers and pipeline (fixed in later tasks).

## 4. Change analyzer signatures

- [x] 4.1 `AlertAnalyzer`: change `Compute(IAlertInput)` to `Compute(ConsensusSnapshot)`. Delete `src/Njord.Alerts/IAlertInput.cs`. Update `src/Njord.Alerts.Tests/AlertAnalyzerSpec.cs` — pass `ConsensusSnapshot` directly, remove `TestAlertInput`.
- [x] 4.2 `DerivedAnalyzer`: change `Compute(IDerivedInput)` to `Compute(ConsensusSnapshot)`. Move `Horizons` to a constructor-injected field. Delete `src/Njord.Derived/IDerivedInput.cs`. Update `src/Njord.Derived.Tests/DerivedAnalyzerSpec.cs`.
- [x] 4.3 `TrendAnalyzer`: change `Compute(ITrendInput)` to `Compute(ConsensusSnapshot, ConsensusSnapshot?)`. Delete `src/Njord.Trends/ITrendInput.cs`. Update `src/Njord.Trends.Tests/TrendAnalyzerSpec.cs`.
- [x] 4.4 `IndexAnalyzer`: change `Compute(IIndexInput)` to `Compute(ConsensusSnapshot, SensorSnapshot?)`. Delete `src/Njord.Indices/IIndexInput.cs`. Update `src/Njord.Indices.Tests/IndexAnalyzerSpec.cs`.
- [x] 4.5 Delete `src/Njord/Pipeline/PollContext.cs`. Remove `IAnalysisStage` from `src/Njord.Core/Pipeline/IAnalysisStage.cs`. Delete `src/Njord.Tests.Shared/PassthroughAnalysisStage.cs`.
- [x] 4.6 Build: `dotnet build Njord.slnx` — expect errors from pipeline and consumers.

## 5. Rewrite PipelineActor pipeline composition

- [x] 5.1 Delete `src/Njord/Pipeline/AnalysisStage.cs`. Remove `IAnalysisStage` DI registration from `src/Njord/Configuration/AnalysisSetupContainer.cs`. Remove `IAnalysisStage` constructor parameter from `PipelineActor`.
- [x] 5.2 Rewrite `MaterializePipeline` in `src/Njord.Pipeline/PipelineActor.cs` with named local flows: `fetch` (`WeightedTarget → PollOutcome`), `schedulerFeedback` (`Sink<PollOutcome>` via AlsoTo with `propagateFailure: false`), `analysis` (GraphDSL with `Broadcast<Forecast>(2)` / `Merge<PipelineEvent>(2)`: branch 0 = `ForecastReceived`, branch 1 = Scan → Where(HasChanged) → consensus → feature fan-out).
- [x] 5.3 Implement the feature fan-out as nested `Broadcast<ConsensusSnapshot>(N)` / `Merge<PipelineEvent>(N)`: consensus branch, alerts branch, derived branch, trends branch (with Scan for previous), indices branch (with SelectAsync for sensors). Per-branch stream supervision.
- [x] 5.4 Build: `dotnet build Njord.slnx` — zero errors.

## 6. Update consumers

- [x] 6.1 Update `src/Njord.Mqtt/MqttStateActor.cs`: replace `AnalysisResult` pattern matching with typed event matching (`ForecastReceived`, `ConsensusComputed`, `AlertComputed`, etc.). Update `MapToMqttMessages` and remove `MapAnalysis`.
- [x] 6.2 Update `src/Njord.Mqtt/StatePayloadBuilder.cs` if needed for new event types.
- [x] 6.3 Update `src/Njord.Grpc/GrpcConsumerActor.cs`: replace `AnalysisResult` with typed events in state tracking.
- [x] 6.4 Update `src/Njord.Grpc/WeatherGrpcService.cs`: replace `AnalysisResult` collect/mapping with typed event handling.
- [x] 6.5 Update `src/Njord.Grpc/EnrichmentProtoMapper.cs`: replace `MapToEvent(string typeName, object result, ...)` with typed overloads or a method accepting `PipelineEvent`.
- [x] 6.6 Update `src/Njord.Grpc/OpsGrpcService.cs` and `src/Njord.Grpc/AdminGrpcService.cs` if they reference `AnalysisResult`.
- [x] 6.7 Build: `dotnet build Njord.slnx` — zero errors.

## 7. Update tests

- [x] 7.1 Update `src/Njord.Pipeline.Tests/PipelineConnectionSpec.cs` and `PipelineActorShutdownSpec.cs`: remove `PassthroughAnalysisStage` usage, inject disabled analyzers or build test pipeline differently.
- [x] 7.2 Update `src/Njord.Mqtt.Tests/MqttStateActorSpec.cs` and `StatePayloadBuilderSpec.cs`: use new typed events.
- [x] 7.3 Update `src/Njord.Grpc.Tests/` specs: use new typed events, update verified snapshots.
- [x] 7.4 Update `src/Njord.Mqtt.Tests/` verified snapshot `.txt` files if event names changed.
- [x] 7.5 Run all tests: `dotnet build Njord.slnx` then `for p in Njord.*Tests; do [ "$p" = Njord.Tests.Shared ] && continue; dotnet run --project "$p/$p.csproj" --no-build; done` — zero failures.

## 8. Update documentation

- [x] 8.1 Update `AGENTS.md`: reflect renamed types (`Forecast`, `PollOutcome`, `ForecastReceived`), typed `PipelineEvent` variants, removed `IAnalysisStage`/`PollContext`/input interfaces, pipeline composition description.
