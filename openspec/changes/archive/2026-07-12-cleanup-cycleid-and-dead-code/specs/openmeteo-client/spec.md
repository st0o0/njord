## MODIFIED Requirements

### Requirement: Forecast fetch per location and model
The client SHALL fetch `GET https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&models={id}` requesting the hourly variables from the active parameter set (resolved from configuration) and, when daily parameters are active, the daily variables. The request SHALL always include `wind_speed_unit=ms`, `timeformat=unixtime`, and `forecast_days=4`. No authentication header or API key SHALL be sent. One successful call SHALL yield one `ModelForecast` with hourly points covering at least +72 h and daily points spanning `forecast_days`. The `FetchAsync` method SHALL NOT accept a `CycleId` parameter — the `CycleId` SHALL be provided via the `WeightedTarget` that carries the fetch context.

#### Scenario: Successful fetch maps hourly and daily to domain
- **WHEN** the API returns a valid single-model payload for `icon_eu` with the configured hourly and daily variables
- **THEN** the client returns `Success` with a `ModelForecast` whose hourly series contains points including the +72 h horizon and whose daily series contains `forecast_days` entries

#### Scenario: Request variables match active configuration
- **WHEN** the active hourly set contains 25 variables and the active daily set contains 12 variables
- **THEN** the HTTP request's `hourly` parameter lists exactly those 25 variable names and the `daily` parameter lists exactly those 12 variable names

#### Scenario: API call weight is determined by hourly variable count
- **WHEN** the active hourly set contains 25 variables
- **THEN** the effective API call weight is `ceil(25/10) = 3`

### Requirement: Expected failures are typed outcomes, not exceptions
The client SHALL return a typed outcome for every call: `Success(ModelForecast)` or `Failure` with reason `RateLimited`, `ModelUnavailable`, `MalformedPayload`, or `Transport`. Expected failure modes MUST NOT surface as thrown exceptions to the caller. `FetchOutcome.Failure` SHALL carry only `Reason` and `Detail` — no `CycleId`, `Location`, or `Model` fields.

#### Scenario: Rate limit exceeded
- **WHEN** the API responds 429
- **THEN** the client returns `Failure(RateLimited, detail)`

#### Scenario: Model out of coverage or unknown
- **WHEN** the API responds 400 with `{"error":true,"reason":…}` (unknown model id, or the model does not cover the requested location)
- **THEN** the client returns `Failure(ModelUnavailable, detail)` carrying the API reason in the detail string

#### Scenario: Malformed payload
- **WHEN** the API responds 200 with JSON that does not match the expected single-model flat-arrays schema
- **THEN** the client returns `Failure(MalformedPayload, detail)`
