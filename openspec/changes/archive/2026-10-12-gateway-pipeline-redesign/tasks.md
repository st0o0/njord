## 1. Actor & Key Renames (mechanical, no behavior change)

- [x] 1.1 Rename actor keys in `src/Njord.Core/Actors/ActorKeys.cs`: `ISchedulerActor`→`IPollScheduler`, `IPipelineActor`→`IForecastPipeline`, `IBudgetTrackerActor`→`IBudgetTracker`, `ISensorHubActor`→`ISensorHub`, `IGrpcConsumerActor`→`IGrpcConsumer`, `IMqttConnectionActor`→`IMqttConnection`, `IMqttStateActor`→`IMqttState`; add `IForecastGateway`. Update all references across `src/` (production and test code).
- [x] 1.2 Rename `SchedulerActor` → `PollScheduler` in `src/Njord.Pipeline/SchedulerActor.cs` (file + class). Update all references including test files in `src/Njord.Pipeline.Tests/SchedulerActor*.cs` (rename spec files to `PollScheduler*.cs`).
- [x] 1.3 Rename `PipelineActor` → `ForecastPipeline` in `src/Njord.Pipeline/PipelineActor.cs` (file + class). Update all references including `src/Njord.Pipeline.Tests/PipelineActorShutdownSpec.cs`, `PipelineConnectionSpec.cs`, `PollPipelineSpec.cs`.
- [x] 1.4 Rename `BudgetTrackerActor` → `BudgetTracker` in `src/Njord.Pipeline/BudgetTrackerActor.cs` (file + class). Update references.
- [x] 1.5 Rename `SensorHubActor` → `SensorHub` in `src/Njord.SensorHub/SensorHubActor.cs` (file + class). Update references including `src/Njord.SensorHub.Tests/`.
- [x] 1.6 Rename `GrpcConsumerActor` → `GrpcConsumer` in `src/Njord.Grpc/` (file + class). Update references including `src/Njord.Grpc.Tests/`.
- [x] 1.7 Rename `MqttConnectionActor` → `MqttConnection` and `MqttStateActor` → `MqttState` in `src/Njord.Mqtt/` (files + classes). Update references including `src/Njord.Mqtt.Tests/`.
- [x] 1.8 Update `src/Njord.Pipeline/Configuration/PipelineSetupContainer.cs` and all other SetupContainers to use new key names.
- [x] 1.9 Update `src/Njord.Architecture.Tests/LayerReferenceSpec.cs` and `ZoneArchitectureSpec.cs` for renamed types.
- [x] 1.10 Update AGENTS.md actor naming convention: remove "Actor naming: `XxxActor`, sealed" rule, replace with FunkArr-style domain-role naming. Update solution structure listing.
- [x] 1.11 Build (`dotnet build Njord.slnx`) and run all tests to verify renames are clean.

## 2. Project Rename: Njord.Ingest → Njord.OpenMeteo

- [x] 2.1 Rename `src/Njord.Ingest/` directory to `src/Njord.OpenMeteo/`. Rename `Njord.Ingest.csproj` → `Njord.OpenMeteo.csproj`. Update `<RootNamespace>` if set. Update all `<ProjectReference>` entries in other csproj files.
- [x] 2.2 Rename `src/Njord.Ingest.Tests/` directory to `src/Njord.OpenMeteo.Tests/`. Rename csproj. Update project references.
- [x] 2.3 Change namespace `Njord.Ingest` → `Njord.OpenMeteo` in all `.cs` files under the renamed projects. Update `using Njord.Ingest` → `using Njord.OpenMeteo` across the entire solution.
- [x] 2.4 Rename `IngestSetupContainer` → `OpenMeteoSetupContainer` in `src/Njord.OpenMeteo/Configuration/`. Update registration in host.
- [x] 2.5 Move `IOpenMeteoClient` from `src/Njord.Core/Ingest/IOpenMeteoClient.cs` to `src/Njord.OpenMeteo/IOpenMeteoClient.cs`. Change namespace from `Njord.Core.Ingest` to `Njord.OpenMeteo`. Delete `src/Njord.Core/Ingest/` directory. Update all `using Njord.Core.Ingest` → `using Njord.OpenMeteo`.
- [x] 2.6 Update `src/Njord.Tests.Shared/FakeOpenMeteoClient.cs`: change namespace, add `Njord.OpenMeteo` project reference to `Njord.Tests.Shared.csproj` if not already present.
- [x] 2.7 Update `src/Njord.Architecture.Tests/` for renamed project (assembly names in layer/zone specs).
- [x] 2.8 Update `Njord.slnx` solution file for renamed projects.
- [x] 2.9 Build and run all tests.

## 3. Create Njord.Budget Project

- [x] 3.1 Create `src/Njord.Budget/Njord.Budget.csproj` with references to `Njord.Core`. Add `<InternalsVisibleTo Include="Njord.Budget.Tests" />`.
- [x] 3.2 Move `BudgetTracker.cs` (renamed from `BudgetTrackerActor.cs` in step 1.4), `BudgetTrackerState.cs`, `BudgetTrackerDtoMapping.cs`, `BudgetTelemetry.cs` from `src/Njord.Pipeline/` to `src/Njord.Budget/`. Change namespace to `Njord.Budget`.
- [x] 3.3 Create `src/Njord.Budget/Configuration/BudgetSetupContainer.cs` — register `BudgetTracker` as BackoffSupervisor singleton with key `IBudgetTracker`. Register the meter in `NjordMeters`.
- [x] 3.4 Remove `BudgetTracker` registration from `src/Njord.Pipeline/Configuration/PipelineSetupContainer.cs`.
- [x] 3.5 Create `src/Njord.Budget.Tests/Njord.Budget.Tests.csproj`. Move budget tracker tests from `src/Njord.Pipeline.Tests/` to `src/Njord.Budget.Tests/`. Update namespaces.
- [x] 3.6 Add `Njord.Budget` project reference to host csproj (`src/Njord/Njord.csproj`). Register `BudgetSetupContainer` in host DI.
- [x] 3.7 Update `src/Njord.Architecture.Tests/` — add `Njord.Budget` to layer/zone specs.
- [x] 3.8 Update `Njord.slnx` solution file.
- [x] 3.9 Build and run all tests.

## 4. New Messages

- [x] 4.1 Create `src/Njord.Messages/Forecast/FetchForecast.cs`: `FetchForecast(string Location, string ModelId)`.
- [x] 4.2 Create `src/Njord.Messages/Forecast/ForecastFetched.cs`: `ForecastFetched(string Location, string ModelId, int Hash)`.
- [x] 4.3 Create `src/Njord.Messages/Forecast/ForecastFetchFailed.cs`: `ForecastFetchFailed(string Location, string ModelId, PollFailureReason Reason, string? Detail)`.
- [x] 4.4 Create `src/Njord.Messages/Forecast/RequestForecastStream.cs`: `RequestForecastStream(Guid RequestId)` + response hierarchy `RequestForecastStreamResponse` → `ForecastStreamReady(Guid RequestId, ISourceRef<Forecast> SourceRef)` / `ForecastStreamFailed(Guid RequestId, Exception Cause)`.

## 5. OpenMeteoGateway Actor

- [x] 5.1 Create `src/Njord.OpenMeteo/OpenMeteoGateway.cs` — `ReceiveActor` with `Source.Queue → BudgetThrottleStage → SelectAsyncUnordered(2, _client.FetchAsync) → WireTap(scheduler feedback via ForecastFetched/ForecastFetchFailed to requester) → DivertTo(failure sink) → Select(Success → Forecast) → BroadcastHub<Forecast>`. Pending request correlation via `Dictionary<string, IActorRef>`. Handle `FetchForecast`, `RequestForecastStream`. Internal token bucket (moved from `WeightedBudgetGate`). Tell `IBudgetTracker` via `RecordApiCall` on acquire. Compute hash via `ForecastDataHash.Compute()`.
- [x] 5.2 Move `BudgetThrottleStage.cs` from `src/Njord.Pipeline/` to `src/Njord.OpenMeteo/`. Change namespace to `Njord.OpenMeteo`. Internalize `IBudgetGate<T>` — the stage uses an internal interface or direct token-bucket access.
- [x] 5.3 Move token-bucket logic from `WeightedBudgetGate` into `OpenMeteoGateway` (or an internal helper class). The gateway reads `BudgetRate` from options and manages the token bucket. `WeightedTarget.ComputeWeight()` logic moves into the gateway.
- [x] 5.4 Register `OpenMeteoGateway` in `src/Njord.OpenMeteo/Configuration/OpenMeteoSetupContainer.cs` as BackoffSupervisor singleton with key `IForecastGateway`.
- [x] 5.5 Create `src/Njord.OpenMeteo.Tests/OpenMeteoGatewaySpec.cs` — test fetch request → SourceRef output, scheduler feedback (ForecastFetched/ForecastFetchFailed), budget recording, request correlation. Use `FakeOpenMeteoClient` and `TestProbe` for scheduler.
- [x] 5.6 Build and run `src/Njord.OpenMeteo.Tests/`.

## 6. Simplify ForecastPipeline

- [x] 6.1 Rewrite `src/Njord.Pipeline/ForecastPipeline.cs` (was PipelineActor): remove `IOpenMeteoClient`, `IBudgetGate`, `KillSwitch`, `StopStreams`/`StreamsStopped` handling. In `PreStart`, resolve `IForecastGateway` via `Context.GetActor<IForecastGateway>()`, Ask for `RequestForecastStream`, receive `ForecastStreamReady` with `SourceRef<Forecast>`. Build analysis stream: `sourceRef.Source → ZipWith(sensorSource) → StatefulSelectMany(analysis) → BroadcastHub<PipelineEvent>`. Keep `RequestPipelineOutput` handler unchanged.
- [x] 6.2 Remove constructor parameters: `IOpenMeteoClient`, `IBudgetGate<WeightedTarget>`. Add retry-backoff for SourceRef request from Gateway (handle `ForecastStreamFailed`, retry with backoff).
- [x] 6.3 Update `src/Njord.Pipeline/Configuration/PipelineSetupContainer.cs`: remove `IBudgetGate`, `IBudgetProvider`, `WeightedBudgetGate` DI registrations. Remove `AddStreamShutdownTask` and the `StopStreamsOf` helper. Simplify `ForecastPipeline` registration (no KillSwitch shutdown coordination).
- [x] 6.4 Update `src/Njord.Pipeline.Tests/PollPipelineSpec.cs` and `PipelineConnectionSpec.cs` for new ForecastPipeline (receives SourceRef<Forecast> instead of WeightedTarget).
- [x] 6.5 Delete `src/Njord.Pipeline.Tests/PipelineActorShutdownSpec.cs`.
- [x] 6.6 Build and run `src/Njord.Pipeline.Tests/`.

## 7. Simplify PollScheduler

- [x] 7.1 Update `src/Njord.Pipeline/PollScheduler.cs` (was SchedulerActor): resolve `IForecastGateway` via `Context.GetActor<IForecastGateway>()` instead of `IPipelineActor`. Watch Gateway instead of Pipeline.
- [x] 7.2 Change `OnScheduledPoll`: send `FetchForecast(location, model)` to Gateway instead of building `WeightedTarget` and telling Pipeline. Remove `_weight` field, `ResolvedParameterSet` constructor parameter, and `WeightedTarget.ComputeWeight()` call.
- [x] 7.3 Replace `HashResult` handler with `ForecastFetched` handler (same logic — compare hash, Changed/Miss). Replace `FetchFailed` handler with `ForecastFetchFailed` handler (same logic — apply transient failure/rate limit).
- [x] 7.4 Update `WaitingForPipeline` → `WaitingForGateway`: resolve `IForecastGateway` instead of `IPipelineActor`. Rename `PipelineResolved` → `GatewayResolved`.
- [x] 7.5 Update `src/Njord.Pipeline.Tests/SchedulerActor*.cs` test files (renamed to `PollScheduler*.cs` in step 1.2): update to use `FetchForecast`/`ForecastFetched`/`ForecastFetchFailed` messages. Remove `WeightedTarget` usage.
- [x] 7.6 Build and run `src/Njord.Pipeline.Tests/`.

## 8. Delete Dead Code

- [x] 8.1 Delete `src/Njord.Pipeline/IBudgetGate.cs` (contains `IBudgetGate<T>`, `WeightedBudgetGate`).
- [x] 8.2 Delete `src/Njord.Pipeline/IBudgetProvider.cs` (contains `IBudgetProvider`, `OptionsBudgetProvider`, `BudgetRate`). Move `BudgetRate` to `Njord.OpenMeteo` if still needed by the gateway's token bucket.
- [x] 8.3 Delete `src/Njord.Messages/Pipeline/WeightedTarget.cs`.
- [x] 8.4 Delete `src/Njord.Messages/Pipeline/HashResult.cs` and `FetchFailed.cs` (if separate files) or remove records from their containing files.
- [x] 8.5 Delete `src/Njord.Messages/Pipeline/StopStreams.cs` (or remove `StopStreams`/`StreamsStopped` records from their containing file).
- [x] 8.6 Delete `src/Njord.Pipeline.Tests/BudgetThrottleStageSpec.cs` (stage moved to OpenMeteo — tests move or are rewritten in step 5.5). Delete `src/Njord.Pipeline.Tests/WeightedTargetSpec.cs`.
- [x] 8.7 Build and verify no dangling references.

## 9. Standardize IActorRef Resolution

- [x] 9.1 Update `src/Njord.Grpc/SensorGrpcService.cs`: replace eager `actorRegistry.Get<ISensorHub>()` with lazy `actorRegistry.GetAsync<ISensorHub>(ct)` per-request.
- [x] 9.2 Verify `src/Njord.Grpc/OpsGrpcService.cs` already uses `GetAsync<T>()` (no change needed — just confirm).
- [x] 9.3 Update `src/Njord.Grpc.Tests/` for any SensorGrpcService test changes.
- [x] 9.4 Build and run `src/Njord.Grpc.Tests/`.

## 10. Update Architecture Tests & Documentation

- [x] 10.1 Update `src/Njord.Architecture.Tests/LayerReferenceSpec.cs`: add `Njord.OpenMeteo` and `Njord.Budget` as feature libraries. Remove `Njord.Ingest`. Verify reference directions.
- [x] 10.2 Update `src/Njord.Architecture.Tests/ZoneArchitectureSpec.cs`: add new projects to lateral independence checks.
- [x] 10.3 Update AGENTS.md: solution structure (add `Njord.OpenMeteo/`, `Njord.Budget/`, remove `Njord.Ingest/`), actor naming convention, test counts, architecture guardrails (update data flow description).
- [x] 10.4 Run `src/Njord.Architecture.Tests/` to verify all zone/layer rules pass.

## 11. Final Validation

- [x] 11.1 Build entire solution: `dotnet build Njord.slnx` from `src/`.
- [x] 11.2 Run all test projects sequentially:
```bash
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
```
- [x] 11.3 Run `dotnet format Njord.slnx --verify-no-changes` to verify formatting.
- [x] 11.4 Run `openspec validate --all --no-interactive` from repo root.
- [x] 11.5 Update test counts in AGENTS.md to match actual results.
