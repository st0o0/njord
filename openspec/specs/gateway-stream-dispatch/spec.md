# gateway-stream-dispatch Specification

## Purpose
The gateway dispatches fetches through an Akka.Streams pipeline with a budget gate stage and bounded concurrency.
## Requirements
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

### Requirement: Pipeline offer as internal stream branch
The gateway SHALL offer successful fetch results to the pipeline through a `WireTap` side-sink (not `AlsoTo`). The reply sink SHALL NOT propagate backpressure to the main pipeline data path. If the reply sink is temporarily slow, copies SHALL be dropped rather than blocking the pipeline delivery.

#### Scenario: Successful fetch offers to pipeline
- **WHEN** a client returns `FetchSuccess`
- **THEN** the gateway SHALL offer the forecast data to the pipeline and reply `FetchDomainCompleted` to the requester

#### Scenario: Pipeline unavailability does not fail the fetch
- **WHEN** the pipeline sink is not yet connected
- **THEN** the gateway SHALL still reply to the requester; the pipeline offer SHALL be dropped with a logged warning

#### Scenario: Reply sink backpressure does not slow the pipeline
- **WHEN** the reply sink is temporarily slower than the main stream
- **THEN** reply copies SHALL be dropped and the pipeline data path SHALL continue unimpeded

### Requirement: Pending request tracking
The gateway SHALL track in-flight requests by a monotonic request id to correlate stream
results with the original requester.

#### Scenario: Reply to correct requester
- **WHEN** two `FetchDomain` messages arrive from different actors
- **THEN** each result SHALL be replied to its original sender, not the other

#### Scenario: Request timeout
- **WHEN** a fetch does not complete within the configured reply timeout
- **THEN** the gateway SHALL reply `FetchDomainFailed` with a timeout reason

