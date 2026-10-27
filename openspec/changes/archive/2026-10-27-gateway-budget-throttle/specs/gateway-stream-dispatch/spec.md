## MODIFIED Requirements

### Requirement: Stream-based concurrent fetch dispatch
The gateway SHALL dispatch fetch requests through an Akka.Streams pipeline: `Source.Queue` (offer) -> `Throttle` (cost-based shaping) -> budget gate `Select` -> `SelectAsyncUnordered(maxConcurrent)` (client fetch) -> reply sink + pipeline sink. The actor SHALL own only lifecycle, the queue reference and the stream materialization.

#### Scenario: Fetch request enters the stream
- **WHEN** a `FetchDomain` message arrives and the domain has a registered client
- **THEN** the gateway SHALL offer the request to the source queue

#### Scenario: Bounded concurrency
- **WHEN** `maxConcurrent` requests are in-flight
- **THEN** additional offers SHALL be backpressured by the stream, not dropped

#### Scenario: Results arrive at the actor
- **WHEN** a client fetch completes (success, empty or error)
- **THEN** the stream SHALL deliver the reply to the poll shard region via the reply sink

### Requirement: Budget gate as a stream stage
The budget gate check SHALL be a `Select` stage in the stream, positioned after the `Throttle` stage and before the `SelectAsyncUnordered` fetch stage. Because the upstream `Throttle` shapes request rate, the gate serves as a safety net — most requests will have sufficient tokens. A rejected budget acquisition SHALL produce a `FetchDomainRejected` reply without entering the fetch stage.

#### Scenario: Budget acquired
- **WHEN** the budget gate grants the request
- **THEN** the request SHALL proceed to the fetch stage

#### Scenario: Budget rejected
- **WHEN** the budget gate rejects a request that passed the throttle
- **THEN** the gateway SHALL reply `FetchDomainRejected` to the requester and the request SHALL NOT enter the fetch stage
