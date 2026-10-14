## ADDED Requirements

### Requirement: Persistence tests keep their helpers local
`Njord.Persistence.Tests` SHALL contain `PersistenceRoundtripHelper.cs` (a static `Roundtrip<T>(T record)` helper using `TypeNameHandling.All`), one roundtrip spec per persistence area (`BudgetTrackerDtoRoundtripSpec`, `DataChangedRoundtripSpec`, `PollSchedulerDtoRoundtripSpec`) and `WireFormatSpec` with inline expected JSON. The helper is NOT in `Njord.Tests.Shared`, because it is used only by `Njord.Persistence.Tests` and the shared-infrastructure rule above requires at least two consumers.

#### Scenario: Roundtrip and wire-format specs sit together
- **WHEN** `Njord.Persistence.Tests` is inspected
- **THEN** it contains the roundtrip specs, `WireFormatSpec` and `PersistenceRoundtripHelper`, and `Njord.Tests.Shared` contains no persistence helper

## MODIFIED Requirements

### Requirement: Shared test infrastructure project
The solution SHALL contain a `Njord.Tests.Shared` class library project that holds JSON fixture files, reusable test fakes (`FakeOpenMeteoClient`, `TestConfigView`, `ConfigHarness`), actor-spec support (`NjordActorSpec`, `FeatureServices`, `DisabledAnalyzers`, `AnalysisFixture`), Akka configuration helpers (`TestPersistenceConfig`, `TestTimefactorConfig`), timeout constants (`TestTimeouts`, `TestTimeConstants`), and `TestOptionsMonitor<T>`. This project SHALL NOT be a test project and SHALL NOT contain any test classes. Every file SHALL be used by at least two test projects.

#### Scenario: Shared project provides fixture files
- **WHEN** a test project references `Njord.Tests.Shared`
- **THEN** the JSON fixture files SHALL be available via `FixtureReader`

#### Scenario: Shared project provides FakeOpenMeteoClient
- **WHEN** a test project needs a fake Open-Meteo client for non-container tests
- **THEN** it SHALL use `FakeOpenMeteoClient` from `Njord.Tests.Shared`

### Requirement: All test projects in the solution
The `Njord.slnx` solution file SHALL include all test projects (`Njord.Weather.Tests`, `Njord.Sensors.Tests`, `Njord.Persistence.Tests`, `Njord.Compute.Tests`, `Njord.Core.Tests`, `Njord.OpenMeteo.Tests`, `Njord.Budget.Tests`, `Njord.SensorHub.Tests`, `Njord.Pipeline.Tests`, `Njord.Mqtt.Tests`, `Njord.Grpc.Tests`, `Njord.Alerts.Tests`, `Njord.Derived.Tests`, `Njord.Trends.Tests`, `Njord.Indices.Tests`, the host project `Njord.Tests` and `Njord.Architecture.Tests`) and `Njord.Tests.Shared`, so `dotnet build Njord.slnx` compiles everything. There is no `Njord.Domain.Tests`, `Njord.IntegrationTests` or `Njord.E2E.Tests` project.

#### Scenario: Solution builds all test projects
- **WHEN** `dotnet build Njord.slnx` is executed
- **THEN** every `Njord.*Tests` project and `Njord.Tests.Shared` SHALL compile successfully

### Requirement: Unit and actor tests in per-library test projects
Tests SHALL live in per-library test projects named `Njord.<Name>.Tests`, next to the library they test, plus the host project `Njord.Tests` for host-level specs (`ConfigManager`, options binding). Each test project is its own executable and SHALL contain only unit tests and actor lifecycle tests that require no Docker containers and no network I/O. No test project SHALL declare a package reference to `Testcontainers`, `WireMock.Net.Testcontainers`, or `MQTTnet`.

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
- **WHEN** a test spec needs to inject persistence failures (e.g., `WithSnapshotLoad(load => load.Fail())`)
- **THEN** it SHALL inherit from `Akka.Persistence.TestKit.PersistenceTestKit`

#### Scenario: Dependencies wired through DI
- **WHEN** a test spec creates the actor under test
- **THEN** it SHALL register dependencies (`IOptions<T>`, `TimeProvider`, `ILogger<T>`) via `ConfigureServices` instead of manual construction with `Options.Create(...)` or `NullLogger.Instance`

#### Scenario: Production actor tested directly
- **WHEN** a test spec verifies actor behavior
- **THEN** it SHALL test the production actor class, not a test-specific clone or subclass

#### Scenario: No manual ActorSystem lifecycle management
- **WHEN** a test spec inherits from `Akka.Hosting.TestKit.TestKit` or `PersistenceTestKit`
- **THEN** it SHALL NOT implement `IDisposable` or `IAsyncDisposable` for ActorSystem cleanup — the base class handles shutdown

## REMOVED Requirements

### Requirement: Persistence roundtrip tests live alongside their Verify shape tests
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Persistence tests keep their helpers local" in this capability.
