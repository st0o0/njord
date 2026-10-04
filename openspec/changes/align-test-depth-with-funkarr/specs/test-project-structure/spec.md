## ADDED Requirements

### Requirement: E2E test project in the solution
The solution SHALL contain a `Njord.E2E.Tests` project that references the host project (`Njord`), Testcontainers, Verify, and MQTTnet. This project SHALL be an executable test project using xUnit v3 on Microsoft.Testing.Platform, consistent with all other test projects.

#### Scenario: E2E project builds with the solution
- **WHEN** `dotnet build Njord.slnx` runs
- **THEN** `Njord.E2E.Tests` SHALL compile without errors

#### Scenario: E2E project excluded from non-Docker CI
- **WHEN** CI runs without Docker available
- **THEN** E2E tests SHALL be skippable via a trait or environment check without failing the build

### Requirement: Integration tests in Njord.Tests use collection-based fixture
Host-level integration tests in `Njord.Tests` SHALL use `ICollectionFixture<NjordFixture>` instead of `IClassFixture<WebApplicationFactory<Program>>` for tests that interact with actor-backed endpoints.

#### Scenario: HealthEndpointSpec uses the fixture
- **WHEN** `HealthEndpointSpec` runs
- **THEN** it SHALL obtain its `HttpClient` from `NjordFixture` rather than `WebApplicationFactory<Program>`

## MODIFIED Requirements

### Requirement: Shared test infrastructure project
The solution SHALL contain a `Njord.Tests.Shared` class library project that holds JSON fixture files, reusable test fakes (`FakeOpenMeteoClient`, `FakeMqttPublisher`), test helpers (`FixtureReader`), and the `NjordFixture` integration test fixture. This project SHALL NOT be a test project and SHALL NOT contain any test classes.

#### Scenario: Shared project provides fixture files
- **WHEN** a test project references `Njord.Tests.Shared`
- **THEN** the JSON fixture files SHALL be available via `FixtureReader`

#### Scenario: Shared project provides FakeOpenMeteoClient
- **WHEN** a test project needs a fake Open-Meteo client for non-container tests
- **THEN** it SHALL use `FakeOpenMeteoClient` from `Njord.Tests.Shared`

#### Scenario: Shared project provides NjordFixture
- **WHEN** an integration test needs the full HTTP/gRPC pipeline with stubbed actors
- **THEN** it SHALL use `NjordFixture` from `Njord.Tests.Shared`
