# Capability: docker-compose-reference

## Purpose

Reference docker-compose example file documenting all configurable options for production deployment of njord.
## Requirements
### Requirement: docker-compose.example.yml replaces docker-compose.yml
The repository SHALL contain `docker-compose.example.yml` (renamed from `docker-compose.yml`). The original `docker-compose.yml` SHALL no longer exist.

#### Scenario: File renamed
- **WHEN** the repository root is listed
- **THEN** `docker-compose.example.yml` exists
- **AND** `docker-compose.yml` does not exist

### Requirement: All configurable options documented
The `docker-compose.example.yml` SHALL contain every configurable environment variable of the bound options types — `NjordOptions` (with `ParameterOptions`), `PollingOptions` (including `BudgetOverride`), `OpenMeteoOptions`, `StorageOptions`, `PersistenceOptions`, `MqttOptions`, `SensorOptions`, `ConsensusOptions`, `AlertOptions`, `DerivedOptions`, `TrendOptions` and `IndexOptions` — as environment variable entries, using the section path and property names those types declare (for example `Njord__Polling__PollInterval`). Required values SHALL be uncommented; optional values SHALL be commented with their default values, which SHALL equal the values in `appsettings.json`.

#### Scenario: Required MQTT host is uncommented
- **WHEN** the example compose file is read
- **THEN** `Njord__Mqtt__Host` is present as an uncommented variable with a placeholder value

#### Scenario: Optional values show defaults
- **WHEN** the example compose file is read
- **THEN** optional variables like `Njord__Mqtt__Port`, `Njord__Polling__PollInterval`, `Njord__Enrichment__Consensus__Enabled` are present as commented entries showing their default values

#### Scenario: All enrichment toggles are listed
- **WHEN** the example compose file is read
- **THEN** every analysis feature (Consensus, Alerts, Derived, Trends, Indices) has its `Enabled` toggle listed, and no removed feature (History) appears

### Requirement: Environment variables grouped by section
The environment variables SHALL be organized into named groups using YAML comments: MQTT, Locations, Polling & Models, Parameters, Persistence, Budget, Enrichment.

#### Scenario: Sections are visually distinct
- **WHEN** a user reads the compose file
- **THEN** each group is separated by a comment header (e.g., `# -- MQTT (required) --`)

### Requirement: SQLite volume mapping
The compose file SHALL define a named volume (`njord-data`) mapped to `/app/data` for SQLite journal persistence.

#### Scenario: Volume is declared
- **WHEN** the compose file is read
- **THEN** a `volumes` section maps `njord-data` to `/app/data`

### Requirement: Multiple locations example
The compose file SHALL show at least two location entries -- one uncommented (default home) and one commented (example second location) -- to demonstrate the array indexing pattern.

#### Scenario: Second location is shown
- **WHEN** the compose file is read
- **THEN** a commented `Njord__Locations__1__Name` entry exists demonstrating how to add a second location

