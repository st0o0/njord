# domain-requirements Specification

## Purpose
TBD - created by archiving change restructure-forecast-and-clients. Update Purpose after archive.
## Requirements
### Requirement: Handlers declare required parameters
Every enrichment handler SHALL declare exactly the API parameters it needs, per domain, instead of only naming domains.

#### Scenario: Handler requirement per domain
- **WHEN** a handler needs temperature and wind speed from Weather
- **THEN** its requirements SHALL contain those two weather parameters and no parameters of any other domain

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

