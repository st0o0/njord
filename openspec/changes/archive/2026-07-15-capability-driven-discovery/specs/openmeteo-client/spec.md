## MODIFIED Requirements

### Requirement: Response deserialization handles dynamic variable sets
The client SHALL deserialize hourly and daily response arrays dynamically based on the active parameter set rather than relying on compile-time typed DTO fields. For each active parameter, the client SHALL extract its array from the JSON response by API name. Missing arrays (parameter not provided by the model) SHALL be treated as all-null rather than failing the call. For daily parameters with `ValueType == TimeString`, when the JSON value is a number (unix timestamp from `timeformat=unixtime`), the client SHALL convert it to an ISO 8601 UTC datetime string (`DateTimeOffset.FromUnixTimeSeconds(n).ToString("O")`). String values SHALL be passed through unchanged.

#### Scenario: Model lacks a requested parameter
- **WHEN** `precipitation_probability` is in the active set but the model response does not include that array
- **THEN** all forecast points carry `null` for that parameter and the call still succeeds

#### Scenario: Extra arrays in the response are ignored
- **WHEN** the API response contains arrays for variables not in the active set
- **THEN** those arrays are not deserialized and do not appear in the domain model

#### Scenario: sunrise/sunset with unixtime format
- **WHEN** `timeformat=unixtime` is set and the daily response contains `sunrise: [1752559380, 1752645900]`
- **THEN** the client SHALL convert these to ISO 8601 strings (e.g. `"2025-07-15T03:23:00+00:00"`) in the daily forecast points

#### Scenario: sunrise/sunset with string format preserved
- **WHEN** a daily TimeString parameter's JSON value is already a string (e.g. `"2025-07-15T05:23"`)
- **THEN** the client SHALL pass the string through unchanged
