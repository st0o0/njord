## MODIFIED Requirements

### Requirement: Shared test infrastructure project
The solution SHALL contain a `Njord.Tests.Shared` class library project that holds JSON fixture files, reusable test fakes (`FakeOpenMeteoClient`, `FakeMqttPublisher`), test helpers (`FixtureReader`, `PersistenceRoundtripHelper`, `TestOptionsMonitor<T>`), and test configuration extensions (`TestPersistenceConfig`, `TestTimefactorConfig`). This project SHALL NOT be a test project and SHALL NOT contain any test classes.

#### Scenario: Shared project provides fixture files
- **WHEN** a test project references `Njord.Tests.Shared`
- **THEN** the JSON fixture files SHALL be available via `FixtureReader`

#### Scenario: Shared project provides FakeOpenMeteoClient
- **WHEN** a test project needs a fake Open-Meteo client for non-container tests
- **THEN** it SHALL use `FakeOpenMeteoClient` from `Njord.Tests.Shared`
