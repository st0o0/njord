## ADDED Requirements

### Requirement: No catch-all host test project
The solution SHALL NOT contain a generic `Njord.Tests` project. Every test spec SHALL reside in the test project of the library it primarily exercises.

#### Scenario: Pipeline specs live in Pipeline.Tests
- **WHEN** a developer looks for `PollPipelineSpec`
- **THEN** it SHALL be in `Njord.Pipeline.Tests/`, not a catch-all project

#### Scenario: Persistence specs live in Persistence.Tests
- **WHEN** a developer looks for `ForecastHistoryDtoSerializationSpec`
- **THEN** it SHALL be in `Njord.Persistence.Tests/`

### Requirement: Integration tests use the IntegrationTests project
All specs that boot a real HTTP pipeline (NjordFixture), a full actor system, or test host-level DI wiring SHALL live in `Njord.IntegrationTests`.

#### Scenario: NjordFixture-based specs in IntegrationTests
- **WHEN** a spec uses `NjordFixture` with `[Collection]`
- **THEN** it SHALL be in `Njord.IntegrationTests/`

#### Scenario: Host boot specs in IntegrationTests
- **WHEN** a spec boots a `ServiceProvider` with `NjordServiceSetup` or an `Akka.Hosting.TestKit` with full actor registration
- **THEN** it SHALL be in `Njord.IntegrationTests/`

### Requirement: IntegrationTests project naming matches FunkArr
The integration test project SHALL be named `Njord.IntegrationTests` (no dot before Tests), matching the FunkArr convention `FunkArr.IntegrationTests`.

#### Scenario: Project name in solution
- **WHEN** `Njord.slnx` is inspected
- **THEN** it SHALL list `Njord.IntegrationTests` and SHALL NOT list `Njord.Integration.Tests` or `Njord.Tests`
