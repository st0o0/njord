# param-enums Specification

## Purpose
TBD - created by archiving change restructure-forecast-and-clients. Update Purpose after archive.
## Requirements
### Requirement: Per-domain parameter flags
Weather, AirQuality, Flood and Marine SHALL each have a flags enumeration with one member per requested API variable. Members SHALL be combinable and distinct.

#### Scenario: Combining flags
- **WHEN** two weather parameters are combined
- **THEN** the combination SHALL contain both and no other parameter

#### Scenario: Weather capacity
- **WHEN** all current weather variables are combined
- **THEN** every one SHALL remain distinct (64-bit capacity)

### Requirement: All point fields except ValidFrom are nullable
Weather, AirQuality, Flood and Marine points SHALL represent a missing value as null, never as 0 or false.

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

