## ADDED Requirements

### Requirement: OpenMeteoGateway actor encapsulates all Open-Meteo API interaction
The `OpenMeteoGateway` actor SHALL own an internal Akka.Streams graph that fetches forecasts from the Open-Meteo API. The stream SHALL be: `Source.Queue → BudgetThrottle → SelectAsyncUnordered(2, fetch) → WireTap(scheduler feedback) → DivertTo(failure sink) → BroadcastHub<Forecast>`. The actor SHALL be registered as a singleton under the `IForecastGateway` actor key. The actor SHALL materialize the stream in `PreStart` using `Context.Materializer()`.

#### Scenario: Gateway materializes fetch stream on start
- **WHEN** the OpenMeteoGateway actor starts
- **THEN** it SHALL materialize the internal fetch stream with a `Source.Queue`, budget throttle, `SelectAsyncUnordered(2)` calling `IOpenMeteoClient.FetchAsync`, and a `BroadcastHub<Forecast>` sink

#### Scenario: Stream lifecycle bound to actor
- **WHEN** the OpenMeteoGateway actor stops
- **THEN** the internal stream terminates via the actor-scoped materializer lifecycle

### Requirement: Gateway receives FetchForecast commands and correlates responses
The actor SHALL handle `FetchForecast(string Location, string ModelId)` messages. On receipt, it SHALL store the `Sender` in a pending-request dictionary keyed by `"{Location}|{ModelId}"` and offer a fetch target into the internal `Source.Queue`. When the stream produces a result (via `Sink.ActorRef(Self)` or equivalent), the actor SHALL look up the original requester and send the appropriate feedback message. The actor SHALL compute the API call weight internally from `ResolvedParameterSet` and `NjordOptions.ForecastDays`.

#### Scenario: FetchForecast enqueued and requester tracked
- **WHEN** PollScheduler sends `FetchForecast("borken", "icon_d2")` to the gateway
- **THEN** the gateway SHALL store the sender reference, compute the weight, and offer the target into the internal queue

#### Scenario: Concurrent requests for different models are independent
- **WHEN** two `FetchForecast` messages arrive for `("borken", "icon_d2")` and `("borken", "icon_eu")`
- **THEN** both are enqueued independently and each requester receives its own feedback

### Requirement: Gateway sends scheduler feedback via WireTap
The gateway's internal stream SHALL use `WireTap` to send feedback to the original requester after each fetch. On success, the gateway SHALL compute a content hash via `ForecastDataHash.Compute` and send `ForecastFetched(Location, ModelId, Hash)` to the requester. On failure, it SHALL send `ForecastFetchFailed(Location, ModelId, Reason, Detail)` to the requester. The `WireTap` SHALL be an explicit side-channel that does not affect the main stream flow.

#### Scenario: Successful fetch sends ForecastFetched with hash
- **WHEN** a fetch for `("borken", "icon_d2")` succeeds
- **THEN** the gateway SHALL compute the forecast data hash and send `ForecastFetched("borken", "icon_d2", hash)` to the original requester (PollScheduler)

#### Scenario: Failed fetch sends ForecastFetchFailed
- **WHEN** a fetch for `("borken", "icon_d2")` fails with `PollFailureReason.Transport`
- **THEN** the gateway SHALL send `ForecastFetchFailed("borken", "icon_d2", Transport, detail)` to the original requester

#### Scenario: WireTap does not block the main flow
- **WHEN** the scheduler feedback side-channel is slow
- **THEN** the main stream flow to `BroadcastHub<Forecast>` SHALL NOT be blocked

### Requirement: Gateway splits success and failure via DivertTo
The gateway's internal stream SHALL use `DivertTo` to route `PollOutcome.Failure` elements to a failure-handling sink. The main flow SHALL continue with only `PollOutcome.Success` elements, selecting the `Forecast` from each success. Failures SHALL be routed to the requester via the pending-request dictionary.

#### Scenario: Success passes through to BroadcastHub
- **WHEN** a fetch returns `PollOutcome.Success(forecast)`
- **THEN** the forecast SHALL flow into the `BroadcastHub<Forecast>`

#### Scenario: Failure is diverted away from BroadcastHub
- **WHEN** a fetch returns `PollOutcome.Failure`
- **THEN** it SHALL NOT enter the `BroadcastHub<Forecast>` — it is routed to the failure sink

### Requirement: Gateway exposes forecasts via SourceRef
The actor SHALL handle `RequestForecastStream(Guid RequestId)` messages by materializing a `SourceRef<Forecast>` from its `BroadcastHub<Forecast>` output source and replying with `ForecastStreamReady(RequestId, SourceRef)`. On materialization failure, it SHALL reply with `ForecastStreamFailed(RequestId, Exception)`.

#### Scenario: Pipeline requests and receives SourceRef
- **WHEN** ForecastPipeline sends `RequestForecastStream(requestId)`
- **THEN** the gateway SHALL reply with `ForecastStreamReady(requestId, sourceRef)` containing a valid `SourceRef<Forecast>`

#### Scenario: SourceRef materialization failure
- **WHEN** the SourceRef materialization fails
- **THEN** the gateway SHALL reply with `ForecastStreamFailed(requestId, exception)`

### Requirement: Gateway sends all forecasts unconditionally
The gateway SHALL NOT perform hash-based deduplication. Every successful fetch SHALL flow into the `BroadcastHub<Forecast>` regardless of whether the data has changed. The hash is computed only for scheduler feedback — the pipeline receives all forecasts.

#### Scenario: Unchanged forecast still reaches pipeline
- **WHEN** two consecutive fetches for the same model return identical data
- **THEN** both forecasts SHALL flow through the BroadcastHub to the pipeline

### Requirement: Gateway owns the token bucket internally
The gateway actor SHALL maintain an in-memory token bucket for rate limiting. The token bucket SHALL derive its rate from `IBudgetProvider.GetCurrentRate()` and refresh every 5 seconds. The token bucket SHALL support weighted elements. After each successful acquisition, the gateway SHALL send `RecordApiCall(weight)` to the `BudgetTracker` actor via fire-and-forget `Tell`. No `IBudgetGate<T>` interface or `WeightedBudgetGate` DI service SHALL exist — the throttling is internal to the gateway.

#### Scenario: Token bucket shapes fetch rate
- **WHEN** 10 fetch targets arrive simultaneously with weight 3 each
- **THEN** the token bucket SHALL release them at the rate derived from the current budget (e.g. 480 cost-units/min for free tier)

#### Scenario: Budget tracker receives RecordApiCall
- **WHEN** the token bucket acquires tokens for a fetch with weight 3
- **THEN** the gateway SHALL send `RecordApiCall(3)` to the BudgetTracker actor via Tell

### Requirement: Gateway registration
The gateway SHALL be registered via an `OpenMeteoSetupContainer` implementing `IServiceSetupContainer`. The container SHALL register `IOpenMeteoClient` as a DI service and the `OpenMeteoGateway` actor as a supervised singleton under the `IForecastGateway` key.

#### Scenario: Gateway registered as singleton
- **WHEN** the host starts
- **THEN** the `OpenMeteoGateway` actor SHALL be registered under `IForecastGateway` as a singleton
