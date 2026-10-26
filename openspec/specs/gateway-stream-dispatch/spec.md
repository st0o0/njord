# gateway-stream-dispatch Specification

## Purpose
The gateway dispatches fetches through an Akka.Streams pipeline with a budget gate stage and bounded concurrency.

## Requirements
### Requirement: Stream-based concurrent fetch dispatch
The gateway SHALL dispatch fetch requests through an Akka.Streams pipeline:
`Source.Queue` (offer) -> budget gate stage -> `SelectAsyncUnordered(maxConcurrent)` (client
fetch) -> `Sink.ActorRef(Self)`. The actor SHALL own only lifecycle, the queue reference and
pending request tracking.

#### Scenario: Fetch request enters the stream
- **WHEN** a `FetchDomain` message arrives and the domain has a registered client
- **THEN** the gateway SHALL offer the request to the source queue

#### Scenario: Bounded concurrency
- **WHEN** `maxConcurrent` requests are in-flight
- **THEN** additional offers SHALL be backpressured by the stream, not dropped

#### Scenario: Results arrive at the actor
- **WHEN** a client fetch completes (success, empty or error)
- **THEN** the stream SHALL deliver the result to the actor via `Sink.ActorRef`

### Requirement: Budget gate as a stream stage
The budget gate check SHALL be a `MapAsync` stage in the stream, positioned before the
`SelectAsyncUnordered` fetch stage. A rejected budget acquisition SHALL produce a
`FetchDomainRejected` reply without entering the fetch stage.

#### Scenario: Budget acquired
- **WHEN** the budget gate grants the request
- **THEN** the request SHALL proceed to the fetch stage

#### Scenario: Budget rejected
- **WHEN** the budget gate rejects the request
- **THEN** the gateway SHALL reply `FetchDomainRejected` to the requester and the request
  SHALL NOT enter the fetch stage

### Requirement: Pipeline offer as internal stream branch
The gateway SHALL offer successful fetch results to the pipeline through an internal stream
branch (not a separate actor message). The pipeline delivery SHALL NOT block or slow the
reply to the requester.

#### Scenario: Successful fetch offers to pipeline
- **WHEN** a client returns `FetchSuccess`
- **THEN** the gateway SHALL offer the forecast data to the pipeline and reply
  `FetchDomainCompleted` to the requester

#### Scenario: Pipeline unavailability does not fail the fetch
- **WHEN** the pipeline sink is not yet connected
- **THEN** the gateway SHALL still reply to the requester; the pipeline offer SHALL be
  dropped with a logged warning

### Requirement: Pending request tracking
The gateway SHALL track in-flight requests by a monotonic request id to correlate stream
results with the original requester.

#### Scenario: Reply to correct requester
- **WHEN** two `FetchDomain` messages arrive from different actors
- **THEN** each result SHALL be replied to its original sender, not the other

#### Scenario: Request timeout
- **WHEN** a fetch does not complete within the configured reply timeout
- **THEN** the gateway SHALL reply `FetchDomainFailed` with a timeout reason
