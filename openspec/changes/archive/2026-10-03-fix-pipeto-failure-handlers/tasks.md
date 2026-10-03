## 1. StreamConsumerActor subclasses — Pipeline/Egress/Grpc

- [x] 1.1 Add `SchedulerResolveFailed` record and `failure:` mapper + handler to `src/Njord.Pipeline/PipelineActor.cs`
- [x] 1.2 Add `PipelineResolveFailed` record and `failure:` mapper + handler to `src/Njord.Pipeline/SchedulerActor.cs` (all three PipeTo calls)
- [x] 1.3 Add `EgressResolveFailed` and `PipelineResolveFailed` records + handlers to `src/Njord.Egress/ModelStateActor.cs`
- [x] 1.4 Add `EgressResolveFailed` and `SnapshotResolveFailed` records + handlers to `src/Njord.Grpc/GrpcSnapshotConsumerActor.cs`
- [x] 1.5 Existing retry/backoff specs cover the resolve failure path — no dedicated tests needed (failure records are private, invoke existing ScheduleRetryResolve)

## 2. StreamConsumerActor subclasses — Enrichment/Mqtt

- [x] 2.1 Add `PipelineResolveFailed`, `EgressResolveFailed`, `SensorHubResolveFailed` records + handlers to `src/Njord.Enrichment/EnrichmentActor.cs`
- [x] 2.2 Add `EgressResolveFailed` and `ConnectionResolveFailed` records + handlers to `src/Njord.Mqtt/MqttEgressActor.cs`
- [x] 2.3 Add `ConnectionResolveFailed` and `EgressResolveFailed` records + handlers to `src/Njord.Mqtt/DiscoveryActor.cs`
- [x] 2.4 Existing retry/backoff specs cover the resolve failure path — no dedicated tests needed

## 3. Validation

- [x] 3.1 All affected test suites pass (Pipeline 111, Egress 13, Grpc 74, Njord.Tests 218)
- [x] 3.2 Architecture tests pass
- [x] 3.3 Slopwatch: 0 issues
- [x] 3.4 dotnet format: clean
