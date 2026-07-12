## MODIFIED Requirements

### Requirement: Forecast fetch per location and model
The client SHALL fetch `GET https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&models={id}` requesting the hourly variables from the active parameter set (resolved from configuration) and, when daily parameters are active, the daily variables. The request SHALL always include `wind_speed_unit=ms`, `timeformat=unixtime`, and `forecast_days=4`. No authentication header or API key SHALL be sent. One successful call SHALL yield one `ModelForecast` with hourly points covering at least +72 h and daily points spanning `forecast_days`.

#### Scenario: Successful fetch maps hourly and daily to domain
- **WHEN** the API returns a valid single-model payload for `icon_eu` with the configured hourly and daily variables
- **THEN** the client returns `Success` with a `ModelForecast` whose hourly series contains points including the +72 h horizon and whose daily series contains `forecast_days` entries

#### Scenario: Request variables match active configuration
- **WHEN** the active hourly set contains 25 variables and the active daily set contains 12 variables
- **THEN** the HTTP request's `hourly` parameter lists exactly those 25 variable names and the `daily` parameter lists exactly those 12 variable names

#### Scenario: API call weight is determined by hourly variable count
- **WHEN** the active hourly set contains 25 variables
- **THEN** the effective API call weight is `ceil(25/10) = 3`

### Requirement: Response units are verified
The client SHALL verify that the returned `hourly_units` report the expected units for every active parameter that has a verifiable unit expectation. Temperature parameters SHALL be `°C`, wind parameters SHALL be `m/s`, pressure parameters SHALL be `hPa`, time SHALL be `unixtime`. The verification set SHALL be derived from the parameter registry's unit metadata. A mismatch SHALL return `Failure(MalformedPayload)`.

#### Scenario: Unit verification covers all active wind parameters
- **WHEN** `wind_speed_80m` and `wind_direction_10m` are in the active set and the response returns them with units `m/s` and `°`
- **THEN** the client accepts both (direction uses degrees, not m/s — only speed is verified)

#### Scenario: Dynamic unit check catches drift
- **WHEN** `surface_pressure` is in the active set and the response reports `surface_pressure` unit as `Pa` instead of `hPa`
- **THEN** the client returns `Failure(MalformedPayload)`

## ADDED Requirements

### Requirement: Response deserialization handles dynamic variable sets
The client SHALL deserialize hourly and daily response arrays dynamically based on the active parameter set rather than relying on compile-time typed DTO fields. For each active parameter, the client SHALL extract its array from the JSON response by API name. Missing arrays (parameter not provided by the model) SHALL be treated as all-null rather than failing the call.

#### Scenario: Model lacks a requested parameter
- **WHEN** `precipitation_probability` is in the active set but the model response does not include that array
- **THEN** all forecast points carry `null` for that parameter and the call still succeeds

#### Scenario: Extra arrays in the response are ignored
- **WHEN** the API response contains arrays for variables not in the active set
- **THEN** those arrays are not deserialized and do not appear in the domain model
