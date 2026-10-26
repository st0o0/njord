## Purpose

Stream-based gateway dispatch where the pipeline SinkRef is the stream's main sink, replies
reach the requesting PollWorker through the shard region via an AlsoTo branch, the budget gate
runs in a synchronous stage serialized by backpressure, and the stream survives individual
fetch failures via the shared logging decider.

## ADDED Requirements

### Requirement: Pipeline SinkRef as main sink
The gateway's fetch stream SHALL terminate in the pipeline's SinkRef sink. Only successful
fetch results that produce domain data SHALL reach the sink; failures and rejections SHALL be
filtered before the sink. Multi-model weather results SHALL expand into one item per model
plus the primary forecast before entering the sink.

#### Scenario: Successful fetch flows to the pipeline
- **WHEN** a client returns `FetchSuccess` with a domain forecast
- **THEN** the forecast SHALL appear as `DomainDataReceived` in the pipeline's MergeHub input

#### Scenario: Multi-model expansion
- **WHEN** a weather fetch returns a primary forecast and two secondary model forecasts
- **THEN** three `DomainDataReceived` items SHALL enter the pipeline sink

#### Scenario: Failure does not reach the pipeline
- **WHEN** a client returns `FetchError` or `FetchEmpty`
- **THEN** no `DomainDataReceived` SHALL enter the pipeline sink

### Requirement: Reply branch via AlsoTo and Sink.ActorRef
The stream SHALL include an `AlsoTo` branch that maps every fetch outcome to the appropriate
`FetchDomainResponse` and delivers it to the poll-worker shard region via `Sink.ActorRef`. The
shard region SHALL route each reply to the correct PollWorker entity by location.

#### Scenario: Successful fetch reply
- **WHEN** a client returns `FetchSuccess`
- **THEN** the requesting PollWorker SHALL receive `FetchDomainCompleted` with the forecast hash

#### Scenario: Error reply
- **WHEN** a client returns `FetchError`
- **THEN** the requesting PollWorker SHALL receive `FetchDomainFailed` with the error's reason

#### Scenario: Budget rejection reply
- **WHEN** the budget gate refuses a request
- **THEN** the requesting PollWorker SHALL receive `FetchDomainRejected` with reason `Budget`

#### Scenario: Unknown domain reply
- **WHEN** no client is registered for the requested domain
- **THEN** the requesting PollWorker SHALL receive `FetchDomainRejected`

### Requirement: FetchDomainResponse routable via shard region
`FetchDomainResponse` and its subtypes SHALL implement the shard routing interface so that the
`ShardMessageExtractor` can extract the entity ID (location name) and route each reply to the
correct PollWorker entity.

#### Scenario: Reply routed by location
- **WHEN** the gateway sends `FetchDomainCompleted` for location "lucerne" to the shard region
- **THEN** the PollWorker entity for "lucerne" SHALL receive the message

### Requirement: PollWorker uses Tell with reply-timeout timer
PollWorker SHALL send `FetchDomain` via `Tell` (not `Ask`). It SHALL start a per-domain reply
timer when the request is sent and cancel it when a `FetchDomainResponse` for that domain
arrives. If the timer fires before a reply arrives, PollWorker SHALL treat it as a transport
failure.

#### Scenario: Normal reply cancels the timer
- **WHEN** PollWorker tells `FetchDomain` and receives `FetchDomainCompleted` within the timeout
- **THEN** the timer SHALL be cancelled and the reply processed normally

#### Scenario: Timeout triggers transport failure
- **WHEN** PollWorker tells `FetchDomain` and no reply arrives before the timeout
- **THEN** PollWorker SHALL process it as `FetchDomainFailed` with reason `Transport`

### Requirement: Separated gate and fetch stages
The budget gate check SHALL run in a synchronous `Select` stage serialized by stream
backpressure. The HTTP fetch SHALL run in a `SelectAsyncUnordered` stage with bounded
parallelism. The gate SHALL NOT require any thread synchronization.

#### Scenario: Gate serialized by backpressure
- **WHEN** multiple fetch requests are queued
- **THEN** the gate check SHALL process them one at a time without concurrent access

#### Scenario: Fetches run in parallel
- **WHEN** three requests pass the gate
- **THEN** up to three HTTP calls SHALL execute concurrently

### Requirement: Resilient stream via logging decider
The gateway stream SHALL use the shared `StreamSupervision.LoggingDecider` so that transient
failures in a fetch stage resume the stream without killing it. No generation tracking or
stream-lifecycle handlers SHALL exist.

#### Scenario: Transient fetch failure resumes the stream
- **WHEN** a single fetch throws `HttpRequestException`
- **THEN** the element SHALL be dropped, the exception logged, and the stream SHALL continue

### Requirement: Synchronous actor resolution
The gateway SHALL resolve the poll-worker shard region and the pipeline actor synchronously in
the constructor or `PreStart` using the synchronous actor registry lookup. No asynchronous
resolution with PipeTo SHALL be used.

#### Scenario: Actors resolved before first message
- **WHEN** the gateway starts
- **THEN** the shard region and pipeline references SHALL be available before any `FetchDomain` is processed

### Requirement: PostStop cancels without completing the queue
`PostStop` SHALL cancel the `CancellationTokenSource` to abort in-flight HTTP requests. It
SHALL NOT explicitly complete the source queue. The actor-scoped materializer SHALL dispose
with the actor, terminating the stream abruptly without sending the `Sink.ActorRef` completion
message.

#### Scenario: In-flight requests cancelled on stop
- **WHEN** the gateway actor stops while HTTP requests are in flight
- **THEN** those requests SHALL be cancelled via the cancellation token

#### Scenario: No completion message to shard region
- **WHEN** the gateway actor stops
- **THEN** the `Sink.ActorRef` completion message SHALL NOT reach the shard region
