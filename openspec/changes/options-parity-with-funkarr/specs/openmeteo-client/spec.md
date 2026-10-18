## MODIFIED Requirements

### Requirement: Forecast fetch per location and model in UTC
`OpenMeteoClient.FetchAsync(LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken)` SHALL fetch `GET {BaseUrl}/v1/forecast?latitude={lat}&longitude={lon}&models={id}`, where `BaseUrl` comes from `OpenMeteoOptions.BaseUrl` (bound from the `Njord:OpenMeteo` section, default `https://api.open-meteo.com/`). The request SHALL include the hourly variables from the active parameter set and, when daily parameters are active, the daily variables. The request SHALL always include `wind_speed_unit=ms`, `timeformat=unixtime`, `timezone=UTC`, and `forecast_days` set to `ModelCoverageRegistry.EffectiveForecastDays(model, ForecastDays)` (the configured days, capped by the model's `MaxForecastHours`). No authentication header or API key SHALL be sent. One successful call SHALL yield one `Forecast(Model, Location, Cycle, Hourly, Daily)` carrying the `CycleId` passed by the caller; it SHALL NOT carry a time zone.

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
- **WHEN** `Njord:OpenMeteo:BaseUrl` is set to `http://localhost:8080`
- **THEN** the client sends requests to `http://localhost:8080/v1/forecast` instead of the default

#### Scenario: Request uses UTC timestamps
- **WHEN** the client builds the request URI for any location and model
- **THEN** the query string includes `timezone=UTC` and `timeformat=unixtime`
