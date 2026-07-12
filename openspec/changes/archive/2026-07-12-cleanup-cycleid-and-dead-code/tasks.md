## 1. Domain model changes

- [x] 1.1 Remove `RetrievedAt` from `ModelForecast` record in `src/Njord/Domain/ModelForecast.cs` — drop the field entirely
- [x] 1.2 Simplify `FetchOutcome.Failure` in `src/Njord/Ingest/FetchOutcome.cs` — remove `CycleId`, `Location`, and `Model` fields, keep only `Reason` and `Detail`
- [x] 1.3 Remove `CycleId.From(TimeProvider)` factory method from `src/Njord/Domain/CycleId.cs` — CycleId is now created directly by the SchedulerActor

## 2. WeightedTarget carries CycleId

- [x] 2.1 Add `CycleId Cycle` field to `WeightedTarget` record in `src/Njord/Pipeline/WeightedTarget.cs`
- [x] 2.2 Update `SchedulerActor` in `src/Njord/Pipeline/SchedulerActor.cs` — create a `CycleId` from `_timeProvider.GetUtcNow()` in `OnScheduledPoll`, `OnRefreshModel`, and `OnRefreshLocation`, pass it to each `WeightedTarget`

## 3. Client signature change

- [x] 3.1 Remove `CycleId cycle` parameter from `IOpenMeteoClient.FetchAsync` in `src/Njord/Ingest/IOpenMeteoClient.cs` — the method now takes `(LocationOptions, WeatherModel, CancellationToken)` only
- [x] 3.2 Update `OpenMeteoClient.FetchAsync` in `src/Njord/Ingest/OpenMeteoClient.cs` — remove `CycleId` parameter, construct `ModelForecast` without `RetrievedAt`, drop the local `Failure` helper's CycleId/Location/Model args

## 4. Pipeline inlining and dead code removal

- [x] 4.1 Delete `src/Njord/Pipeline/FetchStage.cs` — inline the `SelectAsyncUnordered` + supervision into `PipelineActor.MaterializePipeline()` in `src/Njord/Pipeline/PipelineActor.cs`, passing `target.Cycle` to the client
- [x] 4.2 Delete `src/Njord/Pipeline/ExpandStage.cs`

## 5. Egress: decouple StatePayloadBuilder from CycleId

- [x] 5.1 Change `StatePayloadBuilder.BuildPerHorizon` in `src/Njord/Egress/StatePayloadBuilder.cs` — add an explicit `DateTimeOffset anchorTime` parameter, use it instead of `forecast.Cycle.Timestamp` for horizon and daily anchoring
- [x] 5.2 Update `MqttEgressActor.MaterializeConsumerGraph` in `src/Njord/Egress/MqttEgressActor.cs` — pass a `TimeProvider` into the consumer graph and supply `timeProvider.GetUtcNow()` as `anchorTime` to `BuildPerHorizon`

## 6. Config cleanup

- [x] 6.1 Remove `RetryBackoffMax` property from `NjordOptions` in `src/Njord/Configuration/NjordOptions.cs`
- [x] 6.2 Remove any `RetryBackoffMax` validation from `NjordOptionsValidator` in `src/Njord/Configuration/NjordOptionsValidator.cs` (if present)

## 7. Test updates

- [x] 7.1 Delete `src/Njord.Tests/ScaffoldSpec.cs`
- [x] 7.2 Delete `src/Njord.Tests/Pipeline/FetchStageSpec.cs`
- [x] 7.3 Delete `src/Njord.Tests/Pipeline/ExpandStageSpec.cs`
- [x] 7.4 Update or delete `src/Njord.Tests/Pipeline/CycleIdSpec.cs` — if `CycleId.From` is removed, delete the test; otherwise update
- [x] 7.5 Update `src/Njord.Tests/Pipeline/PollPipelineSpec.cs` — remove `FetchStage.Create()` usage, update `WeightedTarget` construction to include `CycleId`, update mock `FetchAsync` signature
- [x] 7.6 Update `src/Njord.Tests/Ingest/OpenMeteoClientSpec.cs` — remove `CycleId` param from `FetchAsync` calls, remove `RetrievedAt` assertions
- [x] 7.7 Update `src/Njord.Tests/Ingest/OpenMeteoSmokeSpec.cs` — remove `CycleId` construction and param
- [x] 7.8 Update `src/Njord.Tests/Domain/ModelForecastSpec.cs` — remove `RetrievedAt` from construction and assertions
- [x] 7.9 Update `src/Njord.Tests/Domain/ForecastDataHashSpec.cs` — update `ModelForecast` construction (no `RetrievedAt`)
- [x] 7.10 Update `src/Njord.Tests/Egress/StatePayloadBuilderSpec.cs` — pass explicit `anchorTime` instead of relying on `CycleId`
- [x] 7.11 Update `src/Njord.Tests/Egress/MqttEgressIntegrationSpec.cs` — update `ModelForecast` construction (no `RetrievedAt`)
- [x] 7.12 Update `src/Njord.Tests/Pipeline/SchedulerActorSpec.cs` — update `WeightedTarget` assertions to include `CycleId`

## 8. Validation

- [x] 8.1 Run `dotnet build Njord.slnx` from `src/` — verify zero errors and zero warnings
- [x] 8.2 Run `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/` — verify all tests pass
