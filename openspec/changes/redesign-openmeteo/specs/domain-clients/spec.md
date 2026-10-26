## MODIFIED Requirements

### Requirement: One client per domain behind IDomainClient
Every fetched domain SHALL have exactly one `IDomainClient` in `Njord.Core/Domain` exposing
its `DomainKind`, its `CallWeight` and `FetchAsync(latitude, longitude, now,
cancellationToken)` returning a `FetchResult` (`FetchSuccess`, `FetchEmpty` or `FetchError`).
Clients SHALL obtain their domain's API variable list from `FetchPlan`, convert domain params
to API variable enums, and use the cached `ToApiName` lookup to build the query string.

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

### Requirement: Weather call weight lives in the client
The Weather client SHALL report a `CallWeight` of `ceil(variables / 10)`; every other client
SHALL report 1. The variable count SHALL come from the API variable list derived from
`FetchPlan`.

#### Scenario: Weather weight
- **WHEN** 31 weather API variables are resolved from `FetchPlan`
- **THEN** the Weather client's `CallWeight` SHALL be 4

### Requirement: Gateway only orchestrates
`OpenMeteoGateway` SHALL look up the client for the requested domain, dispatch the request
through its stream pipeline (budget gate + concurrent fetch), offer a `FetchSuccess` forecast
to the pipeline and reply; it SHALL contain no per-domain code.

#### Scenario: Unknown domain
- **WHEN** a `FetchDomain` names a domain without a registered client
- **THEN** the gateway SHALL reply `FetchDomainRejected`

#### Scenario: Empty result
- **WHEN** the client returns `FetchEmpty`
- **THEN** the gateway SHALL reply `FetchDomainFailed` with `MalformedPayload`

#### Scenario: Error result
- **WHEN** the client returns `FetchError`
- **THEN** the gateway SHALL reply `FetchDomainFailed` with the error's reason
