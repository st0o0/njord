# openmeteo-project-layout Specification

## Purpose
Njord.OpenMeteo is organized into one folder per fetched domain plus shared folders for cross-domain concerns.

## Requirements
### Requirement: Domain-folder project structure
`Njord.OpenMeteo` SHALL organize files into one folder per fetched domain (`Weather/`,
`AirQuality/`, `Flood/`, `Marine/`, `Seasonal/`, `Satellite/`). Each domain folder SHALL
contain the client, API variable enum, response DTOs and response mapping extensions for
that domain.

#### Scenario: Weather folder contents
- **WHEN** the `Weather/` folder is listed
- **THEN** it SHALL contain the weather client, `WeatherApiVariable` enum, weather response
  DTO, weather response mapping extensions and the weather response converter

#### Scenario: No domain files at project root
- **WHEN** the project root is listed
- **THEN** it SHALL contain no client, response DTO, or domain-specific mapping files

### Requirement: Shared concerns in dedicated folders
Cross-domain types SHALL live in shared folders:
- `Gateway/`: gateway actor, stream dispatch, gateway budget gate
- `Budget/`: `IBudgetProvider`, `OptionsBudgetProvider`, `BudgetRate`
- `Http/`: `ClientRequest`, `HttpMetricsHandler`, `OpenMeteoJsonContext`, query constants
- `Configuration/`: setup container, options, validators

#### Scenario: Gateway folder
- **WHEN** the `Gateway/` folder is listed
- **THEN** it SHALL contain the gateway actor and its supporting types

#### Scenario: Budget types not at root
- **WHEN** the project root is listed
- **THEN** it SHALL contain no budget-related files

### Requirement: No functional change from restructuring
The project restructuring SHALL be a mechanical file move with namespace adjustments. No
public API, behavior or test assertion SHALL change as a result of the move alone.

#### Scenario: All tests pass after restructuring
- **WHEN** the project restructuring is applied without other changes
- **THEN** all existing tests SHALL pass without modification beyond namespace updates
