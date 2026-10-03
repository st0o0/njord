## ADDED Requirements

### Requirement: Each feature library SHALL have a dedicated test project

Every feature library (`Njord.Mqtt`, `Njord.Enrichment`, `Njord.Ingest`,
`Njord.Sensors`) SHALL have a corresponding `Njord.<Name>.Tests` project
containing all specs for that library.

#### Scenario: Mqtt specs live in Njord.Mqtt.Tests
- **WHEN** listing test files for Mqtt functionality
- **THEN** all Mqtt specs (11 files + 12 Verify snapshots + 1 Presentation subfolder) reside in `src/Njord.Mqtt.Tests/`

#### Scenario: Enrichment specs live in Njord.Enrichment.Tests
- **WHEN** listing test files for Enrichment functionality
- **THEN** all Enrichment specs (10 files including Features subfolder) reside in `src/Njord.Enrichment.Tests/`

#### Scenario: Ingest specs live in Njord.Ingest.Tests
- **WHEN** listing test files for Ingest functionality
- **THEN** `OpenMeteoClientSpec.cs` resides in `src/Njord.Ingest.Tests/`

#### Scenario: Sensors specs live in Njord.Sensors.Tests
- **WHEN** listing test files for Sensors functionality
- **THEN** `SensorHubActorSpec.cs` resides in `src/Njord.Sensors.Tests/`

### Requirement: Host test project SHALL contain only host-specific tests

`Njord.Tests` SHALL contain only tests that exercise host-level concerns:
Configuration, Health, Pipeline integration, Persistence golden masters,
and the Actors helper.

#### Scenario: Njord.Tests has no Mqtt or Enrichment specs
- **WHEN** listing files in `src/Njord.Tests/`
- **THEN** no `Mqtt/` or `Enrichment/` or `Ingest/` or `Sensors/` subdirectories exist

### Requirement: All new test projects SHALL be in the solution

Every new test project SHALL be added to `Njord.slnx` and picked up by the
`Njord.*.Tests` wildcard in architecture test references.

#### Scenario: Solution includes all test projects
- **WHEN** running `dotnet build src/Njord.slnx`
- **THEN** all four new test projects build successfully
