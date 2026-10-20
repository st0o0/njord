# request-url-encoding Specification

## Purpose
TBD - created by archiving change typed-field-mappings. Update Purpose after archive.
## Requirements
### Requirement: Request URLs encode parameters
The Open-Meteo request builder SHALL percent-encode every query key and value, join list values with a literal comma, and keep invariant-culture numbers, `timeformat=unixtime` and `timezone=UTC`.

#### Scenario: Plain request unchanged
- **WHEN** a request with coordinates, hourly variables and forecast days is built
- **THEN** the URL SHALL be identical to the one built before this change

#### Scenario: Reserved characters
- **WHEN** a value contains `&`, `=`, a space or a non-ASCII character
- **THEN** it SHALL appear percent-encoded and SHALL NOT alter other parameters

#### Scenario: Variable lists
- **WHEN** several variables are passed to the hourly list
- **THEN** they SHALL be separated by literal commas

