## ADDED Requirements

### Requirement: Base parameters for already-required domains
The parameters fetched for a domain SHALL be the union of the enabled features' requirements plus that domain's base
parameters: `weather_code` for Weather and `pm10` for AirQuality. Base parameters SHALL be added only to domains that
an enabled feature already requires, SHALL NOT make a domain required, and SHALL NOT gate rule dispatch. The budget
projection SHALL count the fetched parameters including the base parameters.

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
