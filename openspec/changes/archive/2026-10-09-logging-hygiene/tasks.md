## 1. Named placeholders

- [ ] 1.1 Replace positional `{0}`, `{1}` placeholders with named placeholders in `src/Njord.Grpc/ForecastSnapshotActor.cs` (line 66).
- [ ] 1.2 Same in `src/Njord.Grpc/EnrichmentSnapshotActor.cs` (lines 64-79).
- [ ] 1.3 Same in `src/Njord.Pipeline/Configuration/PipelineSetupContainer.cs` (lines 78-83).
- [ ] 1.4 Pass exception objects to log methods instead of `ex.Message` interpolation in snapshot actor failure handlers (`ForecastSnapshotActor.cs` line 75, `EnrichmentSnapshotActor.cs` line 73).

## 2. Log-level demotion

- [ ] 2.1 Demote routine-operation log calls from `_log.Info` to `_log.Debug` in `src/Njord.Mqtt/MqttDiscoveryActor.cs` (7 call sites: "ready", "idle", "late capability", "capability expanded", "back online", etc.).
- [ ] 2.2 Same in `src/Njord.Enrichment/EnrichmentActor.cs` (2 call sites: "enrichment computed" per cycle).
- [ ] 2.3 Same in `src/Njord.Pipeline/SchedulerActor.cs` (3 call sites: "resolved", "initial polls", "TriggerImmediatePoll" results).
- [ ] 2.4 Keep Info level for lifecycle events: actor started, pipeline resolved (first time), MQTT connected/disconnected, config reloaded.

## 3. OpsGrpcService timeout handling

- [ ] 3.1 Add `bool budget_partial` and `bool models_partial` fields to `StatusResponse` in the proto definition.
- [ ] 3.2 Update `OpsGrpcService.GetStatus()` to set the partial flags when `AskTimeoutException` is caught instead of silently returning zero values.
- [ ] 3.3 Update `OpsGrpcServiceSpec` to cover the partial-response path.

## Validation

- [ ] 4.1 `dotnet build Njord.slnx` (run from `src/`)
- [ ] 4.2 `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj --no-build`
- [ ] 4.3 `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj --no-build`
- [ ] 4.4 `dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj --no-build`
- [ ] 4.5 `dotnet run --project Njord.Enrichment.Tests/Njord.Enrichment.Tests.csproj --no-build`
- [ ] 4.6 `dotnet slopwatch analyze -d . --fail-on warning` (run from repo root)
