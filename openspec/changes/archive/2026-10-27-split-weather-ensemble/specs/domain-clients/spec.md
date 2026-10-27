## ADDED Requirements

### Requirement: ForecastClient replaces WeatherClient for DomainKind.Weather
The `ForecastClient` SHALL be registered as the `IDomainClient` for `DomainKind.Weather`. Its `HttpClient` base address SHALL be `https://api.open-meteo.com/`. It SHALL use the path `v1/forecast`.

#### Scenario: DI registration
- **WHEN** the service container resolves `IDomainClient` instances
- **THEN** the Weather domain SHALL be served by `ForecastClient`, not `WeatherClient`

### Requirement: EnsembleClient registered for DomainKind.Ensemble
The `EnsembleClient` SHALL be registered as the `IDomainClient` for `DomainKind.Ensemble`. Its `HttpClient` base address SHALL be `https://ensemble-api.open-meteo.com/`. It SHALL use the path `v1/ensemble`.

#### Scenario: DI registration
- **WHEN** the service container resolves `IDomainClient` instances
- **THEN** the Ensemble domain SHALL be served by `EnsembleClient`

## MODIFIED Requirements

### Requirement: One client per domain behind IDomainClient
Every fetched domain SHALL have exactly one `IDomainClient` in `Njord.Core/Domain` exposing
its `DomainKind`, its `CallWeight` and `FetchAsync(latitude, longitude, now,
cancellationToken)` returning a `FetchResult` (`FetchSuccess`, `FetchEmpty` or `FetchError`).
The set of domains SHALL be Weather, Ensemble, AirQuality, Flood, Marine, Seasonal, and Satellite.
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

### Requirement: Weather call weight lives in the client
The ForecastClient SHALL report a `CallWeight` of `ceil(variables / 10) * ceil(forecastDays / 14)` with no model multiplier. The EnsembleClient SHALL report `ceil(variables / 10) * ceil(forecastDays / 14) * modelCount`.

#### Scenario: Forecast weight
- **WHEN** 31 weather API variables are resolved
- **THEN** the ForecastClient's `CallWeight` SHALL be 4

#### Scenario: Ensemble spread-only weight
- **WHEN** 3 spread variables are requested for 1 model with 4 forecast days
- **THEN** the EnsembleClient's `CallWeight` SHALL be 1

#### Scenario: Ensemble full comparison weight
- **WHEN** 31 variables are requested for 3 models with 4 forecast days
- **THEN** the EnsembleClient's `CallWeight` SHALL be 12

## REMOVED Requirements

### Requirement: WeatherClient
**Reason**: Replaced by `ForecastClient` (deterministic forecast) and `EnsembleClient` (spread + comparison models).
**Migration**: All references to `WeatherClient` SHALL be replaced with `ForecastClient`. Ensemble-specific logic moves to `EnsembleClient`.
