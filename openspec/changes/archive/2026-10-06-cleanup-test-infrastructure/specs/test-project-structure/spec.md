## MODIFIED Requirements

### Requirement: All test projects in the solution
The `Njord.slnx` solution file SHALL include all test projects (`Njord.Architecture.Tests`, `Njord.Core.Tests`, `Njord.Domain.Tests`, `Njord.Egress.Tests`, `Njord.Enrichment.Tests`, `Njord.Grpc.Tests`, `Njord.Ingest.Tests`, `Njord.IntegrationTests`, `Njord.Mqtt.Tests`, `Njord.Persistence.Tests`, `Njord.Pipeline.Tests`, `Njord.Sensors.Tests`) and `Njord.Tests.Shared`, so `dotnet build Njord.slnx` compiles everything.

#### Scenario: Solution builds all test projects
- **WHEN** `dotnet build Njord.slnx` is executed
- **THEN** every `Njord.*Tests` project and `Njord.Tests.Shared` SHALL compile successfully

### Requirement: Unit and actor tests in per-library test projects
Tests SHALL live in per-library test projects named `Njord.<Name>.Tests`, next to the library they test, plus `Njord.IntegrationTests` for tests that boot the real host or require `NjordFixture`. Each per-library test project is its own executable and SHALL contain only unit tests and actor lifecycle tests that require no Docker containers and no network I/O. No per-library test project SHALL declare a package reference to `Testcontainers`, `WireMock.Net.Testcontainers`, or `MQTTnet`.

#### Scenario: Unit tests run without Docker
- **WHEN** `dotnet run --project <Project>/<Project>.csproj` is executed without a Docker daemon
- **THEN** all tests of that project SHALL pass

#### Scenario: Actor tests use Hosting TestKit base class
- **WHEN** a test spec exercises actors or streams
- **THEN** it SHALL inherit from `Akka.Hosting.TestKit.TestKit` and use `ConfigureServices`/`ConfigureAkka` overrides for DI registration and actor setup — never `ActorSystem.Create`

#### Scenario: Persistence actor tests without interception use Hosting TestKit
- **WHEN** a test spec creates or interacts with `ReceivePersistentActor` subclasses but does not need journal/snapshot failure injection
- **THEN** it SHALL inherit from `Akka.Hosting.TestKit.TestKit` with in-memory persistence configured via `AddTestPersistence()` in `ConfigureAkka`

#### Scenario: Persistence actor tests with interception use PersistenceTestKit
- **WHEN** a test spec needs to inject persistence failures
- **THEN** it SHALL inherit from `Akka.Persistence.TestKit.PersistenceTestKit`

#### Scenario: Dependencies wired through DI
- **WHEN** a test spec creates the actor under test
- **THEN** it SHALL register dependencies via `ConfigureServices` instead of manual construction

#### Scenario: Production actor tested directly
- **WHEN** a test spec verifies actor behavior
- **THEN** it SHALL test the production actor class, not a test-specific clone or subclass

#### Scenario: No manual ActorSystem lifecycle management
- **WHEN** a test spec inherits from TestKit base
- **THEN** it SHALL NOT implement `IDisposable` or `IAsyncDisposable` for ActorSystem cleanup

### Requirement: Shared test infrastructure project
The solution SHALL contain a `Njord.Tests.Shared` class library project that holds JSON fixture files, reusable test fakes (`FakeOpenMeteoClient`, `FailingRefProvider`), Akka configuration helpers (`TestPersistenceConfig`, `TestTimefactorConfig`), timeout constants (`TestTimeouts`), and `TestOptionsMonitor<T>`. This project SHALL NOT be a test project and SHALL NOT contain any test classes. Every file SHALL be used by at least two test projects.

#### Scenario: Shared project provides fixture files
- **WHEN** a test project references `Njord.Tests.Shared`
- **THEN** the JSON fixture files SHALL be available via `FixtureReader`

#### Scenario: Shared project provides FakeOpenMeteoClient
- **WHEN** a test project needs a fake Open-Meteo client for non-container tests
- **THEN** it SHALL use `FakeOpenMeteoClient` from `Njord.Tests.Shared`
