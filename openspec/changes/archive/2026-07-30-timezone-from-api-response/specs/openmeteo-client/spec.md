## MODIFIED Requirements

### Requirement: Forecast fetch per location and model
The client SHALL fetch `GET {OpenMeteoBaseUrl}/v1/forecast?latitude={lat}&longitude={lon}&models={id}` where `OpenMeteoBaseUrl` is read from `NjordOptions.OpenMeteoBaseUrl` (default `https://api.open-meteo.com`). The request SHALL include the hourly variables from the active parameter set and, when daily parameters are active, the daily variables. The request SHALL always include `wind_speed_unit=ms`, `timeformat=unixtime`, `timezone=auto`, and `forecast_days` from configuration. No authentication header or API key SHALL be sent. One successful call SHALL yield one `ModelForecast` with hourly points covering at least +72 h, daily points spanning `forecast_days`, and a `TimeZoneInfo` resolved from the API response's `timezone` field. The `FetchAsync` method SHALL NOT accept a `CycleId` parameter — the `CycleId` SHALL be provided via the `WeightedTarget` that carries the fetch context.

#### Scenario: Successful fetch maps hourly and daily to domain
- **WHEN** the API returns a valid single-model payload for `icon_eu` with the configured hourly and daily variables
- **THEN** the client returns `Success` with a `ModelForecast` whose hourly series contains points including the +72 h horizon and whose daily series contains `forecast_days` entries

#### Scenario: Request variables match active configuration
- **WHEN** the active hourly set contains 25 variables and the active daily set contains 12 variables
- **THEN** the HTTP request's `hourly` parameter lists exactly those 25 variable names and the `daily` parameter lists exactly those 12 variable names

#### Scenario: API call weight is determined by hourly variable count
- **WHEN** the active hourly set contains 25 variables
- **THEN** the effective API call weight is `ceil(25/10) = 3`

#### Scenario: Custom base URL is used when configured
- **WHEN** `NjordOptions.OpenMeteoBaseUrl` is set to `http://localhost:8080`
- **THEN** the client SHALL send requests to `http://localhost:8080/v1/forecast` instead of the default

#### Scenario: Request includes timezone=auto
- **WHEN** the client builds the request URI for any location and model
- **THEN** the query string SHALL include `timezone=auto`

#### Scenario: Timezone from response is carried on ModelForecast
- **WHEN** the API response contains `"timezone": "Europe/Zurich"`
- **THEN** the returned `ModelForecast.TimeZone` SHALL be `TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich")`

#### Scenario: Unrecognized timezone in response is a malformed payload
- **WHEN** the API response contains `"timezone": "Invalid/Zone"`
- **THEN** the client SHALL return `Failure(MalformedPayload, detail)` describing the unrecognized timezone

## ADDED Requirements

### Requirement: OpenMeteoForecastResponse deserializes timezone
The `OpenMeteoForecastResponse` DTO SHALL include a `Timezone` property mapped from the JSON `timezone` field. The client SHALL use this value to resolve a `TimeZoneInfo` for the `ModelForecast`.

#### Scenario: Timezone field present in response
- **WHEN** the API response JSON contains `"timezone": "America/New_York"`
- **THEN** the DTO's `Timezone` property SHALL be `"America/New_York"`

#### Scenario: Timezone field missing from response
- **WHEN** the API response JSON does not contain a `timezone` field
- **THEN** the DTO's `Timezone` property SHALL be null, and the client SHALL fall back to `TimeZoneInfo.Utc` for the `ModelForecast`
