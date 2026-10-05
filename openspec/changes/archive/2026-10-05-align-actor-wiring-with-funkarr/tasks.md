## 1. Replace RetryBackoff with Servus BackoffPolicy

- [x] 1.1 Delete `src/Njord.Core/Actors/RetryBackoff.cs`. Add a static `BackoffPolicy` field in `StreamConsumerActor` (`Backoff.Create(initialDelay: 1s, maxDelay: 30s)`). Update `ScheduleRetryResolve()` to use `policy.DelayWithJitter(retryCount)`.
- [x] 1.2 Delete `src/Njord.Tests.Shared/TestRetryBackoffConfig.cs`. Remove `AddFastRetryBackoff()` calls from test `ConfigureAkka` methods. Replace with a fast `BackoffPolicy` override where needed.
- [x] 1.3 Update `SchedulerActor` (`src/Njord.Pipeline/SchedulerActor.cs`) retry logic: replace `RetryBackoff.For(Context.System, _pipelineRetryCount)` with the shared `BackoffPolicy.DelayWithJitter()`.
- [x] 1.4 Run `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj` and `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj` — all existing tests pass.

## 2. Rewrite host registration to WithSingleton

- [x] 2.1 Add `using Akka.Cluster.Hosting;` to `src/Njord/Configuration/NjordActorSystemSetup.cs`. Replace the `WithActors` callback with ordered `WithSingleton` calls following the tier graph from design.md (Tier 0 → Tier 3). Keep `WithShardRegion` for `ForecastHistoryRegion`. Keep `RegisterWithBackoff` helper refactored to use `WithSingleton` with `BackoffSupervisor.Props`.
- [x] 2.2 Move `AddStreamShutdownTask` to a separate `WithActors` callback that only registers the `CoordinatedShutdown` task (no actor creation).
- [x] 2.3 Run `dotnet run --project Njord.IntegrationTests/Njord.IntegrationTests.csproj` — `ActorKeyRegistrationSpec` and all integration tests pass. Verify registration order is correct.

## 3. Simplify standalone actors (Scheduler, Pipeline)

- [x] 3.1 `PipelineActor` (`src/Njord.Pipeline/PipelineActor.cs`): replace `GetActorAsync<ISchedulerActor>()` in `PreStart` with `Context.GetActor<ISchedulerActor>()` in constructor. Remove `Initializing()` Become-state, `SchedulerResolved`/`SchedulerResolveFailed` records. Call `MaterializePipeline(scheduler)` directly from `PreStart` (after `_mat` is initialized). Start in `Ready`.
- [x] 3.2 `SchedulerActor` (`src/Njord.Pipeline/SchedulerActor.cs`): replace `GetActorAsync<IPipelineActor>()` in `PreStart` with `Context.GetActor<IPipelineActor>()` in constructor. Remove `WaitingForPipeline()` Become-state, `PipelineResolved`/`PipelineResolveFailed`/`RetryPipelineResolve` records. Start in `Ready`. Keep `Terminated` handler for runtime recovery (re-resolve async).
- [x] 3.3 Update `src/Njord.Pipeline.Tests/SchedulerActorSpec.cs` and related specs: remove test setup that exercised the `WaitingForPipeline` init path. Update specs that depend on the `PipelineResolved` transition. Update `SchedulerActorGetPollStatesBeforeReadySpec` — the "before ready" scenario changes (actor starts ready immediately).
- [x] 3.4 Update `src/Njord.Pipeline.Tests/PipelineConnectionSpec.cs` and related specs: remove test setup that exercised the `Initializing` init path.
- [x] 3.5 Run `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj` — all tests pass.

## 4. Simplify StreamConsumerActor and subclasses

- [x] 4.1 `StreamConsumerActor` (`src/Njord.Core/Actors/StreamConsumerActor.cs`): add a `ResolveInitialDependencies()` virtual method returning sync-resolved refs. Call it in the constructor path. On first start, skip `WaitingForRefs` and go directly to `MaterializeGraph` → `Ready`. Keep `ResolveDependencies()` (async) for the `Terminated` recovery path only.
- [x] 4.2 `ModelStateActor` (`src/Njord.Egress/ModelStateActor.cs`): override `ResolveInitialDependencies()` to call `Context.GetActor<IPipelineActor>()` sync. Keep async `ResolveDependencies()` for recovery.
- [x] 4.3 `EnrichmentActor` (`src/Njord.Enrichment/EnrichmentActor.cs`): override sync init to resolve `IPipelineActor` and `ISensorHubActor`. Keep async recovery path.
- [x] 4.4 `GrpcSnapshotConsumerActor` (`src/Njord.Grpc/GrpcSnapshotConsumerActor.cs`): override sync init to resolve `IModelStateActor` and `IEnrichmentActor`. Keep async recovery path.
- [x] 4.5 `MqttDiscoveryActor` (`src/Njord.Mqtt/MqttDiscoveryActor.cs`): override sync init to resolve `IModelStateActor` and `IMqttConnectionActor`. Keep async recovery path.
- [x] 4.6 `MqttStateActor` (`src/Njord.Mqtt/MqttStateActor.cs`): override sync init to resolve `IModelStateActor` and `IEnrichmentActor`. Keep async recovery path.
- [x] 4.7 Update tests for `StreamConsumerActor` (`src/Njord.Core.Tests/Actors/StreamConsumerActorSpec.cs`): add test for sync init path, verify no `WaitingForRefs` on first start, verify `Terminated` still triggers async recovery.
- [x] 4.8 Run `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj` — all tests pass.

## 5. Update downstream test suites

- [x] 5.1 Run `dotnet run --project Njord.Egress.Tests/Njord.Egress.Tests.csproj` — fix any failures from ModelStateActor changes.
- [x] 5.2 Run `dotnet run --project Njord.Enrichment.Tests/Njord.Enrichment.Tests.csproj` — fix any failures from EnrichmentActor changes.
- [x] 5.3 Run `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj` — fix any failures from GrpcSnapshotConsumerActor changes.
- [x] 5.4 Run `dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj` — fix any failures from MqttDiscoveryActor and MqttStateActor changes.
- [x] 5.5 Run `dotnet run --project Njord.Sensors.Tests/Njord.Sensors.Tests.csproj` — verify no regressions.

## 6. Cleanup and validation

- [x] 6.1 Remove dead code: delete unused `XxxResolved`/`XxxResolveFailed` private records from standalone actors. Remove unused `using` statements.
- [x] 6.2 Run full test suite: `for p in Njord.*Tests; do [ "$p" = Njord.Tests.Shared ] && continue; dotnet run --project "$p/$p.csproj" --no-build; done` (after `dotnet build Njord.slnx`).
- [x] 6.3 Run `dotnet slopwatch analyze -d . --fail-on warning` from repo root.
- [x] 6.4 Run `dotnet format --verify-no-changes` from `src/`.
