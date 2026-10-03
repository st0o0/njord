## 1. Pipeline graphs

- [x] 1.1 `src/Njord/Pipeline/PipelineActor.cs` — add `.Log("pipeline-fetch-in", ..., _log)` after `BudgetThrottleStage` and `.Log("pipeline-fetch-out", ..., _log)` after `SelectAsyncUnordered` in the main fetch graph; add `.Log("pipeline-hash", ..., _log)` after `.Select(HashResult)` in the hash feedback loop
- [x] 1.2 `src/Njord/Pipeline/SchedulerActor.cs` — add `.Log("pipeline-failure", ..., _log)` after `.Select(FetchFailed)` in the failure consumer graph

## 2. Egress graphs

- [x] 2.1 `src/Njord/Egress/EgressActor.cs` — add `.Log("egress-hub", ...)` between `mergeHubSource` and `.To(broadcastHubSink)`. Note: `EgressActor` needs a `_log` field initialized in `PreStart`
- [x] 2.2 `src/Njord/Egress/ModelStateActor.cs` — add `.Log("egress-in", ..., _log)` after `.Via(killSwitch)` and `.Log("egress-out", ..., _log)` after `.SelectMany`

## 3. Enrichment graphs

- [x] 3.1 `src/Njord/Enrichment/EnrichmentActor.cs` — add `.Log("enrichment-snapshot", ..., _log)` on the output of `BuildScanSource` (before killSwitch) and `.Log("enrichment-out", ..., _log)` after the consensus+inline flow, before the egress sink

## 4. MQTT graphs

- [x] 4.1 `src/Njord/Mqtt/MqttEgressActor.cs` — add `.Log("mqtt-egress-in", ..., _log)` after `.Via(killSwitch)` and `.Log("mqtt-egress-out", ..., _log)` after `.SelectMany(MapToMqttMessages)`
- [x] 4.2 `src/Njord/Mqtt/MqttConnectionActor.cs` — add `.Log("mqtt-send", ..., _log)` on `hubSource` before `.SelectAsync(SendAsync)`
- [x] 4.3 `src/Njord/Mqtt/DiscoveryActor.cs` — add `.Log("discovery-capability", ..., _log)` after `.Where(CapabilityLearned)` in the capability listener graph

## 5. gRPC graphs

- [x] 5.1 `src/Njord/Grpc/GrpcSnapshotConsumerActor.cs` — add `.Log("grpc-snapshot-in", ..., _log)` after `.Via(_killSwitch)` in the snapshot consumer graph
- [x] 5.2 `src/Njord/Grpc/WeatherGrpcService.cs` — add `.Log("grpc-stream-forecast", ...)` after `.Where(location filter)` in `StreamForecasts` and `.Log("grpc-stream-enrichment", ...)` after `.Where(location filter)` in `StreamEnrichments`. Note: non-actor, no `_log` — omit third parameter or use a logger from DI

## 6. Configuration

- [x] 6.1 `src/Njord/appsettings.Example.json` — add commented Serilog override examples for stream tracing activation

## 7. Validation

- [x] 7.1 Build: `dotnet build Njord.slnx` from `src/`
- [x] 7.2 Tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 7.3 Run `dotnet slopwatch` from repo root, then `dotnet format` whitespace check
