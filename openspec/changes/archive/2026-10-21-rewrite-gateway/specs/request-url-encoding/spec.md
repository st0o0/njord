## MODIFIED Requirements

### Requirement: Request URLs encode parameters
Open-Meteo request URLs SHALL be built with `QueryHelpers.AddQueryString`, which percent-encodes every query key and value, and SHALL keep invariant-culture numbers, `timeformat=unixtime` and `timezone=UTC`.

#### Scenario: Reserved characters
- **WHEN** a value contains `&`, `=`, a space or a non-ASCII character
- **THEN** it SHALL appear percent-encoded and SHALL NOT alter other parameters

#### Scenario: Variable lists
- **WHEN** several variables are passed to the hourly list
- **THEN** they SHALL be joined by a comma that is sent unescaped
