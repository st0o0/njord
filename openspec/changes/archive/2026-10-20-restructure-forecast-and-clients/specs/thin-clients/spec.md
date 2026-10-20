## ADDED Requirements

### Requirement: Variables supplied by the caller
Open-Meteo clients SHALL request exactly the variables and forecast days passed by the gateway, with no built-in variable lists.

#### Scenario: Requested variables in URL
- **WHEN** the gateway fetches AirQuality with two variables and 5 forecast days
- **THEN** the request URL SHALL list exactly those two hourly variables and 5 forecast days

### Requirement: Parameter flags map to API names
Each parameter flag SHALL map to one exact API variable name through an explicit mapping, and an undefined value SHALL be rejected.

#### Scenario: Flag to name
- **WHEN** a set of parameter flags is mapped
- **THEN** the result SHALL contain the API name of each flag and no others

#### Scenario: Unknown flag
- **WHEN** a value that is not a defined member is mapped
- **THEN** mapping SHALL fail with an out-of-range error

### Requirement: Gateway derives request from resolution and options
The gateway SHALL take variables from the resolved enrichment requirements and forecast days from the domain's own options.

#### Scenario: Per-domain forecast days
- **WHEN** Weather, AirQuality and Flood are fetched
- **THEN** each request SHALL use its own domain's configured forecast days

### Requirement: Missing response values stay missing
Response mapping SHALL pass absent values through as null.

#### Scenario: Null entry in response array
- **WHEN** a response array contains null at an hour
- **THEN** the mapped point field for that hour SHALL be null
