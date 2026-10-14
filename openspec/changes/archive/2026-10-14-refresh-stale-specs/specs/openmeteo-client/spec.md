## ADDED Requirements

### Requirement: Forecast fetch per location and model in UTC
`OpenMeteoClient.FetchAsync(LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken)` SHALL fetch `GET {OpenMeteoBaseUrl}/v1/forecast?latitude={lat}&longitude={lon}&models={id}`, where `OpenMeteoBaseUrl` comes from `OpenMeteoOptions.OpenMeteoBaseUrl` (bound from the `Njord` section, default `https://api.open-meteo.com/`). The request SHALL include the hourly variables from the active parameter set and, when daily parameters are active, the daily variables. The request SHALL always include `wind_speed_unit=ms`, `timeformat=unixtime`, `timezone=UTC`, and `forecast_days` set to `ModelCoverageRegistry.EffectiveForecastDays(model, ForecastDays)` (the configured days, capped by the model's `MaxForecastHours`). No authentication header or API key SHALL be sent. One successful call SHALL yield one `Forecast(Model, Location, Cycle, Hourly, Daily)` carrying the `CycleId` passed by the caller; it SHALL NOT carry a time zone.

#### Scenario: Successful fetch maps hourly and daily to domain
- **WHEN** the API returns a valid single-model payload for `icon_eu` with the configured hourly and daily variables
- **THEN** the client returns `Success` with a `Forecast` whose hourly series contains points including the +72 h horizon and whose daily series contains the effective number of forecast days

#### Scenario: Request variables match active configuration
- **WHEN** the active hourly set contains 25 variables and the active daily set contains 12 variables
- **THEN** the HTTP request's `hourly` parameter lists exactly those 25 variable names and the `daily` parameter lists exactly those 12 variable names

#### Scenario: API call weight is determined by hourly variable count
- **WHEN** the active hourly set contains 25 variables and 4 forecast days are configured
- **THEN** the gateway computes an API call weight of `ceil(25/10) = 3`

#### Scenario: Custom base URL is used when configured
- **WHEN** `Njord:OpenMeteoBaseUrl` is set to `http://localhost:8080`
- **THEN** the client sends requests to `http://localhost:8080/v1/forecast` instead of the default

#### Scenario: Request uses UTC timestamps
- **WHEN** the client builds the request URI for any location and model
- **THEN** the query string includes `timezone=UTC` and `timeformat=unixtime`

#### Scenario: Forecast days are capped per model
- **WHEN** 16 forecast days are configured and the model's `MaxForecastHours` is 60
- **THEN** the request carries `forecast_days=3`

## MODIFIED Requirements

### Requirement: Expected failures are typed outcomes, not exceptions
The client SHALL return a typed outcome for every call: `PollOutcome.Success(Forecast)` or `PollOutcome.Failure` with reason `RateLimited`, `ModelUnavailable`, `MalformedPayload`, or `Transport`. Expected failure modes (non-success HTTP status, `HttpRequestException`, request timeout, `JsonException`, missing hourly data, unit mismatch) MUST NOT surface as thrown exceptions to the caller. `PollOutcome.Failure` SHALL carry the `Location`, the `Model`, the `Reason` and a `Detail` text; it SHALL NOT carry a `CycleId`. HTTP 429 SHALL map to `RateLimited`, HTTP 400 to `ModelUnavailable` and every other non-success status to `Transport`.

#### Scenario: Rate limit exceeded
- **WHEN** the API responds 429
- **THEN** the client returns `Failure(RateLimited, detail)`

#### Scenario: Model out of coverage or unknown
- **WHEN** the API responds 400 with `{"error":true,"reason":…}` (unknown model id, or the model does not cover the requested location)
- **THEN** the client returns `Failure(ModelUnavailable, detail)` carrying the API reason in the detail string

#### Scenario: Malformed payload
- **WHEN** the API responds 200 with JSON that does not match the expected single-model flat-arrays schema
- **THEN** the client returns `Failure(MalformedPayload, detail)`

## REMOVED Requirements

### Requirement: Forecast fetch per location and model
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Forecast fetch per location and model in UTC" in this capability.

### Requirement: OpenMeteoForecastResponse deserializes timezone
**Reason**: The client no longer resolves a `TimeZoneInfo`; requests use `timezone=UTC` and `Forecast` carries no time zone. The DTO still declares a `Timezone` property, but nothing reads it.
**Migration**: See `utc-timestamps` ("The system SHALL NOT parse the `timezone` field").
