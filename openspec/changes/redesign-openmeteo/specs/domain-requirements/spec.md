## MODIFIED Requirements

### Requirement: Handlers declare required parameters
Every handler SHALL declare, through its feature descriptor, exactly the API parameters it
requires per domain, and separately the parameters it can use when present.

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
A requirement SHALL report whether it needs a given domain, true exactly when that domain has
at least one parameter set.

#### Scenario: Domain without parameters
- **WHEN** a requirement contains only Weather parameters
- **THEN** it SHALL report Weather as required and AirQuality, Flood and Marine as not required

### Requirement: Base parameters for already-required domains
The parameters fetched for a domain SHALL be the union of the enabled features' requirements
plus that domain's base parameters: `weather_code` for Weather and `pm10` for AirQuality.
Base parameters SHALL be added only to domains that an enabled feature already requires,
SHALL NOT make a domain required, and SHALL NOT gate rule dispatch. The budget projection
SHALL count the fetched parameters including the base parameters.

#### Scenario: Weather code is fetched
- **WHEN** only the Frost feature is enabled
- **THEN** the weather request SHALL contain `weather_code` next to the Frost parameters

#### Scenario: PM10 is fetched
- **WHEN** only the Pm25 feature is enabled
- **THEN** the air quality request SHALL contain `pm10`

#### Scenario: Base parameters never add a domain
- **WHEN** no enabled feature requires AirQuality
- **THEN** AirQuality SHALL NOT be fetched

#### Scenario: Base parameters never gate a rule
- **WHEN** a weather forecast arrives without `weather_code`
- **THEN** every enabled weather rule whose own required parameters are present SHALL still run

#### Scenario: Budget counts base parameters
- **WHEN** the enabled features require 10 weather variables
- **THEN** the projected weather call weight SHALL be computed from 11 variables

## REMOVED Requirements

### Requirement: ResolvedRequirements wrapper type
**Reason**: The `ResolvedRequirements` wrapper type is replaced by `FetchPlan`, which combines
requirements with domains, models and call weights in a single startup-computed record.
**Migration**: All consumers of `ResolvedRequirements` SHALL depend on `FetchPlan` and extract
per-domain requirements from it.
