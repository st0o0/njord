## MODIFIED Requirements

### Requirement: Base parameters for already-required domains
The parameters fetched for a domain SHALL be the union of the enabled features' requirements
plus that domain's base parameters: `weather_code` for Forecast and `pm10` for AirQuality.
Base parameters SHALL be added only to domains that an enabled feature already requires,
SHALL NOT make a domain required, and SHALL NOT gate rule dispatch. The budget projection
SHALL count the fetched parameters including the base parameters.

The base-params computation SHALL be an instance method on the `DomainRequirements` value type, callable without a service provider or static helper class.

#### Scenario: Weather code is fetched
- **WHEN** only the Frost feature is enabled
- **THEN** the resolved requirements SHALL contain `weather_code` next to the Frost parameters

#### Scenario: PM10 is fetched
- **WHEN** only the Pm25 feature is enabled
- **THEN** the resolved requirements SHALL contain `pm10`

#### Scenario: Base parameters never add a domain
- **WHEN** no enabled feature requires AirQuality
- **THEN** AirQuality SHALL NOT be fetched

#### Scenario: Base parameters never gate a rule
- **WHEN** a weather forecast arrives without `weather_code`
- **THEN** every enabled weather rule whose own required parameters are present SHALL still run

#### Scenario: Budget counts base parameters
- **WHEN** the enabled features require 10 weather variables
- **THEN** the projected weather call weight SHALL be computed from 11 variables

#### Scenario: Testable without DI
- **WHEN** a test constructs a `DomainRequirements` value and calls `WithBaseParams()`
- **THEN** the result SHALL contain the base parameters without needing a service provider

## ADDED Requirements

### Requirement: Requirements report required domains as a list
The `DomainRequirements` value type SHALL provide a method that returns the ordered list of domains that have at least one parameter set.

#### Scenario: Two domains active
- **WHEN** requirements contain Forecast and AirQuality parameters
- **THEN** the required-domains list SHALL contain `[AirQuality, Forecast]` in enum order

#### Scenario: No domains active
- **WHEN** requirements are default (no parameters set)
- **THEN** the required-domains list SHALL be empty

### Requirement: DomainRequirements is a reference type
`DomainRequirements` SHALL be a sealed record class (not a struct) with a parameterless constructor, so that it is directly compatible with `IOptions<T>`. It SHALL retain its positional constructor with 14 defaulted parameters, the `|` operator, `with`-expressions, and all computed properties.

#### Scenario: Parameterless construction
- **WHEN** code constructs `new DomainRequirements()` with no arguments
- **THEN** all parameter fields SHALL be their default values and `Requires()` SHALL return false for every domain

#### Scenario: Positional construction unchanged
- **WHEN** code constructs `new DomainRequirements(Forecast: ForecastParams.Temperature)`
- **THEN** the result SHALL have `Forecast` set and all other fields at their defaults

### Requirement: Requirements registered as IOptions
The resolved `DomainRequirements` (union of enabled features with base params applied) SHALL be registered as `IOptions<DomainRequirements>` using the Configure pattern. Resolution SHALL happen once at startup. All consumers that need the resolved requirements SHALL inject `IOptions<DomainRequirements>`.

#### Scenario: Consumers receive resolved requirements
- **WHEN** two features are enabled requiring different domains
- **THEN** every consumer injecting `IOptions<DomainRequirements>` SHALL receive the union of both features' requirements with base params applied

#### Scenario: ValidateOnStart triggers resolution
- **WHEN** the host builds its service provider
- **THEN** the `DomainRequirements` SHALL be resolved before the host starts accepting requests
