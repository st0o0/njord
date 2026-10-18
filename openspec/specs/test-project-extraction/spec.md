## Purpose

Defines the extraction of feature-library specs into dedicated test projects, leaving only host-specific tests in Njord.Tests, with all test projects included in the solution.
## Requirements
### Requirement: Host test project SHALL contain only host-specific tests
`Njord.Tests` SHALL contain only tests that exercise host-level concerns: the `ConfigManager` actor and the binding of the host's options configuration.

#### Scenario: Njord.Tests has no Mqtt or Enrichment specs
- **WHEN** listing files in `src/Njord.Tests/`
- **THEN** they live under `Configuration/` and no `Mqtt/`, `Pipeline/`, `Grpc/` or analysis subdirectories exist

### Requirement: Each production library SHALL have a dedicated test project
Every production library (`Njord.Weather`, `Njord.Sensors`, `Njord.Persistence`, `Njord.Consensus`, `Njord.Messages`, `Njord.Core`, `Njord.OpenMeteo`, `Njord.Budget`, `Njord.SensorHub`, `Njord.Pipeline`, `Njord.Mqtt`, `Njord.Grpc`, `Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices`) SHALL have a corresponding `Njord.<Name>.Tests` project containing the specs for that library.

#### Scenario: Mqtt specs live in Njord.Mqtt.Tests
- **WHEN** listing test files for MQTT functionality
- **THEN** they reside in `src/Njord.Mqtt.Tests/`

#### Scenario: OpenMeteo specs live in Njord.OpenMeteo.Tests
- **WHEN** listing test files for the Open-Meteo client
- **THEN** `OpenMeteoClientSpec.cs` resides in `src/Njord.OpenMeteo.Tests/`

#### Scenario: SensorHub specs live in Njord.SensorHub.Tests
- **WHEN** listing test files for SensorHub functionality
- **THEN** they reside in `src/Njord.SensorHub.Tests/`

### Requirement: All test projects SHALL be in the solution
Every test project SHALL be added to `Njord.slnx` and picked up by the `Njord.*.Tests` wildcard in the architecture test references.

#### Scenario: Solution includes all test projects
- **WHEN** running `dotnet build src/Njord.slnx`
- **THEN** every `Njord.*Tests` project builds successfully

