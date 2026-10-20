# domain-requirements Specification

## Purpose
TBD - created by archiving change restructure-forecast-and-clients. Update Purpose after archive.
## Requirements
### Requirement: Handlers declare required parameters
Every handler SHALL declare, through its feature descriptor, exactly the API parameters it requires per domain, and separately the parameters it can use when present.

#### Scenario: Handler requirement per domain
- **WHEN** a handler needs temperature and wind speed from Weather
- **THEN** its feature's required parameters SHALL contain those two weather parameters and no parameters of any other domain

#### Scenario: Optional parameters separate
- **WHEN** a handler lists soil temperature as optional
- **THEN** the parameter appears in the optional set and not in the required set

### Requirement: Requirements can be combined
Requirements SHALL support a union operator that combines the parameters of each domain.

#### Scenario: Union of two requirements
- **WHEN** one requirement asks for Weather temperature and another for Weather wind speed and Flood river discharge
- **THEN** the union SHALL contain both weather parameters and the flood parameter

### Requirement: Requirements report required domains
A requirement SHALL report whether it needs a given domain, true exactly when that domain has at least one parameter set.

#### Scenario: Domain without parameters
- **WHEN** a requirement contains only Weather parameters
- **THEN** it SHALL report Weather as required and AirQuality, Flood and Marine as not required

