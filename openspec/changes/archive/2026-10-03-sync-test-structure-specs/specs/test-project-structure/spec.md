## RENAMED Requirements

- FROM: `### Requirement: Unit and actor tests in Njord.Tests`
- TO: `### Requirement: Unit and actor tests in per-library test projects`

## MODIFIED Requirements

### Requirement: Unit and actor tests in per-library test projects
Tests SHALL live in per-library test projects named `Njord.<Name>.Tests` (`Njord.Domain.Tests`, `Njord.Persistence.Tests`, `Njord.Core.Tests`, `Njord.Egress.Tests`, `Njord.Grpc.Tests`, `Njord.Pipeline.Tests`, `Njord.Architecture.Tests`), next to the library they test, plus the host-resident `Njord.Tests` for code that still lives in the `Njord` host (Mqtt, Enrichment, Health, host Configuration, Ingest, Sensors, `PollPipelineSpec`). Each test project is its own executable and SHALL contain only unit tests and actor lifecycle tests that require no Docker containers and no network I/O. No test project SHALL declare a package reference to `Testcontainers`, `WireMock.Net.Testcontainers`, or `MQTTnet`.

#### Scenario: Unit tests run without Docker
- **WHEN** `dotnet run --project <Project>/<Project>.csproj` (for example `Njord.Core.Tests/Njord.Core.Tests.csproj` or `Njord.Tests/Njord.Tests.csproj`) is executed without a Docker daemon
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

### Requirement: All test projects in the solution
The `Njord.slnx` solution file SHALL include all test projects (`Njord.Architecture.Tests`, `Njord.Core.Tests`, `Njord.Domain.Tests`, `Njord.Egress.Tests`, `Njord.Grpc.Tests`, `Njord.Persistence.Tests`, `Njord.Pipeline.Tests`, `Njord.Tests`) and `Njord.Tests.Shared`, so `dotnet build Njord.slnx` compiles everything.

#### Scenario: Solution builds all test projects
- **WHEN** `dotnet build Njord.slnx` is executed
- **THEN** every `Njord.*Tests` project and `Njord.Tests.Shared` SHALL compile successfully
