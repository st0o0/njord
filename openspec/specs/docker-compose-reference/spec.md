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

