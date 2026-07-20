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

### Requirement: Actor and stream tests use deterministic assertions
All actor and stream tests SHALL use Akka TestKit's `TestProbe` with
`ExpectMsg<T>` for positive assertions and `ExpectNoMsg` for negative
assertions. Tests MUST NOT use polling-based `AsyncAssert.WaitUntil` or
`AsyncAssert.StaysTrue` for actor message assertions. Tests SHOULD NOT
use custom collector classes when TestProbe provides equivalent functionality.

#### Scenario: Positive assertion uses ExpectMsg
- **WHEN** a test asserts that an actor produced a message
- **THEN** it uses `TestProbe.ExpectMsg<T>()` instead of polling a shared collection

#### Scenario: Negative assertion uses ExpectNoMsg
- **WHEN** a test asserts that an actor did NOT produce a message within a period
- **THEN** it uses `TestProbe.ExpectNoMsg(duration)` instead of `AsyncAssert.StaysTrue`

#### Scenario: Stream events route to TestProbe
- **WHEN** a test needs to assert on messages flowing through an Akka Stream
- **THEN** it routes them to a TestProbe via `Sink.ForEach(m => probe.Tell(m))` and uses `ExpectMsg` for assertions

#### Scenario: Batch draining uses ReceiveWhile
- **WHEN** a test needs to wait for a batch of messages to finish before asserting on subsequent messages
- **THEN** it uses `TestProbe.ReceiveWhile<T>()` to drain the batch, then `ExpectMsg` for new messages

#### Scenario: Tests pass deterministically on CI
- **WHEN** all tests run on a shared GitHub Actions runner
- **THEN** zero tests fail due to timing or thread starvation

### Requirement: All test projects in the solution
The `Njord.slnx` solution file SHALL include all test projects so `dotnet build Njord.slnx` compiles everything.

#### Scenario: Solution builds all test projects
- **WHEN** `dotnet build Njord.slnx` is executed
- **THEN** `Njord.Tests` and `Njord.Tests.Shared` SHALL all compile successfully
