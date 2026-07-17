## Purpose

Organization of test projects, their dependency boundaries, shared infrastructure, and which test types belong where.

## Requirements

### Requirement: Shared test infrastructure project
The solution SHALL contain a `Njord.Tests.Shared` class library project that holds JSON fixture files, reusable test fakes (`FakeOpenMeteoClient`), and test helpers (`FixtureReader`). This project SHALL NOT be a test project and SHALL NOT contain any test classes.

#### Scenario: Shared project provides fixture files
- **WHEN** a test project references `Njord.Tests.Shared`
- **THEN** the JSON fixture files SHALL be available via `FixtureReader`

#### Scenario: Shared project provides FakeOpenMeteoClient
- **WHEN** a test project needs a fake Open-Meteo client for non-container tests
- **THEN** it SHALL use `FakeOpenMeteoClient` from `Njord.Tests.Shared`

### Requirement: Unit and actor tests in Njord.Tests
The `Njord.Tests` project SHALL contain only unit tests and actor lifecycle tests that require no Docker containers and no network I/O. It SHALL NOT depend on `Testcontainers`, `WireMock.Net.Testcontainers`, or `MQTTnet`.

#### Scenario: Unit tests run without Docker
- **WHEN** `dotnet run --project Njord.Tests/Njord.Tests.csproj` is executed without a Docker daemon
- **THEN** all tests SHALL pass

#### Scenario: Actor tests use in-process ActorSystem
- **WHEN** actor tests (EgressActorSpec, SchedulerActorSpec, etc.) run
- **THEN** they SHALL use `ActorSystem.Create` or Akka.Hosting test infrastructure without external services

### Requirement: All test projects in the solution
The `Njord.slnx` solution file SHALL include all test projects so `dotnet build Njord.slnx` compiles everything.

#### Scenario: Solution builds all test projects
- **WHEN** `dotnet build Njord.slnx` is executed
- **THEN** `Njord.Tests` and `Njord.Tests.Shared` SHALL all compile successfully
