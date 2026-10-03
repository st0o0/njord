## MODIFIED Requirements

### Requirement: Forecast fetch per location and model
The weather forecast client SHALL be renamed from `IOpenMeteoClient`/`OpenMeteoClient` to `IWeatherClient`/`WeatherClient` and moved to the `Njord.Endpoints.Weather` namespace. It SHALL fetch `GET {OpenMeteoBaseUrl}/v1/forecast?latitude={lat}&longitude={lon}&models={id}` where `OpenMeteoBaseUrl` is read from `NjordOptions.OpenMeteoBaseUrl` (default `https://api.open-meteo.com`). The request SHALL include the hourly variables from the active parameter set and, when daily parameters are active, the daily variables. The request SHALL always include `wind_speed_unit=ms`, `timeformat=unixtime`, and `forecast_days` from configuration. No authentication header or API key SHALL be sent. The `FetchAsync` method signature is unchanged — it remains weather-specific and does not implement a generic endpoint interface.

#### Scenario: Successful fetch maps hourly and daily to domain
- **WHEN** the API returns a valid single-model payload for `icon_eu` with the configured hourly and daily variables
- **THEN** the client returns `Success` with a `ModelForecast` whose hourly series contains points including the +72 h horizon and whose daily series contains `forecast_days` entries

#### Scenario: Request variables match active configuration
- **WHEN** the active hourly set contains 25 variables and the active daily set contains 12 variables
- **THEN** the HTTP request's `hourly` parameter lists exactly those 25 variable names and the `daily` parameter lists exactly those 12 variable names

#### Scenario: Custom base URL is used when configured
- **WHEN** `NjordOptions.OpenMeteoBaseUrl` is set to `http://localhost:8080`
- **THEN** the client SHALL send requests to `http://localhost:8080/v1/forecast` instead of the default

### Requirement: Expected failures are typed outcomes, not exceptions
The client SHALL return a typed outcome for every call: `Success(ModelForecast)` or `Failure` with reason `RateLimited`, `ModelUnavailable`, `MalformedPayload`, or `Transport`. Expected failure modes MUST NOT surface as thrown exceptions to the caller. The outcome type SHALL be `WeatherFetchOutcome` (renamed from `FetchOutcome`) in the `Njord.Endpoints.Weather` namespace. `WeatherFetchOutcome.Failure` SHALL carry only `Reason` and `Detail` — no `CycleId`, `Location`, or `Model` fields.

#### Scenario: Rate limit exceeded
- **WHEN** the API responds 429
- **THEN** the client returns `Failure(RateLimited, detail)`

#### Scenario: Model out of coverage or unknown
- **WHEN** the API responds 400 with `{"error":true,"reason":…}`
- **THEN** the client returns `Failure(ModelUnavailable, detail)` carrying the API reason in the detail string

#### Scenario: Malformed payload
- **WHEN** the API responds 200 with JSON that does not match the expected single-model flat-arrays schema
- **THEN** the client returns `Failure(MalformedPayload, detail)`
