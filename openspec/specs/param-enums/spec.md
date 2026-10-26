# param-enums Specification

## Purpose
TBD - created by archiving change restructure-forecast-and-clients. Update Purpose after archive.
## Requirements
### Requirement: Per-domain parameter flags
Weather, AirQuality, Flood, Marine, Seasonal and Satellite SHALL each have a flags
enumeration with one member per requested API variable. Members SHALL be combinable and
distinct. These enums SHALL live in `Njord.Forecast` and SHALL NOT carry any API-specific
attributes.

#### Scenario: Combining flags
- **WHEN** two weather parameters are combined
- **THEN** the combination SHALL contain both and no other parameter

#### Scenario: Weather capacity
- **WHEN** all current weather variables are combined
- **THEN** every one SHALL remain distinct (64-bit capacity)

#### Scenario: No API attributes on domain enums
- **WHEN** any `XxxParams` enum in `Njord.Forecast` is inspected
- **THEN** no member SHALL carry `[JsonStringEnumMemberName]` or `[EnumMember]` attributes

### Requirement: All point fields except ValidFrom are nullable
Weather, AirQuality, Flood and Marine points SHALL represent a missing value as null, never
as 0 or false.

#### Scenario: Variable not delivered
- **WHEN** the API response lacks a variable
- **THEN** the corresponding point field SHALL be null

### Requirement: Points report field presence
Each point SHALL answer whether all requested parameters have a value.

#### Scenario: All requested present
- **WHEN** a point has values for every requested parameter
- **THEN** the presence check SHALL succeed

#### Scenario: One requested missing
- **WHEN** a point has a null value for one requested parameter
- **THEN** the presence check SHALL fail

#### Scenario: Empty request
- **WHEN** no parameters are requested
- **THEN** the presence check SHALL succeed

### Requirement: Param-to-API-string mapping via API variable enums
The mapping from domain param flags members to Open-Meteo API strings SHALL go through the
dedicated API variable enums in `Njord.OpenMeteo`. The six `*ParamsMappingExtensions` switch
files that directly map to strings SHALL be removed and replaced by a two-step mapping:
domain param -> API variable enum (explicit switch) -> API string (cached dictionary lookup).

#### Scenario: ToApiNames through API variable enum
- **WHEN** a set of `WeatherParam` flags is converted to API names
- **THEN** each flag member SHALL be mapped to a `WeatherApiVariable` member and then to its
  API string name

#### Scenario: Old switch-based ToApiName removed
- **WHEN** the codebase is searched for `*ParamsMappingExtensions`
- **THEN** no files with switch-based string-returning `ToApiName` methods SHALL exist
