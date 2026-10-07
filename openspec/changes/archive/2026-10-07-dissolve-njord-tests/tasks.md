## 1. Rename Integration.Tests to IntegrationTests

- [x] 1.1 Remove `Njord.Integration.Tests` from `Njord.slnx`, rename directory and csproj to `Njord.IntegrationTests`, re-add to solution
- [x] 1.2 Update all namespaces from `Njord.Integration.Tests` to `Njord.IntegrationTests` in all .cs files
- [x] 1.3 Update `AGENTS.md` references from `Njord.Integration.Tests` to `Njord.IntegrationTests`
- [x] 1.4 Update `DisabledTestArchitectureSpec.cs` exclusion from `Njord.Integration.Tests` to `Njord.IntegrationTests`

## 2. Move specs to IntegrationTests

- [x] 2.1 Move `NjordFixture.cs` and `Collections/TestCollections.cs` from `Njord.Tests/` to `Njord.IntegrationTests/`; update namespaces
- [x] 2.2 Move `HealthEndpointSpec.cs` from `Njord.Tests/Health/` to `Njord.IntegrationTests/Health/`; update namespace
- [x] 2.3 Move `OpsGrpcIntegrationSpec.cs` and `SensorGrpcIntegrationSpec.cs` from `Njord.Tests/Grpc/` to `Njord.IntegrationTests/Grpc/`; update namespaces
- [x] 2.4 Move `NjordServiceSetupSpec.cs` from `Njord.Tests/Configuration/` to `Njord.IntegrationTests/Configuration/`; update namespace
- [x] 2.5 Move `ActorKeyRegistrationSpec.cs` from `Njord.Tests/Configuration/` to `Njord.IntegrationTests/Configuration/`; update namespace
- [x] 2.6 Move `StreamShutdownTaskSpec.cs` from `Njord.Tests/Configuration/` to `Njord.IntegrationTests/Configuration/`; update namespace
- [x] 2.7 Copy gRPC proto client compilation (`<Protobuf>` items) from `Njord.Tests.csproj` to `Njord.IntegrationTests.csproj`; add required package references (`Akka.Hosting.TestKit`, `Grpc.Net.Client`, `Google.Protobuf`, `Grpc.Tools`)

## 3. Move specs to domain test projects

- [x] 3.1 Move `PollPipelineSpec.cs` from `Njord.Tests/Pipeline/` to `Njord.Pipeline.Tests/`; update namespace; add host reference if needed
- [x] 3.2 Move `ForecastHistoryDtoSerializationSpec.cs` + its `.verified.txt` files from `Njord.Tests/Persistence/` to `Njord.Persistence.Tests/`; update namespace
- [x] 3.3 Move `PipelineHealthCheckSpec.cs` and `MqttConnectionHealthCheckSpec.cs` to `Njord.IntegrationTests/Health/` (health checks are internal to host, need InternalsVisibleTo)
- [x] 3.4 Move `NjordActorSystemSetupSpec.cs` and `PersistenceBeforeActorsSpec.cs` from `Njord.Tests/Configuration/` to `Njord.IntegrationTests/Configuration/` (they need the host reference); update namespace

## 4. Delete Njord.Tests and clean up

- [x] 4.1 Remove `Njord.Tests` from `Njord.slnx`
- [x] 4.2 Delete `src/Njord.Tests/` directory
- [x] 4.3 Update `AGENTS.md` solution structure — remove `Njord.Tests`, update test counts
- [x] 4.4 Build solution, run all tests, run slopwatch, check formatting

## Validation

```bash
# From src/
dotnet build Njord.slnx
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
dotnet run --project Njord.IntegrationTests/Njord.IntegrationTests.csproj --no-build
# Slopwatch (from repo root)
dotnet slopwatch analyze -d . --fail-on warning
# Format
dotnet format whitespace --verify-no-changes Njord.slnx
```
