# poll-pipeline Specification

## Purpose

The `OpenMeteoGateway` fetch stream: `FetchForecast` targets enter a source queue, are throttled to the per-minute budget, fetched with bounded parallelism, reported back to the `PollScheduler` exactly once per request (including fetches that throw, requests the fetch queue cannot accept, and requests lost to a stopped fetch stream, which is re-materialized in place), and successful forecasts fan out via a BroadcastHub to the `ForecastPipeline`.
## Requirements
### Requirement: Fetch requests arrive at the gateway as FetchForecast
`OpenMeteoGateway` (src/Njord.OpenMeteo/OpenMeteoGateway.cs, marker `IForecastGateway`) SHALL accept `FetchForecast(LocationId Location, string ModelId)` from `PollScheduler`. On receipt it SHALL remember the sender for this individual request (two requests for the same location and model are remembered separately), resolve the location from `NjordOptions` case-insensitively, resolve the model against the location's configured models case-insensitively (a model that is not configured keeps the requested spelling), create an internal `FetchTarget` carrying the configured location, the resolved model, a `CycleId` stamped with the gateway's `TimeProvider` at receipt, and the pre-computed API weight, and offer it into an internal `Source.Queue<FetchTarget>` (buffer 16, backpressure). There SHALL be no MergeHub, no SinkRef and no tick source; all poll timing is owned by `PollScheduler`. A request for a location that is not configured SHALL be answered immediately with `FetchForecastFailed(..., ModelUnavailable, ...)`.

#### Scenario: Request is queued and its sender remembered
- **WHEN** `PollScheduler` sends `FetchForecast(lucerne, "icon_d2")`
- **THEN** the gateway remembers the sender for this request and offers a `FetchTarget` into its queue

#### Scenario: No raw queue handle crosses actor boundaries
- **WHEN** the gateway materializes its streams
- **THEN** no `ISourceQueueWithComplete` and no SinkRef is exposed to another actor

#### Scenario: CycleId is assigned when the request arrives
- **WHEN** the gateway receives `FetchForecast` at 12:00:00 UTC
- **THEN** the `FetchTarget` carries a `CycleId` with timestamp 12:00:00 UTC

#### Scenario: Unknown location fails fast
- **WHEN** `FetchForecast` names a location that is not configured
- **THEN** the sender receives `FetchForecastFailed` with reason `ModelUnavailable` and nothing is queued

#### Scenario: Request spelled differently is fetched under the configured names
- **WHEN** `lucerne` and `icon_d2` are configured and the gateway receives `FetchForecast(Lucerne, "ICON_D2")`
- **THEN** the fetch and the forecast offered to the hub use location `lucerne` and model `icon_d2`

### Requirement: Gateway fetches respect the per-minute budget
The gateway's fetch stream SHALL be `Source.Queue<FetchTarget> → GatewayThrottleStage → SelectAsyncUnordered(2, IOpenMeteoClient.FetchAsync) → Sink.ActorRef(Self)`. `GatewayThrottleStage` SHALL release targets according to a weighted token bucket whose rate comes from `IBudgetProvider.GetCurrentRate()` (80 % of the effective `RequestsPerMinute`) and is refreshed at most every 5 seconds, so budget changes apply without re-materializing the stream. `SelectAsyncUnordered(2)` SHALL limit concurrent Open-Meteo connections to 2. The fetch stream SHALL use `StreamSupervision.LoggingDecider`.

#### Scenario: Throttle rate derived from budget
- **WHEN** the effective budget is 600 req/min (free tier)
- **THEN** the throttle releases at most 480 weighted cost-units per minute (80 % politeness margin)

#### Scenario: Budget override changes throttle rate at runtime
- **WHEN** the budget override is set to 60 req/min via gRPC `SetBudget`
- **THEN** the throttle adapts to 48 cost-units per minute within 5 seconds, without re-materializing the stream

#### Scenario: Maximum 2 concurrent HTTP calls
- **WHEN** the throttle releases 3 targets within the same second
- **THEN** `SelectAsyncUnordered(2)` runs two fetches concurrently and starts the third only when one completes

### Requirement: Successful forecasts fan out via BroadcastHub and every fetch reports back
Each fetch result SHALL come back to the gateway actor, which SHALL reply to the sender remembered for that request, with the location and model id spelled as in the request: on `PollOutcome.Success` with `FetchForecastCompleted(location, modelId, hash)`, where `hash` is `ForecastDataHash.Compute(forecast, timeProvider)`, and on `PollOutcome.Failure` with `FetchForecastFailed(location, modelId, reason, detail)`. Every successful `Forecast` SHALL then be offered into a second `Source.Queue<Forecast>` (buffer 16, `DropHead`) that feeds a `BroadcastHub<Forecast>` (buffer size 2); failures SHALL NOT reach the hub. The gateway SHALL NOT deduplicate by hash: unchanged forecasts still reach the hub. On `RequestForecastStream(RequestId)` the gateway SHALL reply `RequestForecastStreamCompleted(RequestId, SourceRef<Forecast>)`, or `RequestForecastStreamFailed` when the hub is not materialized or the SourceRef cannot be created. A slow or absent consumer (for example the MQTT broker being down) MUST NOT stall fetching.

#### Scenario: Success reaches the hub and the requester
- **WHEN** a fetch for (lucerne, icon_d2) succeeds
- **THEN** the requester receives `FetchForecastCompleted` with the forecast data hash and the forecast is offered to the `BroadcastHub<Forecast>`

#### Scenario: Failure reaches only the requester
- **WHEN** a fetch for (lucerne, icon_d2) fails with `Transport`
- **THEN** the requester receives `FetchForecastFailed` with reason `Transport` and nothing is offered to the hub

#### Scenario: Unchanged forecast still reaches the pipeline
- **WHEN** two consecutive fetches return identical data
- **THEN** both forecasts are offered to the hub

#### Scenario: Pipeline requests the forecast stream
- **WHEN** `ForecastPipeline` sends `RequestForecastStream(id)`
- **THEN** the gateway replies `RequestForecastStreamCompleted(id, sourceRef)`

#### Scenario: Broker outage does not stall fetching
- **WHEN** the MQTT broker is unreachable
- **THEN** the gateway keeps fetching; downstream consumers handle the failure independently

#### Scenario: Reply keeps the requested spelling
- **WHEN** `lucerne` is configured and a fetch requested as `FetchForecast(LUCERNE, "icon_d2")` succeeds
- **THEN** the requester receives `FetchForecastCompleted` for location `LUCERNE` and model `icon_d2`

### Requirement: A fetch that throws reports back as a failure
When `IOpenMeteoClient.FetchAsync` throws instead of returning a `PollOutcome`, the gateway SHALL treat it as a `PollOutcome.Failure` with reason `Transport` and a detail naming the exception type, log a Warning with the exception, and reply `FetchForecastFailed(location, modelId, Transport, detail)` to the remembered requester. The fetch stream SHALL keep running, so later fetches are processed normally, and nothing SHALL be offered to the forecast hub for the failed target.

#### Scenario: Throwing fetch is answered with FetchForecastFailed
- **WHEN** `FetchAsync` for (lucerne, broken) throws an `InvalidOperationException`
- **THEN** the requester receives `FetchForecastFailed` for (lucerne, broken) with reason `Transport` and a detail containing `InvalidOperationException`

#### Scenario: The next fetch still works after a throwing fetch
- **WHEN** a fetch for (lucerne, broken) has thrown and the scheduler then sends `FetchForecast(lucerne, icon_d2)`
- **THEN** the requester receives `FetchForecastCompleted` for (lucerne, icon_d2)

### Requirement: A stopped fetch stream is recovered in place
If the gateway's fetch stream completes or fails while the gateway is running, the gateway SHALL log a Warning, answer every request that was already handed to the stopped stream and has not been answered yet with `FetchForecastFailed(location, modelId, Transport, detail)`, and re-materialize the fetch stream with the same budget gate. Requests still waiting to be offered SHALL be offered to the new stream. The forecast hub and any SourceRef handed out from it SHALL stay untouched. A completion signal or offer result from a fetch stream that was already replaced SHALL be ignored.

#### Scenario: In-flight requests are failed and fetching resumes
- **WHEN** the fetch stream stops while a request for (lucerne, icon_d2) is in flight
- **THEN** the requester receives `FetchForecastFailed` for (lucerne, icon_d2) with reason `Transport`
- **AND** a subsequent `FetchForecast(lucerne, icon_d2)` is answered with `FetchForecastCompleted`

#### Scenario: Forecast stream survives the recovery
- **WHEN** the fetch stream is re-materialized
- **THEN** a forecast fetched afterwards still reaches a SourceRef obtained before the recovery

#### Scenario: Requests arriving around a stream stop are each answered once
- **WHEN** six requests arrive while the fetch stream is failing
- **THEN** each of the six requesters receives exactly one reply and later requests are fetched normally

### Requirement: Every fetch request is answered exactly once
The gateway SHALL answer every accepted `FetchForecast` exactly once, with `FetchForecastCompleted` or `FetchForecastFailed`, whatever the state of its fetch queue. It SHALL hand requests to the backpressured fetch queue one at a time and keep the requests that do not fit yet in an ordered waiting list of at most 1024 requests, so a full queue delays requests instead of losing them. A request whose offer is not enqueued (queue closed, dropped, failed or faulted) SHALL be answered with `FetchForecastFailed(location, modelId, Transport, detail)` naming the cause; a request arriving while the waiting list is full SHALL be answered immediately with `FetchForecastFailed(location, modelId, Transport, detail)`. A request answered this way SHALL be forgotten, so the requester's normal retry applies. Two requests for the same location and model that are in flight at the same time SHALL both be fetched and both be answered.

#### Scenario: More requests than the fetch queue holds
- **WHEN** 48 requests for distinct models arrive at once while the fetches are held
- **THEN** after the fetches are released every one of the 48 requesters receives exactly one reply

#### Scenario: Concurrent requests for the same target
- **WHEN** two `FetchForecast(lucerne, "icon_d2")` requests are in flight at the same time and both fetches succeed
- **THEN** the requester receives two `FetchForecastCompleted` replies

#### Scenario: Waiting list full
- **WHEN** a request arrives while 1024 requests are waiting to be offered
- **THEN** the requester immediately receives `FetchForecastFailed` with reason `Transport` and a detail stating that the gateway backlog is full

