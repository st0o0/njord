## Purpose

Organization of test projects, their dependency boundaries, shared infrastructure, and which test types belong where.

## Requirements

### Requirement: Shared test infrastructure project
The solution SHALL contain a `Njord.Tests.Shared` class library project that holds JSON fixture files, reusable test fakes (`FakeOpenMeteoClient`), and test helpers (`MosquittoHelper`, `FixtureReader`). This project SHALL NOT be a test project and SHALL NOT contain any test classes.

#### Scenario: Shared project provides fixture files
- **WHEN** a test project references `Njord.Tests.Shared`
- **THEN** the JSON fixture files (`openmeteo-icon_eu-96h.json`, `openmeteo-icon_d2-96h.json`) SHALL be available via `FixtureReader`

#### Scenario: Shared project provides FakeOpenMeteoClient
- **WHEN** a test project needs a fake Open-Meteo client for non-container tests
- **THEN** it SHALL use `FakeOpenMeteoClient` from `Njord.Tests.Shared`

#### Scenario: Shared project provides MosquittoHelper
- **WHEN** a test project needs to collect retained MQTT messages from a Mosquitto container
- **THEN** it SHALL use `MosquittoHelper.CollectRetainedAsync` from `Njord.Tests.Shared`

### Requirement: Unit and actor tests in Njord.Tests
The `Njord.Tests` project SHALL contain only unit tests and actor lifecycle tests that require no Docker containers and no network I/O. It SHALL NOT depend on `Testcontainers`, `WireMock.Net.Testcontainers`, or `MQTTnet`.

#### Scenario: Unit tests run without Docker
- **WHEN** `dotnet run --project Njord.Tests/Njord.Tests.csproj` is executed without a Docker daemon
- **THEN** all tests SHALL pass

#### Scenario: Actor tests use in-process ActorSystem
- **WHEN** actor tests (EgressActorSpec, SchedulerActorSpec, etc.) run
- **THEN** they SHALL use `ActorSystem.Create` or Akka.Hosting test infrastructure without external services

### Requirement: Container integration tests in Njord.Tests.Integration
The `Njord.Tests.Integration` project SHALL contain tests that require Docker containers (WireMock, Mosquitto) but test individual layers in isolation (ingest against WireMock, MQTT egress against Mosquitto).

#### Scenario: WireMock integration tests run in Integration project
- **WHEN** `dotnet run --project Njord.Tests.Integration/Njord.Tests.Integration.csproj` is executed
- **THEN** `OpenMeteoClientIntegrationSpec` and `OpenMeteoSmokeSpec` SHALL execute

#### Scenario: Mosquitto integration tests run in Integration project
- **WHEN** `dotnet run --project Njord.Tests.Integration/Njord.Tests.Integration.csproj` is executed
- **THEN** `MqttEgressIntegrationSpec` SHALL execute

### Requirement: E2E pipeline tests in Njord.Tests.Integration.E2E
The `Njord.Tests.Integration.E2E` project SHALL contain only the full end-to-end pipeline test that exercises the complete data path from API fetch to MQTT publish.

#### Scenario: E2E test runs independently
- **WHEN** `dotnet run --project Njord.Tests.Integration.E2E/Njord.Tests.Integration.E2E.csproj` is executed
- **THEN** `EndToEndPipelineSpec` SHALL execute using both WireMock and Mosquitto containers

### Requirement: All test projects in the solution
The `Njord.slnx` solution file SHALL include all four test projects so `dotnet build Njord.slnx` compiles everything.

#### Scenario: Solution builds all test projects
- **WHEN** `dotnet build Njord.slnx` is executed
- **THEN** `Njord.Tests`, `Njord.Tests.Shared`, `Njord.Tests.Integration`, and `Njord.Tests.Integration.E2E` SHALL all compile successfully
