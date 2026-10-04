## 1. Integration Test Fixture

- [x] 1.1 Create `NjordFixture` in `src/Njord.Tests/Infrastructure/NjordFixture.cs` — `IAsyncLifetime` fixture that builds a `WebApplicationBuilder` with `TestServer`, configures in-memory persistence, registers a `TestProbe` for every actor key in `ActorKeys.cs` (15 keys), exposes probes as properties, and provides `HttpClient` + gRPC `GrpcChannel`
- [x] 1.2 Create collection definitions in `src/Njord.Tests/Collections/` — `HealthCollection`, `WeatherCollection`, `OpsCollection`, `AdminCollection`, `SensorCollection`, each with `[CollectionDefinition]` and `ICollectionFixture<NjordFixture>`
- [x] 1.3 Migrate `src/Njord.Tests/Health/HealthEndpointSpec.cs` from `IClassFixture<WebApplicationFactory<Program>>` to `[Collection("Health")]` with `NjordFixture` — remove direct `WebApplicationFactory` usage, use fixture's `HttpClient`
- [x] 1.4 Add gRPC integration tests in `src/Njord.Tests/Grpc/` — one spec per gRPC service (WeatherGrpcIntegrationSpec, OpsGrpcIntegrationSpec, AdminGrpcIntegrationSpec, SensorGrpcIntegrationSpec) using the fixture's gRPC channel; verify that requests reach the probes and probe replies reach the client

## 2. Persistence Roundtrip Tests

- [x] 2.1 Add roundtrip test helper in `src/Njord.Tests.Shared/PersistenceRoundtripHelper.cs` — static method `AssertRoundtrip<T>(T dto)` that serializes with `JsonConvert.SerializeObject(dto, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All })`, deserializes, and asserts field-level equality
- [x] 2.2 Add roundtrip tests in `src/Njord.Persistence.Tests/BudgetTrackerDtoRoundtripSpec.cs` — `BudgetTrackerSnapshotDto_roundtrip()` and `ApiCallRecordedDto_roundtrip()` with representative field values
- [x] 2.3 Add roundtrip tests in `src/Njord.Persistence.Tests/SchedulerDtoRoundtripSpec.cs` — `SchedulerSnapshotDto_roundtrip()` and `DataChangedDto_roundtrip()` with nested `ModelPollStateDto`
- [x] 2.4 Add roundtrip tests in `src/Njord.Persistence.Tests/ForecastSnapshotDtoRoundtripSpec.cs` — `ForecastSnapshotDto_roundtrip()` with hourly and daily forecast points
- [x] 2.5 Add roundtrip tests in `src/Njord.Persistence.Tests/EnrichmentSnapshotDtoRoundtripSpec.cs` — `EnrichmentSnapshotDto_roundtrip()` with enrichment entries
- [x] 2.6 Add roundtrip tests in `src/Njord.Persistence.Tests/ForecastHistoryDtoRoundtripSpec.cs` — `ForecastHistorySnapshotDto_roundtrip()` and `ForecastRecordDto_roundtrip()`

## 3. E2E Test Project Setup

- [x] 3.1 Create `src/Njord.E2E.Tests/Njord.E2E.Tests.csproj` — executable test project with references to `Njord`, `Njord.Tests.Shared`, `Testcontainers`, `Verify.Xunit`, `MQTTnet`; add to `Njord.slnx`
- [x] 3.2 Create `src/Njord.E2E.Tests/Infrastructure/MosquittoFixture.cs` — `IAsyncLifetime` fixture that starts a Mosquitto container via Testcontainers, exposes the mapped MQTT port, and subscribes to `#` to collect all published messages
- [x] 3.3 Create `src/Njord.E2E.Tests/Infrastructure/FakeOpenMeteoHandler.cs` — `DelegatingHandler` that returns canned JSON responses from fixture files in `Njord.Tests.Shared`
- [x] 3.4 Create `src/Njord.E2E.Tests/Infrastructure/E2EFixture.cs` — `IAsyncLifetime` fixture composing `MosquittoFixture` + full njord host with `FakeTimeProvider`, `FakeOpenMeteoHandler`, and MQTT configured to the Testcontainers broker

## 4. E2E Test Scenarios

- [x] 4.1 Add `src/Njord.E2E.Tests/SingleModelHappyPathSpec.cs` — single location, single model, one poll cycle; Verify-snapshot all MQTT messages (discovery + state)
- [x] 4.2 Add `src/Njord.E2E.Tests/MultiModelConsensusSpec.cs` — single location, two models, one poll cycle; Verify consensus device payloads
- [x] 4.3 Add `src/Njord.E2E.Tests/EnrichmentPipelineSpec.cs` — verify alert/derived/trend/index/history enrichment payloads for a location
- [x] 4.4 Add `src/Njord.E2E.Tests/HaBirthRediscoverySpec.cs` — publish `online` to `homeassistant/status`, verify all discovery payloads are re-published
- [x] 4.5 Add `src/Njord.E2E.Tests/SensorPushIntegrationSpec.cs` — push indoor temperature via gRPC, trigger poll, verify enrichment uses the pushed value

## 5. Cleanup and Validation

- [x] 5.1 Remove `[Collection("HostIntegration")]` from specs that are migrated to the new fixture pattern; delete stale `WebApplicationFactory` usage
- [x] 5.2 Update `AGENTS.md` test counts and solution structure to reflect the new E2E project and updated test organization
- [x] 5.3 Add E2E test run instructions to `AGENTS.md` — Docker requirement, how to run E2E tests separately

## Validation

```bash
# From src/
dotnet build Njord.slnx
dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj --no-build
dotnet run --project Njord.Tests/Njord.Tests.csproj --no-build
dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj --no-build
# E2E (requires Docker)
dotnet run --project Njord.E2E.Tests/Njord.E2E.Tests.csproj --no-build
# Slopwatch (from repo root)
dotnet slopwatch analyze -d . --fail-on warning
# Format
dotnet format whitespace --verify-no-changes Njord.slnx
```
