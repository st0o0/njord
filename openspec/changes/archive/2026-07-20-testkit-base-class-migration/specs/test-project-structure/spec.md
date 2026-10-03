## MODIFIED Requirements

### Requirement: Unit and actor tests in Njord.Tests
The `Njord.Tests` project SHALL contain only unit tests and actor lifecycle tests that require no Docker containers and no network I/O. It SHALL NOT depend on `Testcontainers`, `WireMock.Net.Testcontainers`, or `MQTTnet`.

#### Scenario: Unit tests run without Docker
- **WHEN** `dotnet run --project Njord.Tests/Njord.Tests.csproj` is executed without a Docker daemon
- **THEN** all tests SHALL pass

#### Scenario: Actor tests use TestKit base classes
- **WHEN** a test spec exercises actors or streams
- **THEN** it SHALL inherit from `Akka.TestKit.Xunit.TestKit` (non-persistence) or `Akka.Persistence.TestKit.PersistenceTestKit` (persistence-dependent) and use the inherited `Sys` property — never `ActorSystem.Create`

#### Scenario: Persistence-dependent specs use PersistenceTestKit
- **WHEN** a test spec creates or interacts with `ReceivePersistentActor` subclasses
- **THEN** it SHALL inherit from `PersistenceTestKit` to get the in-memory journal and snapshot store

#### Scenario: No manual ActorSystem lifecycle management
- **WHEN** a test spec inherits from `TestKit` or `PersistenceTestKit`
- **THEN** it SHALL NOT implement `IDisposable` or `IAsyncDisposable` for ActorSystem cleanup — the base class handles shutdown
