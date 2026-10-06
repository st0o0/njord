## Purpose

Rules for removing dead test infrastructure, redundant wiring tests, and ensuring Tests.Shared hygiene.

## Requirements

### Requirement: No empty leftover project directories
The repository SHALL NOT contain project directories that are not referenced by `Njord.slnx` and contain no source files. Stale directories from previous restructurings SHALL be deleted.

#### Scenario: Empty directories removed
- **WHEN** the cleanup is complete
- **THEN** `src/Njord.Tests/`, `src/Njord.Integration.Tests/`, and `src/Njord.E2E.Tests/` SHALL NOT exist on disk

### Requirement: No stub E2E specs in IntegrationTests
The `Njord.IntegrationTests` project SHALL NOT contain skipped placeholder specs that assert only `Assert.NotEmpty` with `Task.Delay` waits. E2E testing is handled by the separate `e2e/` Docker stack skill.

#### Scenario: Stub specs removed
- **WHEN** `Njord.IntegrationTests` is built
- **THEN** there SHALL be no test classes with `Skip = "Requires Docker"` that use `E2EFixture`

#### Scenario: E2E fixture infrastructure removed
- **WHEN** `Njord.IntegrationTests` is built
- **THEN** `E2EFixture`, `MosquittoFixture`, and `FakeOpenMeteoHandler` SHALL NOT exist in the project

#### Scenario: Container and MQTT packages removed
- **WHEN** `Njord.IntegrationTests.csproj` is inspected
- **THEN** it SHALL NOT reference `Testcontainers`, `MQTTnet`, or `Verify.XunitV3`

### Requirement: Redundant wiring tests removed
Explicit setup/wiring tests whose failures are already covered by the E2E Docker stack SHALL be removed. Tests with unique coverage SHALL be kept.

#### Scenario: ActorKeyRegistrationSpec removed
- **WHEN** `Njord.IntegrationTests` is built
- **THEN** `ActorKeyRegistrationSpec` SHALL NOT exist (E2E boots all real actors)

#### Scenario: NjordServiceSetupSpec removed
- **WHEN** `Njord.IntegrationTests` is built
- **THEN** `NjordServiceSetupSpec` SHALL NOT exist (E2E uses real host DI)

#### Scenario: NjordActorSystemSetupSpec removed
- **WHEN** `Njord.IntegrationTests` is built
- **THEN** `NjordActorSystemSetupSpec` SHALL NOT exist (trivial reflection checks)

#### Scenario: PersistenceBeforeActorsSpec kept
- **WHEN** `Njord.IntegrationTests` is built
- **THEN** `PersistenceBeforeActorsSpec` SHALL exist (fail-fast guard not covered by E2E)

#### Scenario: StreamShutdownTaskSpec kept
- **WHEN** `Njord.IntegrationTests` is built
- **THEN** `StreamShutdownTaskSpec` SHALL exist (unique coordinated shutdown coverage)

### Requirement: Tests.Shared contains only multi-project helpers
Every file in `Njord.Tests.Shared` SHALL be referenced by at least two test projects. Single-consumer helpers SHALL be moved to their sole consumer.

#### Scenario: PersistenceRoundtripHelper moved
- **WHEN** `Njord.Tests.Shared` is inspected
- **THEN** `PersistenceRoundtripHelper` SHALL NOT exist (moved to `Njord.Persistence.Tests`)

#### Scenario: FailingRefProvider consolidated
- **WHEN** `Njord.Mqtt.Tests` is inspected
- **THEN** it SHALL NOT contain its own `FailingRefProvider` class — it SHALL use the shared version from `Njord.Tests.Shared`

#### Scenario: Shared FailingRefProvider handles SubscribeInbound
- **WHEN** the shared `FailingRefProvider` receives a `SubscribeInbound` message
- **THEN** it SHALL handle it silently (no-op) to support MQTT test scenarios
