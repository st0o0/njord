## MODIFIED Requirements

### Requirement: One client per domain behind IDomainClient
Every fetched domain SHALL have exactly one `IDomainClient` in `Njord.Core/Domain` exposing
its `DomainKind`, its `CallWeight` and `FetchAsync(latitude, longitude, now,
cancellationToken)` returning a `FetchResult` (`FetchSuccess`, `FetchEmpty` or `FetchError`).
Clients SHALL obtain their domain's API variable list from `FeatureRequirementsOptions`, convert
domain params to API variable enums, and use the cached `ToApiName` lookup to build the query
string. Clients SHALL use named constants from `ClientRequest` for all query-parameter keys
and fixed values.

#### Scenario: Successful fetch
- **WHEN** the API answers 200 with a valid payload
- **THEN** the client SHALL return `FetchSuccess` carrying the mapped and trimmed domain forecast

#### Scenario: Bad request
- **WHEN** the API answers HTTP 400
- **THEN** the client SHALL return `FetchEmpty`

#### Scenario: Transport, timeout or payload error
- **WHEN** the call throws `HttpRequestException`, times out, or the payload cannot be deserialized
- **THEN** the client SHALL return `FetchError` with the matching `PollFailureReason` and SHALL record the fetch telemetry with the same tags as before

#### Scenario: Caller cancellation
- **WHEN** the caller's cancellation token is cancelled
- **THEN** the client SHALL NOT swallow the cancellation

#### Scenario: Query parameters use named constants
- **WHEN** a client builds a request URI
- **THEN** all query-parameter keys (`forecast_days`, `past_hours`, `tilt`, `azimuth`,
  `wind_speed_unit`, `ensemble`) and their fixed values (`ms`, `true`) SHALL be referenced
  through constants on `ClientRequest`, not string literals

## ADDED Requirements

### Requirement: Weather response uses dictionary-based deserialization
`WeatherResponse` SHALL NOT use a custom `JsonConverter`. The hourly block SHALL be
deserialized as `Dictionary<string, JsonElement>` (or via `[JsonExtensionData]`), following
the Flood precedent. Multi-model key splitting and member-series filtering SHALL be performed
in the mapping layer, not the deserialization layer.

#### Scenario: Single-model response
- **WHEN** the API returns plain hourly keys (e.g. `temperature_2m`)
- **THEN** the mapping SHALL produce one forecast keyed by the requested primary model id

#### Scenario: Multi-model response
- **WHEN** the API returns suffixed hourly keys (e.g. `temperature_2m_ecmwf_ifs025_ensemble_mean`)
- **THEN** the mapping SHALL split each key into variable name and model id using the
  longest-prefix match against `ApiVariableNames.For<WeatherApiVariable>()`, and produce one
  forecast per model

#### Scenario: Member series are dropped
- **WHEN** an hourly key's suffix starts with `member` (e.g. `temperature_2m_member01_<model>`)
- **THEN** the mapping SHALL silently skip it

#### Scenario: Unknown keys are dropped
- **WHEN** an hourly key does not match any known API variable name
- **THEN** the mapping SHALL silently skip it

### Requirement: Shared azimuth conversion
The azimuth offset conversion (Open-Meteo convention: south = 0, config convention: south = 180)
SHALL be a single helper method on `ClientRequest`. Both `WeatherClient` and
`SatelliteRadiationClient` SHALL use it instead of inline arithmetic.

#### Scenario: Azimuth 180 maps to 0
- **WHEN** the configured azimuth is 180 (south-facing)
- **THEN** the API azimuth parameter SHALL be 0

#### Scenario: Both solar clients use the same helper
- **WHEN** `WeatherClient` or `SatelliteRadiationClient` sends a `global_tilted_irradiance` request
- **THEN** both SHALL call the same azimuth conversion method
