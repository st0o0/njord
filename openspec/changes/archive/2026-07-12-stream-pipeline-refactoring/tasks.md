## 1. Command Protocol & Expand Stage

- [x] 1.1 Create `src/Njord/Pipeline/PipelineCommand.cs` — sealed abstract record with `PollAll`, `RefreshLocation(string Location)`, `RefreshModel(string Location, WeatherModel Model)` variants
- [x] 1.2 Create `src/Njord/Pipeline/WeightedTarget.cs` — record carrying `LocationOptions`, `WeatherModel`, and `int Weight` (computed via `ceil(hourlyVars/10) × ceil(days/14)`)
- [x] 1.3 Create `src/Njord/Pipeline/ExpandStage.cs` — static factory returning `Flow<PipelineCommand, WeightedTarget, NotUsed>` using `MapConcat`; validates location/model against config, logs warnings for unknown refs
- [x] 1.4 Create `src/Njord.Tests/Pipeline/ExpandStageSpec.cs` — tests: PollAll fan-out count, RefreshLocation filters correctly, RefreshModel single target, unknown location emits zero + warning, weight calculation for default and extended configs

## 2. Fetch Stage

- [x] 2.1 Create `src/Njord/Pipeline/FetchStage.cs` — static factory returning `Flow<WeightedTarget, FetchOutcome, NotUsed>` using `SelectAsyncUnordered(maxParallelism, ...)` calling `IOpenMeteoClient.FetchAsync`; supervision decider resumes on transient errors
- [x] 2.2 Create `src/Njord.Tests/Pipeline/FetchStageSpec.cs` — tests: parallel fetches respect parallelism cap, transient failure emits `FetchOutcome.Failure` without killing stream, successful fetch emits `FetchOutcome.Success`

## 3. Publish Stage

- [x] 3.1 Create `src/Njord/Pipeline/PublishStage.cs` — static factory returning `Sink<FetchOutcome, NotUsed>`; maps `FetchOutcome.Success` to device state payload and publishes via `IMqttPublisher`; discards `Failure` outcomes; logs structured entry per outcome (location, model, duration, status)
- [x] 3.2 Create `src/Njord.Tests/Pipeline/PublishStageSpec.cs` — tests: success outcome triggers publish, failure outcome does not publish, broker unavailability does not throw

## 4. Pipeline Composition & MergeHub

- [x] 4.1 Rewrite `src/Njord/Pipeline/PollPipeline.cs` — compose full graph: `MergeHub.Source<PipelineCommand>` → `ExpandStage` → weighted `Throttle(480/min, cost: t.Weight)` → `FetchStage` → `PublishStage`; return `(Sink<PipelineCommand, NotUsed>, UniqueKillSwitch)` from a `Create` factory using `PreMaterialize`
- [x] 4.2 Create tick source attachment: `Source.Tick` → `PollAll` → feed into the materialized MergeHub sink; wrap tick in `RestartSource.WithBackoff`
- [x] 4.3 Create `src/Njord.Tests/Pipeline/PollPipelineSpec.cs` (rewrite) — integration test: TestSource → full pipeline → TestSink verifying end-to-end flow; tick-driven test verifying interval semantics

## 5. Host Integration & Cleanup

- [x] 5.1 Replace `PipelineGuardianActor` with pipeline hosting in DI/`IHostedService` — materialize graph, attach tick source, register `KillSwitch` with `IHostApplicationLifetime.ApplicationStopping`
- [x] 5.2 Remove `CycleResult`, `CycleId` types and update `FetchOutcome` to drop `CycleId` field from `Failure` variant
- [x] 5.3 Update `PublishTelemetry` message and `MqttConnectionActor` to accept individual `FetchOutcome.Success` publishes instead of batched `IReadOnlyList<ModelForecast>` (or replace with direct `IMqttPublisher` call from the sink)
- [x] 5.4 Remove `FetchTarget` record (replaced by `WeightedTarget`)
- [x] 5.5 Update Akka.Hosting actor registration to remove `PipelineGuardianActor`; ensure remaining actors (`MqttConnectionActor`) are still registered

## 6. Validation

- [x] 6.1 Run full test suite: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj`
- [x] 6.2 Run build: `dotnet build src/Njord.slnx`
- [x] 6.3 Verify service starts cleanly: `dotnet run --project src/Njord/Njord.csproj` (confirm no startup crash, tick fires, first fetch logged)
- [x] 6.4 Run `dotnet slopwatch` from repo root
