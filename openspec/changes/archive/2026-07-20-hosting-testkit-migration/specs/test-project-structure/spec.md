## MODIFIED Requirements

### Requirement: Actor tests use TestKit base classes
Actor and stream test specs SHALL inherit from `Akka.Hosting.TestKit.TestKit` as the primary base class, using `ConfigureServices` for DI registration and `ConfigureAkka` for actor system setup. Specs that require persistence interception APIs (`WithJournalWrite`, `WithSnapshotLoad`, etc.) SHALL inherit from `Akka.Persistence.TestKit.PersistenceTestKit` instead. Test specs SHALL use the inherited `Sys` property — never `ActorSystem.Create`.

#### Scenario: Non-persistence actor test uses Hosting TestKit
- **WHEN** a test spec exercises actors or streams that do not require persistence interception
- **THEN** it SHALL inherit from `Akka.Hosting.TestKit.TestKit` and register the actor under test via `ConfigureAkka` with `WithResolvableActors`

#### Scenario: Persistence actor test without interception uses Hosting TestKit
- **WHEN** a test spec exercises a `ReceivePersistentActor` but does not need to inject journal/snapshot failures
- **THEN** it SHALL inherit from `Akka.Hosting.TestKit.TestKit` with in-memory persistence configured via HOCON in `ConfigureAkka`

#### Scenario: Persistence actor test with interception uses PersistenceTestKit
- **WHEN** a test spec needs to inject persistence failures (e.g., `WithSnapshotLoad(load => load.Fail())`)
- **THEN** it SHALL inherit from `Akka.Persistence.TestKit.PersistenceTestKit`

#### Scenario: Dependencies wired through DI
- **WHEN** a test spec creates the actor under test
- **THEN** it SHALL register dependencies (`IOptions<T>`, `TimeProvider`, `ILogger<T>`) via `ConfigureServices` instead of manual construction with `Options.Create(...)` or `NullLogger.Instance`

#### Scenario: Production actor tested directly
- **WHEN** a test spec verifies actor behavior
- **THEN** it SHALL test the production actor class, not a test-specific clone or subclass

### Requirement: Persistence-dependent specs use PersistenceTestKit
Test specs that require persistence interception (journal write failure injection, snapshot load failure injection) SHALL inherit from `PersistenceTestKit`. All other persistence actor specs SHALL use `Akka.Hosting.TestKit.TestKit` with in-memory persistence HOCON configuration.

#### Scenario: Recovery failure spec uses PersistenceTestKit
- **WHEN** a test spec needs to simulate snapshot or journal failures during recovery
- **THEN** it SHALL inherit from `PersistenceTestKit` to access `WithSnapshotLoad` and `WithJournalWrite` interception APIs
