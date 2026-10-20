# thin-clients Specification

## Purpose
TBD - created by archiving change restructure-forecast-and-clients. Update Purpose after archive.
## Requirements
### Requirement: Parameter flags map to API names
Each parameter flag SHALL map to one exact API variable name through an explicit mapping, and an undefined value SHALL be rejected.

#### Scenario: Flag to name
- **WHEN** a set of parameter flags is mapped
- **THEN** the result SHALL contain the API name of each flag and no others

#### Scenario: Unknown flag
- **WHEN** a value that is not a defined member is mapped
- **THEN** mapping SHALL fail with an out-of-range error

### Requirement: Missing response values stay missing
Response mapping SHALL pass absent values through as null.

#### Scenario: Null entry in response array
- **WHEN** a response array contains null at an hour
- **THEN** the mapped point field for that hour SHALL be null

### Requirement: Clients derive request from resolution and options
Each client SHALL take its variables from the resolved feature requirements and its forecast days from its own domain options, clamped to the API maximum.

#### Scenario: Requested variables in URL
- **WHEN** AirQuality is resolved with two variables and 5 forecast days
- **THEN** the request URL SHALL list exactly those two hourly variables and 5 forecast days
