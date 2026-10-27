## MODIFIED Requirements

### Requirement: Stream-based concurrent fetch dispatch
The gateway SHALL dispatch fetch requests through an Akka.Streams pipeline: `Source.Queue` (offer) -> `Select` (resolve domain client) -> `BudgetGateStage` (cost-based shaping + accounting) -> `Select` (unknown-domain rejection) -> `SelectAsyncUnordered(maxConcurrent)` (client fetch) -> reply sink + pipeline sink. The actor SHALL own only lifecycle, the queue reference and the stream materialization.

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
The budget gate SHALL be a `BudgetGateStage` custom `GraphStage` in the stream, positioned after client resolution and before the `SelectAsyncUnordered` fetch stage. The stage SHALL delay elements until tokens are available (never reject). Unknown domains (client is null) SHALL bypass the stage's cost accounting and be handled by a subsequent `Select` that produces a `PipelineUnavailable` rejection.

#### Scenario: Budget acquired
- **WHEN** the budget gate has enough tokens for the element's cost
- **THEN** the request SHALL proceed to the fetch stage immediately

#### Scenario: Budget rejected
- **WHEN** the budget gate does not have enough tokens for the element's cost
- **THEN** the element SHALL be parked and delayed until tokens are available, never rejected with `FetchDomainRejected`
