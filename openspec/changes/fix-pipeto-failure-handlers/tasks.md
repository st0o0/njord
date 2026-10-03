## 1. StreamConsumerActor subclasses — Pipeline/Egress/Grpc

- [ ] 1.1 Add `PipelineResolveFailed` record and `failure:` mapper + handler to `src/Njord.Pipeline/PipelineActor.cs` (line 49–50: `GetActorAsync<ISchedulerActor>`). Handle in `Initializing()` — log warning, schedule retry via scheduler.
- [ ] 1.2 Add `PipelineResolveFailed` record and `failure:` mapper + handler to `src/Njord.Pipeline/SchedulerActor.cs` (lines 75–76, 103–104, 254–255: three `GetActorAsync<IPipelineActor>` calls). Handle in `WaitingForPipeline()` and `RestartPipelineConnection()` — log warning, apply existing backoff retry.
- [ ] 1.3 Add `EgressResolveFailed` and `PipelineResolveFailed` records + `failure:` mappers + handlers to `src/Njord.Egress/ModelStateActor.cs` (lines 48–49: two `GetActorAsync` calls). Handle in `ConfigureWaitingForRefs()` — log warning, `ScheduleRetryResolve()`.
- [ ] 1.4 Add `EgressResolveFailed` and `SnapshotResolveFailed` records + `failure:` mappers + handlers to `src/Njord.Grpc/GrpcSnapshotConsumerActor.cs` (line 25: `GetActorAsync<IEgressActor>`, lines 50–51: `Task.WhenAll`). Handle in `ConfigureWaitingForRefs()` — log warning, `ScheduleRetryResolve()`.
- [ ] 1.5 Tests for tasks 1.1–1.4: verify that when `GetActorAsync` is not resolvable, the actor logs and retries. Add specs to `src/Njord.Pipeline.Tests/`, `src/Njord.Egress.Tests/`, `src/Njord.Grpc.Tests/`.

## 2. StreamConsumerActor subclasses — Enrichment/Mqtt

- [ ] 2.1 Add `PipelineResolveFailed`, `EgressResolveFailed`, `SensorHubResolveFailed` records + `failure:` mappers + handlers to `src/Njord.Enrichment/EnrichmentActor.cs` (lines 64–66: three `GetActorAsync` calls). Handle in `ConfigureWaitingForRefs()` — log warning, `ScheduleRetryResolve()`.
- [ ] 2.2 Add `EgressResolveFailed` and `ConnectionResolveFailed` records + `failure:` mappers + handlers to `src/Njord.Mqtt/MqttEgressActor.cs` (lines 61–62: two `GetActorAsync` calls). Handle in `ConfigureWaitingForRefs()` — log warning, `ScheduleRetryResolve()`.
- [ ] 2.3 Add `ConnectionResolveFailed` and `EgressResolveFailed` records + `failure:` mappers + handlers to `src/Njord.Mqtt/DiscoveryActor.cs` (lines 75–76: two `GetActorAsync` calls). Handle in `ConfigureWaitingForRefs()` — log warning, `ScheduleRetryResolve()`.
- [ ] 2.4 Tests for tasks 2.1–2.3: verify failure handling in the resolve path. Add specs to `src/Njord.Tests/` (Enrichment and Mqtt specs are host-resident).

## 3. Validation

- [ ] 3.1 Run all affected test suites:
  ```
  dotnet build src/Njord.slnx
  dotnet run --project src/Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj --no-build
  dotnet run --project src/Njord.Egress.Tests/Njord.Egress.Tests.csproj --no-build
  dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj --no-build
  dotnet run --project src/Njord.Tests/Njord.Tests.csproj --no-build
  ```
- [ ] 3.2 Run architecture tests: `dotnet run --project src/Njord.Architecture.Tests/Njord.Architecture.Tests.csproj --no-build`
- [ ] 3.3 Run slopwatch from repo root: `dotnet tool restore && dotnet slopwatch analyze -d . --fail-on warning`
- [ ] 3.4 Run dotnet format: `dotnet format src/Njord.slnx --verify-no-changes`
