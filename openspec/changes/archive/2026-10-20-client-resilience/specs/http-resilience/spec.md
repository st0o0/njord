## ADDED Requirements

### Requirement: HTTP clients use standard resilience pipeline
All OpenMeteo HTTP clients SHALL have retry, circuit breaker, and timeout configured via `AddStandardResilienceHandler()` at the HttpClient registration level. Client code SHALL NOT implement its own retry or error-swallowing logic.

#### Scenario: Transient 503 is retried automatically
- **WHEN** the Open-Meteo API returns HTTP 503 on the first attempt
- **THEN** the resilience pipeline SHALL retry up to 3 times with exponential backoff

#### Scenario: Persistent failure propagates as exception
- **WHEN** all retry attempts fail
- **THEN** an HttpRequestException SHALL propagate to the caller (Gateway)

### Requirement: Clients are self-contained with no shared fetch method
Each domain client (Weather, AirQuality, Flood, Marine, Geocoding) SHALL build its own URL, call `GetFromJsonAsync<T>`, and return its typed response. No shared static `FetchCoreAsync` or base class.

#### Scenario: Client catches only 400 Bad Request
- **WHEN** the API returns HTTP 400 (invalid coordinates)
- **THEN** the client SHALL return null without retry

#### Scenario: Client code has no try/catch for transient errors
- **WHEN** inspecting the client code
- **THEN** there SHALL be no catch for HttpRequestException with transient status codes
