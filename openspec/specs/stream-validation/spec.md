# stream-validation Specification

## Purpose
TBD - created by archiving change restructure-forecast-and-clients. Update Purpose after archive.
## Requirements
### Requirement: Handlers run only with complete data
The projection engine SHALL dispatch a handler only when the current forecast contains every field the handler requires.

#### Scenario: Complete data
- **WHEN** a forecast has all fields a handler requires
- **THEN** the handler SHALL run and its events SHALL be emitted

#### Scenario: Missing data
- **WHEN** a required field is null in the forecast
- **THEN** the handler SHALL be skipped, a warning SHALL be logged naming the handler, and no event SHALL be emitted for it

#### Scenario: Other handlers unaffected
- **WHEN** one handler is skipped for missing data
- **THEN** all other handlers SHALL still run for that forecast

### Requirement: Absent domain skips dependent handlers
A handler requiring a domain with no data yet SHALL be skipped without a warning flood.

#### Scenario: Domain not yet arrived
- **WHEN** a handler requires AirQuality and none has arrived
- **THEN** the handler SHALL be skipped

