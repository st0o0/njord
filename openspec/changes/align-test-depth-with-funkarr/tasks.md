> Re-audited 2026-10-07 against actual code: sections 1 and 2 are genuinely
> implemented. Sections 3–5's E2E-project tasks had been checked off without
> a `Njord.E2E.Tests` project ever being created — a different
> (agent-orchestrated, Docker-Compose based) E2E approach was built instead
> and never reconciled with this change. Checkboxes below reflect the code.

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

- [ ] 3.1 Create `src/Njord.E2E.Tests/Njord.E2E.Tests.csproj` — executable test project with references to `Njord`, `Njord.Tests.Shared`, `Testcontainers`, `Verify.Xunit`, `MQTTnet`; add to `Njord.slnx` (project does not exist; `a90daee feat: add agent-orchestrated E2E test infrastructure` built a different, Docker-Compose + browser-automation based E2E setup instead — see the `e2e-test` skill)
- [ ] 3.2 Create `src/Njord.E2E.Tests/Infrastructure/MosquittoFixture.cs` — `IAsyncLifetime` fixture that starts a Mosquitto container via Testcontainers, exposes the mapped MQTT port, and subscribes to `#` to collect all published messages
- [ ] 3.3 Create `src/Njord.E2E.Tests/Infrastructure/FakeOpenMeteoHandler.cs` — `DelegatingHandler` that returns canned JSON responses from fixture files in `Njord.Tests.Shared`
- [ ] 3.4 Create `src/Njord.E2E.Tests/Infrastructure/E2EFixture.cs` — `IAsyncLifetime` fixture composing `MosquittoFixture` + full njord host with `FakeTimeProvider`, `FakeOpenMeteoHandler`, and MQTT configured to the Testcontainers broker

## 4. E2E Test Scenarios

- [ ] 4.1 Add `src/Njord.E2E.Tests/SingleModelHappyPathSpec.cs` — single location, single model, one poll cycle; Verify-snapshot all MQTT messages (discovery + state)
- [ ] 4.2 Add `src/Njord.E2E.Tests/MultiModelConsensusSpec.cs` — single location, two models, one poll cycle; Verify consensus device payloads
- [ ] 4.3 Add `src/Njord.E2E.Tests/EnrichmentPipelineSpec.cs` — verify alert/derived/trend/index/history enrichment payloads for a location
- [ ] 4.4 Add `src/Njord.E2E.Tests/HaBirthRediscoverySpec.cs` — publish `online` to `homeassistant/status`, verify all discovery payloads are re-published
- [ ] 4.5 Add `src/Njord.E2E.Tests/SensorPushIntegrationSpec.cs` — push indoor temperature via gRPC, trigger poll, verify enrichment uses the pushed value

## 5. Cleanup and Validation

- [x] 5.1 Remove `[Collection("HostIntegration")]` from specs that are migrated to the new fixture pattern; delete stale `WebApplicationFactory` usage
- [ ] 5.2 Update `AGENTS.md` test counts and solution structure to reflect the new E2E project and updated test organization (no `Njord.E2E.Tests` project exists to document)
- [ ] 5.3 Add E2E test run instructions to `AGENTS.md` — Docker requirement, how to run E2E tests separately (N/A until 3.1–3.4/4.1–4.5 are actually built, or the change is rescoped to the agent-orchestrated approach)

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
