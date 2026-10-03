## 1. Foundation Types

- [x] 1.1 Create `EndpointType` value type in `src/Njord/Endpoints/EndpointType.cs` — record struct with `Name` (string) and `Index` (int), equality by name. Tests in `src/Njord.Tests/Endpoints/EndpointTypeSpec.cs`.
- [x] 1.2 Make `WeightedTarget` abstract in `src/Njord/Pipeline/WeightedTarget.cs` — add `EndpointType` and abstract `Weight` properties, keep `Location` and `CycleId`. Update all existing usages to compile.
- [x] 1.3 Create `WeatherTarget` in `src/Njord/Endpoints/Weather/WeatherTarget.cs` — extends `WeightedTarget`, adds `WeatherModel Model`, implements `Weight` with `ceil(vars/10) * ceil(days/14)`. Tests in `src/Njord.Tests/Endpoints/Weather/WeatherTargetSpec.cs`.
- [x] 1.4 Create `IEndpointModule` interface in `src/Njord/Endpoints/IEndpointModule.cs` — `EndpointType`, `CreateTargets`, `BuildSubGraph`, `GetDeviceDefinitions`.

## 2. Weather Module Extraction

- [x] 2.1 Move `IOpenMeteoClient` → `IWeatherClient` in `src/Njord/Endpoints/Weather/IWeatherClient.cs`. Move `OpenMeteoClient` → `WeatherClient` in `src/Njord/Endpoints/Weather/WeatherClient.cs`. Update DI registration in `src/Njord/Endpoints/Weather/WeatherServiceCollectionExtensions.cs`.
- [x] 2.2 Rename `FetchOutcome` → `WeatherFetchOutcome` in `src/Njord/Endpoints/Weather/WeatherFetchOutcome.cs`. Update all references.
- [x] 2.3 Move weather enrichments to `src/Njord/Endpoints/Weather/Enrichments/` — ConsensusEnrichment, AlertEnrichment, DerivedEnrichment, TrendEnrichment, IndexEnrichment, EnergyEnrichment, HistoryEnrichment. Update namespaces and DI registrations.
- [x] 2.4 Create `WeatherModule : IEndpointModule` in `src/Njord/Endpoints/Weather/WeatherModule.cs` — composes `IWeatherClient`, snapshot builder, and enrichment features. Implements `CreateTargets` (iterates resolved models), `BuildSubGraph` (cast → fetch → snapshot → enrich → EgressEvent), `GetDeviceDefinitions`.
- [x] 2.5 Update `FakeOpenMeteoClient` → `FakeWeatherClient` in `src/Njord.Tests.Shared/FakeWeatherClient.cs`. Update all test references.
- [x] 2.6 Verify all existing tests compile and pass after namespace moves: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj`.

## 3. Partition Pipeline

- [x] 3.1 Refactor `PipelineActor` in `src/Njord/Pipeline/PipelineActor.cs` — inject `IReadOnlyList<IEndpointModule>`, assign `EndpointType.Index` per module at startup, build `Partition<WeightedTarget>(N, t => t.EndpointType.Index)` after `BudgetThrottleStage`, wire each outlet to the module's `BuildSubGraph()`, merge all outputs into `MergeHub<EgressEvent>`.
- [x] 3.2 Remove the inline `SelectAsyncUnordered` fetch call and hash feedback consumer from `PipelineActor` — these move into `WeatherModule.BuildSubGraph()`.
- [x] 3.3 Test partition routing in `src/Njord.Tests/Pipeline/PipelinePartitionSpec.cs` — verify that targets with different `EndpointType` values route to the correct outlet, that budget stage operates on base `Weight` only, and that module failure isolation holds.

## 4. Scheduler Adaptation

- [x] 4.1 Extend `ModelPollState` key from `(Location, Model)` to `(Location, EndpointType, Model?)` in `src/Njord/Pipeline/ModelPollState.cs`. Support null model for model-less endpoints.
- [x] 4.2 Refactor `SchedulerActor` in `src/Njord/Pipeline/SchedulerActor.cs` — inject `IReadOnlyList<IEndpointModule>`, iterate modules × locations to build poll state matrix, use `module.CreateTargets()` instead of constructing `WeightedTarget` directly, support per-endpoint poll intervals.
- [x] 4.3 Update persistence DTO for `DataChanged` event — add `EndpointType` field, default to `Weather` for legacy events without it. Increment DTO version. Tests in `src/Njord.Tests/Pipeline/SchedulerPersistenceSpec.cs`.
- [x] 4.4 Test per-endpoint scheduling in `src/Njord.Tests/Pipeline/SchedulerEndpointSpec.cs` — verify independent timers, correct target creation delegation, Discovery/Steady phases per (location, endpointType, model?) triple.

## 5. Configuration

- [x] 5.1 Add `EndpointsOptions` section to `src/Njord/Configuration/EndpointsOptions.cs` with per-endpoint `Enabled` (bool) and `PollInterval` (TimeSpan). Add `WeatherEndpointOptions` with defaults (Enabled=true, PollInterval=60min). Wire into `NjordOptions`.
- [x] 5.2 Update `appsettings.Development.json` and `appsettings.json` with `Endpoints` section structure.
- [x] 5.3 Update DI setup in `src/Njord/Configuration/NjordServiceSetup.cs` — register endpoint modules, remove old global enrichment registrations, wire `IReadOnlyList<IEndpointModule>`.

## 6. Cleanup and Integration

- [x] 6.1 Remove old `src/Njord/Ingest/` directory (or reduce to shared HTTP utilities if any remain). Remove old `IOpenMeteoClient`/`OpenMeteoClient` files. Remove old `FetchOutcome` if fully replaced.
- [x] 6.2 Remove or refactor `EnrichmentActor` — enrichment wiring is now inside each module's sub-graph. If `EnrichmentActor` becomes empty, remove it.
- [x] 6.3 Update `ModelSnapshot` references — if `ModelSnapshot` moves to `src/Njord/Endpoints/Weather/WeatherSnapshot.cs`, update all enrichment and test references.

## 7. Validation

- [x] 7.1 Run full test suite: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj`
- [x] 7.2 Run `dotnet slopwatch` from repo root to check for quality regressions.
- [x] 7.3 Verify build: `dotnet build src/Njord.slnx`
- [x] 7.4 Verify the service starts: `dotnet run --project src/Njord/Njord.csproj` (smoke test — no MQTT needed).
