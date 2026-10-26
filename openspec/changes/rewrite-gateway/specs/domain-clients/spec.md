## MODIFIED Requirements

### Requirement: Gateway only orchestrates
`OpenMeteoGateway` SHALL look up the client for the requested domain in the stream's
synchronous gate stage, dispatch the fetch through `SelectAsyncUnordered`, deliver successful
forecasts to the pipeline SinkRef, and route replies to the poll-worker shard region via an
`AlsoTo` branch; it SHALL contain no per-domain code and no result-routing logic in the actor
mailbox.

#### Scenario: Unknown domain
- **WHEN** a `FetchDomain` names a domain without a registered client
- **THEN** the gateway SHALL send `FetchDomainRejected` to the shard region for routing to the requesting worker

#### Scenario: Empty result
- **WHEN** the client returns `FetchEmpty`
- **THEN** the gateway stream SHALL send `FetchDomainFailed` with `MalformedPayload` via the reply branch

#### Scenario: Error result
- **WHEN** the client returns `FetchError`
- **THEN** the gateway stream SHALL send `FetchDomainFailed` with the error's reason via the reply branch

#### Scenario: Reply via shard region
- **WHEN** any fetch completes (success, failure or rejection)
- **THEN** the reply SHALL be sent to the poll-worker shard region, NOT to the original message sender
