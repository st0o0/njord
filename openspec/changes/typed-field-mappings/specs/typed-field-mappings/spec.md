## ADDED Requirements

### Requirement: One field table per domain
Weather, AirQuality, Marine, Flood and Seasonal SHALL each define exactly one field table that lists, per requested variable, its parameter flag, API name, response series accessor and point setter. API names, request lists and point mapping SHALL be derived from that table.

#### Scenario: Request names from flags
- **WHEN** a set of parameter flags is converted to API names
- **THEN** the result SHALL contain the table's API name for each set flag, in flag order, and no other name

#### Scenario: Flag without entry
- **WHEN** a flag has no table entry
- **THEN** the completeness spec SHALL fail

### Requirement: Table completeness is enforced by tests
A spec per domain SHALL fail when a non-None parameter flag has no table entry or more than one, when API names collide, when an entry's API name differs from the serialized name of the response property it reads, or when a point property is written by no entry or by more than one.

#### Scenario: Missing entry
- **WHEN** a new flag is added without a table entry
- **THEN** the completeness spec SHALL fail

#### Scenario: Wrong response property
- **WHEN** an entry reads a response property whose serialized name differs from its API name
- **THEN** the completeness spec SHALL fail

### Requirement: Response mapping is table-driven and behavior-preserving
Weather, AirQuality and Marine responses SHALL be mapped to a domain forecast by one shared loop over the table. Missing or short series SHALL yield null fields; trailing points without any value SHALL be trimmed; a response without any value or without timestamps SHALL yield no forecast.

#### Scenario: Trailing nulls are trimmed
- **WHEN** the last hourly slots carry no value in any series
- **THEN** the forecast SHALL end at the last slot with at least one value

#### Scenario: Nothing valued
- **WHEN** no series has a value at any index
- **THEN** the mapping SHALL return no forecast

#### Scenario: Weather code conversion
- **WHEN** the weather code series holds 61.0
- **THEN** the point's weather code SHALL be the integer 61

#### Scenario: Day flag conversion
- **WHEN** the is-day series holds 1 or 0
- **THEN** the point's day flag SHALL be true for 1 and false for 0, and null when the value is missing

#### Scenario: Missing series
- **WHEN** a requested series is absent from the response
- **THEN** the corresponding point fields SHALL be null and the other fields SHALL still be mapped

### Requirement: Presence check is table-consistent
A point's presence check SHALL succeed exactly when every requested parameter has a non-null value, for every domain, and an empty request SHALL succeed.

#### Scenario: One requested value missing
- **WHEN** one requested parameter's field is null
- **THEN** the presence check SHALL fail

#### Scenario: Every flag covered
- **WHEN** each flag of a domain is requested alone on a point that has only that field set
- **THEN** the presence check SHALL succeed for that flag and fail for every other flag

### Requirement: Flood ensemble members stay outside the table
Flood ensemble-member series SHALL continue to be parsed from the response's extension data and SHALL NOT be described by the field table.

#### Scenario: Members unchanged
- **WHEN** a Flood response with ensemble members is mapped
- **THEN** the forecast SHALL equal the one produced before this change
