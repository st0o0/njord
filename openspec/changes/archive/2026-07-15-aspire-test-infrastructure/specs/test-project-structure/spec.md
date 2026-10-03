## MODIFIED Requirements

### Requirement: Container integration tests in Njord.Tests.Integration
The `Njord.Tests.Integration` project SHALL contain tests that use the Aspire-managed infrastructure (WireMock, Mosquitto) via a shared fixture. It SHALL depend on `Aspire.Hosting.Testing` and reference the `Njord.AppHost` project. It SHALL NOT depend on `Testcontainers` or `WireMock.Net.Testcontainers`.

#### Scenario: WireMock integration tests run in Integration project
- **WHEN** `dotnet run --project Njord.Tests.Integration/Njord.Tests.Integration.csproj` is executed
- **THEN** `OpenMeteoClientIntegrationSpec` SHALL execute against the Aspire-managed WireMock

#### Scenario: Mosquitto integration tests run in Integration project
- **WHEN** `dotnet run --project Njord.Tests.Integration/Njord.Tests.Integration.csproj` is executed
- **THEN** `MqttEgressIntegrationSpec` SHALL execute against the Aspire-managed Mosquitto

### Requirement: E2E pipeline tests in Njord.Tests.Integration.E2E
The `Njord.Tests.Integration.E2E` project SHALL contain black-box end-to-end tests that boot the full Njord host via the Aspire fixture. It SHALL depend on `Aspire.Hosting.Testing` and reference the `Njord.AppHost` project. It SHALL NOT depend on `Testcontainers` or `WireMock.Net.Testcontainers`. Tests SHALL interact with the running host only via gRPC (trigger poll) and MQTT (assert retained messages).

#### Scenario: E2E test runs as black-box
- **WHEN** `dotnet run --project Njord.Tests.Integration.E2E/Njord.Tests.Integration.E2E.csproj` is executed
- **THEN** `EndToEndPipelineSpec` SHALL boot the full Njord host via Aspire and verify MQTT output

### Requirement: Shared test infrastructure project
The solution SHALL contain a `Njord.Tests.Shared` class library project that holds JSON fixture files, reusable test fakes (`FakeOpenMeteoClient`), and test helpers (`MosquittoHelper`, `FixtureReader`). This project SHALL NOT be a test project and SHALL NOT contain any test classes.

#### Scenario: Shared project provides fixture files
- **WHEN** a test project references `Njord.Tests.Shared`
- **THEN** the JSON fixture files (`openmeteo-icon_eu-96h.json`, `openmeteo-icon_d2-96h.json`) SHALL be available via `FixtureReader`

#### Scenario: Shared project provides MosquittoHelper
- **WHEN** a test project needs to collect retained MQTT messages from a Mosquitto container
- **THEN** it SHALL use `MosquittoHelper.CollectRetainedAsync` from `Njord.Tests.Shared`
