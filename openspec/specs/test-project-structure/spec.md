## Purpose

Organization of test projects, their dependency boundaries, shared infrastructure, and which test types belong where.
## Requirements
### Requirement: Shared test infrastructure project
The solution SHALL contain a `Njord.Tests.Shared` class library project that holds JSON fixture files, reusable test fakes (`FakeOpenMeteoClient`, `FailingRefProvider`), Akka configuration helpers (`TestPersistenceConfig`, `TestTimefactorConfig`), timeout constants (`TestTimeouts`), and `TestOptionsMonitor<T>`. This project SHALL NOT be a test project and SHALL NOT contain any test classes. Every file SHALL be used by at least two test projects.

#### Scenario: Shared project provides fixture files
- **WHEN** a test project references `Njord.Tests.Shared`
- **THEN** the JSON fixture files SHALL be available via `FixtureReader`

#### Scenario: Shared project provides FakeOpenMeteoClient
- **WHEN** a test project needs a fake Open-Meteo client for non-container tests
- **THEN** it SHALL use `FakeOpenMeteoClient` from `Njord.Tests.Shared`

### Requirement: Persistence roundtrip tests live alongside their Verify shape tests
`Njord.Persistence.Tests` SHALL contain `PersistenceRoundtripHelper.cs` (a static `AssertRoundtrip<T>(T dto)` helper) and a `*DtoRoundtripSpec.cs` file per persistence DTO, alongside the existing `*DtoSerializationSpec.cs` (Verify shape) files. The helper is NOT in `Njord.Tests.Shared`, because it is used only by `Njord.Persistence.Tests` and the shared-infrastructure rule above requires at least two consumers.

#### Scenario: Roundtrip spec sits next to its shape spec
- **WHEN** `Njord.Persistence.Tests` is inspected
- **THEN** every persistence DTO has both a `*DtoSerializationSpec.cs` (Verify shape) and a `*DtoRoundtripSpec.cs` (field-level roundtrip) file

### Requirement: End-to-end stack verification is a skill, not a dotnet test project
Full-stack, cross-repo (njord + ha-njord + Home Assistant) end-to-end verification SHALL be the `e2e-test` Claude Code skill (`.claude/skills/e2e-test/SKILL.md` + `e2e/E2E-TEST-PLAN.md`), driving a real Docker Compose stack and browser automation — not a `Njord.E2E.Tests` dotnet test project. The solution SHALL NOT contain a `Njord.E2E.Tests` project.

#### Scenario: No E2E dotnet test project exists
- **WHEN** `Njord.slnx` is inspected
- **THEN** it SHALL NOT list a `Njord.E2E.Tests` project

#### Scenario: E2E verification is invoked as a skill
- **WHEN** a contributor wants to verify the full stack end-to-end
- **THEN** they invoke the `e2e-test` skill, which requires Docker and a Chrome browser, not `dotnet run`

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
The `Njord.slnx` solution file SHALL include all test projects (`Njord.Architecture.Tests`, `Njord.Compute.Tests`, `Njord.Core.Tests`, `Njord.Domain.Tests`, `Njord.Egress.Tests`, `Njord.Enrichment.Tests`, `Njord.Grpc.Tests`, `Njord.Ingest.Tests`, `Njord.Mqtt.Tests`, `Njord.Persistence.Tests`, `Njord.Pipeline.Tests`, `Njord.Sensors.Tests`) and `Njord.Tests.Shared`, so `dotnet build Njord.slnx` compiles everything. There is no `Njord.IntegrationTests` or `Njord.E2E.Tests` project — see `persistence-roundtrip-tests` and `e2e-stack-verification` for where that coverage lives instead.

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

