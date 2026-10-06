## 1. Delete Empty Project Directories

- [x] 1.1 Delete `src/Njord.Tests/` directory (empty, not in solution, not tracked)
- [x] 1.2 Delete `src/Njord.Integration.Tests/` directory (empty, not in solution, not tracked)
- [x] 1.3 Delete `src/Njord.E2E.Tests/` directory (empty, not in solution, not tracked)

## 2. Delete E2E Stubs and Fixture Infrastructure

- [x] 2.1 Delete `src/Njord.IntegrationTests/SingleModelHappyPathSpec.cs`
- [x] 2.2 Delete `src/Njord.IntegrationTests/MultiModelConsensusSpec.cs`
- [x] 2.3 Delete `src/Njord.IntegrationTests/EnrichmentPipelineSpec.cs`
- [x] 2.4 Delete `src/Njord.IntegrationTests/HaBirthRediscoverySpec.cs`
- [x] 2.5 Delete `src/Njord.IntegrationTests/SensorPushIntegrationSpec.cs`
- [x] 2.6 Delete `src/Njord.IntegrationTests/Infrastructure/E2EFixture.cs`
- [x] 2.7 Delete `src/Njord.IntegrationTests/Infrastructure/MosquittoFixture.cs`
- [x] 2.8 Delete `src/Njord.IntegrationTests/Infrastructure/FakeOpenMeteoHandler.cs`
- [x] 2.9 Remove `Testcontainers`, `MQTTnet`, and `Verify.XunitV3` package references from `src/Njord.IntegrationTests/Njord.IntegrationTests.csproj`
- [x] 2.10 Remove `Microsoft.AspNetCore.Mvc.Testing` package reference if no remaining spec uses `WebApplicationFactory<Program>` (check first)

## 3. Delete Redundant Wiring Tests

- [x] 3.1 Delete `src/Njord.IntegrationTests/Configuration/ActorKeyRegistrationSpec.cs`
- [x] 3.2 Delete `src/Njord.IntegrationTests/Configuration/NjordServiceSetupSpec.cs`
- [x] 3.3 Delete `src/Njord.IntegrationTests/Configuration/NjordActorSystemSetupSpec.cs`
- [x] 3.4 Verify `src/Njord.IntegrationTests/Configuration/PersistenceBeforeActorsSpec.cs` still compiles (kept)
- [x] 3.5 Verify `src/Njord.IntegrationTests/Configuration/StreamShutdownTaskSpec.cs` still compiles (kept)

## 4. Tests.Shared Cleanup

- [x] 4.1 Move `src/Njord.Tests.Shared/PersistenceRoundtripHelper.cs` to `src/Njord.Persistence.Tests/PersistenceRoundtripHelper.cs`, update namespace to `Njord.Persistence.Tests`
- [x] 4.2 Add `Receive<SubscribeInbound>(_ => { })` handler to `src/Njord.Tests.Shared/FailingRefProvider.cs`
- [x] 4.3 Delete `src/Njord.Mqtt.Tests/FailingRefProvider.cs` (local duplicate)
- [x] 4.4 Verify `Njord.Mqtt.Tests` compiles using the shared `FailingRefProvider`

## 5. Collection Cleanup

- [x] 5.1 Remove unused collection definitions from `src/Njord.IntegrationTests/Collections/TestCollections.cs` if any collections have zero remaining specs (Weather, Admin, Sensor collections may be empty after wiring test removal — check and remove if unused)

## 6. Documentation Update

- [x] 6.1 Update `AGENTS.md` test counts to reflect removed tests and the current totals
- [x] 6.2 Update `AGENTS.md` solution structure if any project listing references `Njord.Tests` or `Njord.Integration.Tests`

## Validation

```bash
# From src/
dotnet build Njord.slnx
dotnet run --project Njord.IntegrationTests/Njord.IntegrationTests.csproj --no-build
dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj --no-build
dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj --no-build
dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj --no-build
# Slopwatch (from repo root)
dotnet slopwatch analyze -d . --fail-on warning
# Format
dotnet format whitespace --verify-no-changes Njord.slnx
```
