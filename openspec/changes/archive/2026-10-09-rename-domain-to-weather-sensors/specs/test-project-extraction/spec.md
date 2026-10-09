## MODIFIED Requirements

### Requirement: Each feature library SHALL have a dedicated test project

Every feature library (`Njord.Mqtt`, `Njord.Enrichment`, `Njord.Ingest`,
`Njord.SensorHub`) SHALL have a corresponding `Njord.<Name>.Tests` project
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

#### Scenario: SensorHub specs live in Njord.SensorHub.Tests
- **WHEN** listing test files for SensorHub functionality
- **THEN** `SensorHubActorSpec.cs` resides in `src/Njord.SensorHub.Tests/`
