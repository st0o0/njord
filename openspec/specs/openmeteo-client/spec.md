# openmeteo-client Specification

## Purpose

HTTP client for the Open-Meteo forecast API: fetches hourly forecasts per location and model with metric units and unixtime, maps responses to `ModelForecast`, reports expected failures as typed outcomes without retrying, and tolerates values beyond a model's horizon as missing values.

## Requirements

### Requirement: Forecast fetch per location and model
The client SHALL fetch
`GET https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&models={id}`
requesting exactly the hourly variables `temperature_2m`,
`apparent_temperature`, `precipitation`, `wind_speed_10m`, `wind_gusts_10m`,
`dew_point_2m`, `relative_humidity_2m`, `cloud_cover`, `pressure_msl` with
`wind_speed_unit=ms`, `timeformat=unixtime`, and `forecast_days=4`. No
authentication header or API key SHALL be sent. One successful call SHALL
yield one `ModelForecast` with hourly points covering at least +72 h.

#### Scenario: Successful fetch maps to domain
- **WHEN** the API returns a valid single-model payload for `icon_eu`
- **THEN** the client returns `Success` with a `ModelForecast` whose series
  contains hourly points including the +72 h horizon

#### Scenario: Request stays within call weight 1.0
- **WHEN** the client builds a forecast request
- **THEN** it requests exactly 9 hourly variables and 4 forecast days (at or
  below the 10-variable / 2-week weighting thresholds)

### Requirement: Expected failures are typed outcomes, not exceptions
The client SHALL return a typed outcome for every call: `Success(ModelForecast)`
or `Failure` with reason `RateLimited`, `ModelUnavailable`,
`MalformedPayload`, or `Transport`. Expected failure modes MUST NOT surface as
thrown exceptions to the caller.

#### Scenario: Rate limit exceeded
- **WHEN** the API responds 429
- **THEN** the client returns `Failure(RateLimited)`

#### Scenario: Model out of coverage or unknown
- **WHEN** the API responds 400 with `{"error":true,"reason":…}` (unknown
  model id, or the model does not cover the requested location)
- **THEN** the client returns `Failure(ModelUnavailable)` carrying the model
  id and the API reason

#### Scenario: Malformed payload
- **WHEN** the API responds 200 with JSON that does not match the expected
  single-model flat-arrays schema
- **THEN** the client returns `Failure(MalformedPayload)`

### Requirement: Response units are verified
The client SHALL verify that the returned `hourly_units` report the expected
units (`m/s` wind, `°C` temperatures, `unixtime` time) and SHALL return
`Failure(MalformedPayload)` on any mismatch rather than mapping values.

#### Scenario: Unit drift is rejected
- **WHEN** the API responds 200 but `hourly_units.wind_speed_10m` is `km/h`
- **THEN** the client returns `Failure(MalformedPayload)` and no
  `ModelForecast` is produced

### Requirement: Values beyond a model's horizon are missing values
Trailing or interior `null` entries in hourly value arrays SHALL map to
forecast points with the affected parameter absent; they MUST NOT drop the
point, shift the series, or fail the call. Points whose parameter values are
all absent MAY be trimmed from the end of the series.

#### Scenario: Short-horizon model keeps its points
- **WHEN** `icon_d2` returns values up to +48 h and `null` beyond
- **THEN** the client returns `Success` with a series whose points up to
  +48 h carry values and whose all-null tail is absent or value-free

### Requirement: No automatic retries
The client SHALL NOT retry failed requests on its own; each call maps to at
most one HTTP request. Retry cadence is owned by the poll pipeline's next
cycle.

#### Scenario: Transport error consumes exactly one request
- **WHEN** the underlying HTTP call fails with a network error
- **THEN** the client returns `Failure(Transport)` after exactly one attempt
