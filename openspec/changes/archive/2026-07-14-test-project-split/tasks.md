## 1. Create Njord.Tests.Shared project

- [x] 1.1 Create `src/Njord.Tests.Shared/Njord.Tests.Shared.csproj` as a class library (no `<OutputType>Exe</OutputType>`, no `<IsTestProject>`) with project reference to `Njord.csproj` and package references for `xunit.v3.mtp-v2` (for assert utilities) and `MQTTnet` (for MosquittoHelper)
- [x] 1.2 Move `src/Njord.Tests/Ingest/Fixtures/openmeteo-icon_eu-96h.json` and `openmeteo-icon_d2-96h.json` to `src/Njord.Tests.Shared/Fixtures/` as Content items with `CopyToOutputDirectory`
- [x] 1.3 Create `src/Njord.Tests.Shared/FixtureReader.cs` — static helper `Fixture(string name)` that reads from `Fixtures/` relative to `AppContext.BaseDirectory`
- [x] 1.4 Extract `FakeOpenMeteoClient` from `src/Njord.Tests/Pipeline/PollPipelineSpec.cs` into `src/Njord.Tests.Shared/FakeOpenMeteoClient.cs` (public class, same logic)
- [x] 1.5 Extract `CollectRetainedAsync` helper from `src/Njord.Tests/Mqtt/MqttEgressIntegrationSpec.cs` into `src/Njord.Tests.Shared/MosquittoHelper.cs` (public static class)
- [x] 1.6 Add `Njord.Tests.Shared` to `src/Njord.slnx`

## 2. Create Njord.Tests.Integration project

- [x] 2.1 Create `src/Njord.Tests.Integration/Njord.Tests.Integration.csproj` as a test project (`<OutputType>Exe</OutputType>`, `<IsTestProject>true</IsTestProject>`) with project references to `Njord.csproj` and `Njord.Tests.Shared.csproj`, and package references for `xunit.v3.mtp-v2`, `Testcontainers`, `WireMock.Net.Testcontainers`, `MQTTnet`
- [x] 2.2 Move `src/Njord.Tests/Ingest/OpenMeteoClientIntegrationSpec.cs` to `src/Njord.Tests.Integration/Ingest/OpenMeteoClientIntegrationSpec.cs` — update namespace to `Njord.Tests.Integration.Ingest`, update fixture loading to use `FixtureReader`
- [x] 2.3 Move `src/Njord.Tests/Ingest/OpenMeteoSmokeSpec.cs` to `src/Njord.Tests.Integration/Ingest/OpenMeteoSmokeSpec.cs` — update namespace
- [x] 2.4 Move `src/Njord.Tests/Mqtt/MqttEgressIntegrationSpec.cs` to `src/Njord.Tests.Integration/Mqtt/MqttEgressIntegrationSpec.cs` — update namespace, replace inline `CollectRetainedAsync` with `MosquittoHelper`
- [x] 2.5 Add `xunit.runner.json` (copy from `Njord.Tests`)
- [x] 2.6 Add `Njord.Tests.Integration` to `src/Njord.slnx`

## 3. Create Njord.Tests.Integration.E2E project

- [x] 3.1 Create `src/Njord.Tests.Integration.E2E/Njord.Tests.Integration.E2E.csproj` as a test project with project references to `Njord.csproj` and `Njord.Tests.Shared.csproj`, and package references for `xunit.v3.mtp-v2`, `Testcontainers`, `WireMock.Net.Testcontainers`, `MQTTnet`
- [x] 3.2 Move `src/Njord.Tests/Integration/EndToEndPipelineSpec.cs` to `src/Njord.Tests.Integration.E2E/EndToEndPipelineSpec.cs` — update namespace to `Njord.Tests.Integration.E2E`, replace inline helpers with `FixtureReader` and `MosquittoHelper`
- [x] 3.3 Add `xunit.runner.json` (copy from `Njord.Tests`)
- [x] 3.4 Add `Njord.Tests.Integration.E2E` to `src/Njord.slnx`

## 4. Clean up Njord.Tests

- [x] 4.1 Remove `Testcontainers`, `WireMock.Net.Testcontainers` package references from `src/Njord.Tests/Njord.Tests.csproj`
- [x] 4.2 Add project reference to `Njord.Tests.Shared.csproj` in `src/Njord.Tests/Njord.Tests.csproj`
- [x] 4.3 Remove the moved files: `Ingest/OpenMeteoClientIntegrationSpec.cs`, `Ingest/OpenMeteoSmokeSpec.cs`, `Mqtt/MqttEgressIntegrationSpec.cs`, `Integration/EndToEndPipelineSpec.cs`, `Ingest/Fixtures/` directory
- [x] 4.4 Update `OpenMeteoClientSpec.cs` to use `FixtureReader` from Shared instead of inline `Fixture()` method
- [x] 4.5 Update `PollPipelineSpec.cs` to use `FakeOpenMeteoClient` from Shared instead of the inner class (remove the inner class)
- [x] 4.6 Remove the Content items for fixture JSON files from `Njord.Tests.csproj`

## 5. Update CLAUDE.md

- [x] 5.1 Update the "Build & test" section in `CLAUDE.md` to document all 4 test projects and their run commands

## 6. Validation

- [x] 6.1 Run `dotnet build src/Njord.slnx` — all projects compile (0 errors)
- [x] 6.2 Run `dotnet run --project src/Njord.Tests/Njord.Tests.csproj` — 352 unit + actor tests pass (no Docker)
- [x] 6.3 Run `dotnet run --project src/Njord.Tests.Integration/Njord.Tests.Integration.csproj` — 7 tests (6 pass, 1 smoke skipped)
- [x] 6.4 Run `dotnet run --project src/Njord.Tests.Integration.E2E/Njord.Tests.Integration.E2E.csproj` — 1 E2E test passes
- [x] 6.5 Run `dotnet slopwatch` from repo root — 1 pre-existing warning, no new regressions
