## Context

`Njord.Tests` is a catch-all with 12 specs spanning configuration, gRPC, health, persistence, and pipeline concerns. FunkArr has no equivalent — tests live in domain projects or `FunkArr.IntegrationTests`. Dissolving it eliminates file contention and makes test ownership clear.

## Goals / Non-Goals

**Goals:**
- Every spec in its domain's test project or in `Njord.IntegrationTests`
- `Njord.Tests` project deleted
- `Njord.Integration.Tests` renamed to `Njord.IntegrationTests`

**Non-Goals:**
- Changing test logic
- Adding new tests

## Decisions

### D1: Move map

| Spec | From | To | Reason |
|---|---|---|---|
| `PollPipelineSpec` | Njord.Tests/Pipeline | Njord.Pipeline.Tests | Tests pipeline code |
| `ForecastHistoryDtoSerializationSpec` | Njord.Tests/Persistence | Njord.Persistence.Tests | Tests persistence DTOs |
| `MqttConnectionHealthCheckSpec` | Njord.Tests/Health | Njord.Core.Tests | HealthCheck is in Core |
| `PipelineHealthCheckSpec` | Njord.Tests/Health | Njord.Core.Tests | HealthCheck is in Core |
| `NjordActorSystemSetupSpec` | Njord.Tests/Configuration | Njord.Core.Tests | Tests static config logic |
| `PersistenceBeforeActorsSpec` | Njord.Tests/Configuration | Njord.Core.Tests | Tests static config logic |
| `NjordServiceSetupSpec` | Njord.Tests/Configuration | Njord.IntegrationTests | Boots ServiceProvider |
| `ActorKeyRegistrationSpec` | Njord.Tests/Configuration | Njord.IntegrationTests | Boots Akka actor system |
| `StreamShutdownTaskSpec` | Njord.Tests/Configuration | Njord.IntegrationTests | Boots Akka actor system |
| `HealthEndpointSpec` | Njord.Tests/Health | Njord.IntegrationTests | Uses NjordFixture |
| `OpsGrpcIntegrationSpec` | Njord.Tests/Grpc | Njord.IntegrationTests | Uses NjordFixture |
| `SensorGrpcIntegrationSpec` | Njord.Tests/Grpc | Njord.IntegrationTests | Uses NjordFixture |

### D2: NjordFixture moves to IntegrationTests

`NjordFixture`, collection definitions, and the gRPC proto compilation (`GrpcServices="Client"`) move from `Njord.Tests` to `Njord.IntegrationTests`.

### D3: Projects gaining specs may need new references

- `Njord.Pipeline.Tests` needs `Njord` host reference for `PollPipelineSpec` (it creates `PipelineActor` with DI)
- `Njord.Core.Tests` needs `Njord` host reference for `NjordActorSystemSetupSpec` and health checks
- These references are acceptable since the specs test host-level wiring

Actually, `NjordActorSystemSetupSpec` and health check specs depend on the host project. If adding the host reference to Core.Tests breaks architecture rules, they go to IntegrationTests instead.

### D4: Rename approach

Rename `Njord.Integration.Tests` → `Njord.IntegrationTests` via:
1. Remove from solution
2. Rename directory and csproj
3. Update namespaces
4. Re-add to solution

## Risks / Trade-offs

- **[Reference additions]** → Some test projects gain the host reference. If this violates architecture rules, those specs go to IntegrationTests instead.
- **[Namespace changes]** → All moved specs need namespace updates. Low risk since it's a mechanical change.
