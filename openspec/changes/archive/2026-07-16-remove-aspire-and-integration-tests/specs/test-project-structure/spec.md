## REMOVED Requirements

### Requirement: Container integration tests in Njord.Tests.Integration
**Reason**: Aspire-based integration test project removed entirely.
**Migration**: Tests to be rebuilt in a future change.

### Requirement: E2E tests in Njord.Tests.Integration.E2E
**Reason**: E2E test project removed along with Aspire infrastructure.
**Migration**: Tests to be rebuilt in a future change.

## MODIFIED Requirements

### Requirement: Shared test infrastructure project
The solution SHALL contain a `Njord.Tests.Shared` class library project that holds JSON fixture files, reusable test fakes (`FakeOpenMeteoClient`), and test helpers (`FixtureReader`). This project SHALL NOT be a test project and SHALL NOT contain any test classes.

#### Scenario: Shared project provides fixture files
- **WHEN** a test project references `Njord.Tests.Shared`
- **THEN** the JSON fixture files SHALL be available via `FixtureReader`

#### Scenario: Shared project provides FakeOpenMeteoClient
- **WHEN** a test project needs a fake Open-Meteo client for non-container tests
- **THEN** it SHALL use `FakeOpenMeteoClient` from `Njord.Tests.Shared`
