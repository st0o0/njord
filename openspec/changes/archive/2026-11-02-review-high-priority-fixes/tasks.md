## 1. GrpcConsumer Reconnect

- [x] 1.1 Simplify `PreStart` in `src/Njord.Grpc/GrpcConsumer.cs`: replace `GetActorAsync<IForecastPipeline>().PipeTo(...)` with synchronous `Context.GetActor<IForecastPipeline>()` followed by `pipeline.Tell(new RequestPipelineOutput(0))`. Remove `PipelineResolved`, `PipelineResolveFailed` records and their handlers.
- [x] 1.2 Replace the `StreamCompleted` warning-only handler with a `ReconnectToPipeline()` method that resolves the pipeline synchronously and sends `RequestPipelineOutput(0)`. Log at Info level on reconnect attempt.
- [x] 1.3 Add a reconnect test in `src/Njord.Grpc.Tests/GrpcConsumerTests.cs`: after the consumer is attached and receiving events, complete the fake pipeline's source queue, then re-register a new fake pipeline and verify the consumer re-attaches and receives events from the new source.

## 2. ModelComparisonHelper Tests

- [x] 2.1 Create `src/Njord.Features.Tests/Forecasts/Shared/ModelComparisonHelperTests.cs` with tests: two models with overlapping days, past-only data returns empty, null temperature/precipitation preserved as null in `ModelDayValues`, 5-day window boundary (day 6 excluded).
- [x] 2.2 Create `src/Njord.Features.Tests/Forecasts/ForecastModelComparison/ForecastModelComparisonTests.cs` with tests: `Project` returns null when `ForecastModelMap` has < 2 models, returns `ForecastModelsInput` when >= 2; `Evaluate` returns `ForecastModelComparisonResult` with valid days.
- [x] 2.3 Create `src/Njord.Features.Tests/Forecasts/EnsembleModelComparison/EnsembleModelComparisonTests.cs` with tests: `Project` returns null when `EnsembleModelMap` has < 2 models, returns `EnsembleModelsInput` when >= 2; `Evaluate` returns `EnsembleModelComparisonResult` with valid days.

## 3. Validation

- [x] 3.1 Run `dotnet build Njord.slnx` from `src/`
- [x] 3.2 Run `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj` from `src/`
- [x] 3.3 Run `dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj` from `src/`
- [x] 3.4 Run all remaining test projects to check for regressions
- [x] 3.5 Run `dotnet format Njord.slnx --verify-no-changes` from `src/`
