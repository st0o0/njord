# kachelmann-client — Delta Spec

## ADDED Requirements

### Requirement: Advanced forecast fetch per location and model
The client SHALL fetch
`GET {base}/forecast/{lat}/{lon}/advanced/3h?model=<id>` with metric units
(m/s wind) against base URL `https://api.kachelmannwetter.com/v02`,
authenticating via the `X-API-Key` header. One successful call SHALL yield one
`ModelForecast` covering at least +72 h.

#### Scenario: Successful fetch maps to domain
- **WHEN** the API returns a valid advanced forecast payload for `ICON-D2`
- **THEN** the client returns `Success` with a `ModelForecast` whose series
  contains 3-hourly points including the +72 h horizon

### Requirement: Expected failures are typed outcomes, not exceptions
The client SHALL return a typed outcome for every call: `Success(ModelForecast)`
or `Failure` with reason `AuthFailed`, `RateLimited`, `ModelUnavailable`,
`MalformedPayload`, or `Transport`. Expected failure modes MUST NOT surface as
thrown exceptions to the caller.

#### Scenario: Invalid API key
- **WHEN** the API responds 401/403
- **THEN** the client returns `Failure(AuthFailed)`

#### Scenario: Rate limit exceeded
- **WHEN** the API responds 429
- **THEN** the client returns `Failure(RateLimited)`

#### Scenario: Unknown model id
- **WHEN** the API rejects the requested model id
- **THEN** the client returns `Failure(ModelUnavailable)` carrying the model id

#### Scenario: Malformed payload
- **WHEN** the API responds 200 with JSON that does not match the expected
  schema
- **THEN** the client returns `Failure(MalformedPayload)`

### Requirement: No automatic retries
The client SHALL NOT retry failed requests on its own; each call maps to at
most one HTTP request. Retry cadence is owned by the poll pipeline's next
cycle.

#### Scenario: Transport error consumes exactly one request
- **WHEN** the underlying HTTP call fails with a network error
- **THEN** the client returns `Failure(Transport)` after exactly one attempt

### Requirement: The API key never leaks
The client MUST NOT include the API key in log output, exception messages, or
outcome values.

#### Scenario: Failure outcomes are key-free
- **WHEN** any failure outcome is produced
- **THEN** neither the outcome nor associated log entries contain the API key
