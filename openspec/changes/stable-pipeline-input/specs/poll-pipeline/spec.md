## MODIFIED Requirements

### Requirement: Fetch requests arrive at the gateway as FetchForecast
`OpenMeteoGateway` (src/Njord.OpenMeteo/OpenMeteoGateway.cs, marker `IForecastGateway`) SHALL accept `FetchForecast(LocationId Location, string ModelId)` from a requester that asks it. On receipt it SHALL remember the sender for this individual request (two requests for the same location and model are remembered separately), resolve the location from `NjordOptions` case-insensitively, resolve the model against the location's configured models case-insensitively (a model that is not configured keeps the requested spelling), create an internal `FetchTarget` carrying the configured location, the resolved model, a `CycleId` stamped with the gateway's `TimeProvider` at receipt, an admission deadline (receipt time plus the admission timeout, 60 s by default) and the pre-computed API weight, and offer it into a private `Source.Queue<FetchTarget>` (buffer 16, `DropNew`). There SHALL be no tick source; all poll timing is owned by the requester. A request for a location that is not configured SHALL be answered immediately with `FetchForecastFailed(..., ModelUnavailable, ...)`; a request that arrives while no pipeline input is attached SHALL be answered immediately with `FetchForecastRejected(..., PipelineUnavailable)`. Neither is queued nor fetched.

#### Scenario: Request is queued and its sender remembered
- **WHEN** a requester asks the gateway with `FetchForecast(lucerne, "icon_d2")` while the pipeline input is attached
- **THEN** the gateway remembers the sender for this request and offers a `FetchTarget` into its queue

#### Scenario: No raw queue handle crosses actor boundaries
- **WHEN** the gateway materializes its streams
- **THEN** no `ISourceQueueWithComplete` is exposed to another actor; the only stream reference the gateway uses across actors is the SinkRef it receives from the pipeline

#### Scenario: CycleId is assigned when the request arrives
- **WHEN** the gateway receives `FetchForecast` at 12:00:00 UTC
- **THEN** the `FetchTarget` carries a `CycleId` with timestamp 12:00:00 UTC

#### Scenario: Unknown location fails fast
- **WHEN** `FetchForecast` names a location that is not configured
- **THEN** the sender receives `FetchForecastFailed` with reason `ModelUnavailable` and nothing is queued

#### Scenario: Request spelled differently is fetched under the configured names
- **WHEN** `lucerne` and `icon_d2` are configured and the gateway receives `FetchForecast(Lucerne, "ICON_D2")`
- **THEN** the fetch and the forecast sent to the pipeline input use location `lucerne` and model `icon_d2`

#### Scenario: No pipeline input attached
- **WHEN** the gateway has not attached to the pipeline input yet and receives `FetchForecast(lucerne, "icon_d2")`
- **THEN** the sender immediately receives `FetchForecastRejected` with reason `PipelineUnavailable` and Open-Meteo is not called

### Requirement: Gateway fetches respect the per-minute budget
The gateway's private fetch stream SHALL be `Source.Queue<FetchTarget> → GatewayThrottleStage → SelectAsyncUnordered(2, fetch) → Sink.ActorRef(Self)`. `GatewayThrottleStage` SHALL release targets according to a weighted token bucket whose rate comes from `IBudgetProvider.GetCurrentRate()` (80 % of the effective `RequestsPerMinute`) and is refreshed at most every 5 seconds, so budget changes apply without re-materializing the stream. The throttle MAY delay a target to smooth a burst, but never beyond the target's admission deadline: a target that cannot be admitted before its deadline SHALL be answered without being fetched and without being recorded as an API call: `FetchForecastRejected(..., Budget)` when the token wait alone would not fit its admission window, `FetchForecastRejected(..., Expired)` when the window was used up while it queued (including a target that reaches the throttle after its deadline). `SelectAsyncUnordered(2)` SHALL limit concurrent Open-Meteo connections to 2. The fetch stream SHALL use `StreamSupervision.LoggingDecider`.

#### Scenario: Throttle rate derived from budget
- **WHEN** the effective budget is 600 req/min (free tier)
- **THEN** the throttle releases at most 480 weighted cost-units per minute (80 % politeness margin)

#### Scenario: Budget override changes throttle rate at runtime
- **WHEN** the budget override is set to 60 req/min via gRPC `SetBudget`
- **THEN** the throttle adapts to 48 cost-units per minute within 5 seconds, without re-materializing the stream

#### Scenario: Maximum 2 concurrent HTTP calls
- **WHEN** the throttle releases 3 targets within the same second
- **THEN** `SelectAsyncUnordered(2)` runs two fetches concurrently and starts the third only when one completes

#### Scenario: Exhausted budget is rejected, not queued
- **WHEN** the budget allows one request per minute, one request has just been admitted and a second request arrives
- **THEN** the second requester immediately receives `FetchForecastRejected` with reason `Budget`, Open-Meteo is called only once and only one `RecordApiCall` is sent

### Requirement: A fetch that throws reports back as a failure
When `IOpenMeteoClient.FetchAsync` throws instead of returning a `PollOutcome`, the gateway SHALL treat it as a `PollOutcome.Failure` with reason `Transport` and a detail naming the exception type, log a Warning with the exception, and reply `FetchForecastFailed(location, modelId, Transport, detail)` to the remembered requester. The fetch stream SHALL keep running, so later fetches are processed normally, and nothing SHALL be sent to the pipeline input for the failed target.

#### Scenario: Throwing fetch is answered with FetchForecastFailed
- **WHEN** `FetchAsync` for (lucerne, broken) throws an `InvalidOperationException`
- **THEN** the requester receives `FetchForecastFailed` for (lucerne, broken) with reason `Transport` and a detail containing `InvalidOperationException`

#### Scenario: The next fetch still works after a throwing fetch
- **WHEN** a fetch for (lucerne, broken) has thrown and the scheduler then sends `FetchForecast(lucerne, icon_d2)`
- **THEN** the requester receives `FetchForecastCompleted` for (lucerne, icon_d2)

## REMOVED Requirements

### Requirement: Successful forecasts fan out via BroadcastHub and every fetch reports back
**Reason**: The gateway no longer owns a forecast hub or answers `RequestForecastStream`; forecasts flow into the pipeline's input hub through a SinkRef.
**Migration**: See "Successful forecasts flow into the pipeline input and every fetch reports back" and "The gateway attaches to the pipeline input".

### Requirement: A stopped fetch stream is recovered in place
**Reason**: The in-place rebuild with generation tags is replaced by the FunkArr pattern: an ended private stream answers its open requests and restarts the gateway.
**Migration**: See "A stopped fetch stream restarts the gateway".

### Requirement: Every fetch request is answered exactly once
**Reason**: The waiting list of up to 1024 requests is removed; a full queue rejects instead of delaying.
**Migration**: See "Every fetch request ends with exactly one reply".

## ADDED Requirements

### Requirement: Successful forecasts flow into the pipeline input and every fetch reports back
Each fetch result SHALL come back to the gateway actor, which SHALL reply to the sender remembered for that request, with the location and model id spelled as in the request. On `PollOutcome.Success` the gateway SHALL offer the `Forecast` into its forecast branch (a `Source.Queue<Forecast>` of 16 with `DropHead` that runs into the pipeline's SinkRef) and reply `FetchForecastCompleted(location, modelId, hash)`, where `hash` is `ForecastDataHash.Compute(forecast, timeProvider)`, once the forecast is enqueued; when no branch is attached or the offer is not enqueued it SHALL reply `FetchForecastRejected(location, modelId, PipelineUnavailable)` instead. On `PollOutcome.Failure` it SHALL reply `FetchForecastFailed(location, modelId, reason, detail)` and send nothing to the pipeline. The reply SHALL carry no forecast data. The gateway SHALL NOT deduplicate by hash: unchanged forecasts still reach the pipeline. A slow or absent consumer of the pipeline output MUST NOT stall fetching.

#### Scenario: Success reaches the pipeline and the requester
- **WHEN** a fetch for (lucerne, icon_d2) succeeds while the pipeline input is attached
- **THEN** the requester receives `FetchForecastCompleted` with the forecast data hash and the forecast reaches the pipeline input

#### Scenario: Failure reaches only the requester
- **WHEN** a fetch for (lucerne, icon_d2) fails with `Transport`
- **THEN** the requester receives `FetchForecastFailed` with reason `Transport` and nothing is sent to the pipeline input

#### Scenario: Unchanged forecast still reaches the pipeline
- **WHEN** two consecutive fetches return identical data
- **THEN** both forecasts reach the pipeline input

#### Scenario: Reply keeps the requested spelling
- **WHEN** `lucerne` is configured and a fetch requested as `FetchForecast(LUCERNE, "icon_d2")` succeeds
- **THEN** the requester receives `FetchForecastCompleted` for location `LUCERNE` and model `icon_d2`

#### Scenario: Broker outage does not stall fetching
- **WHEN** the MQTT broker is unreachable
- **THEN** the gateway keeps fetching; downstream consumers handle the failure independently

### Requirement: The gateway attaches to the pipeline input
On start the gateway SHALL resolve `IForecastPipeline`, send it `RequestForecastSink(RequestId)` and wait a bounded time for the reply carrying that request id; replies with another request id SHALL be ignored. On `RequestForecastSinkCompleted` it SHALL watch the replying pipeline actor and run its forecast branch into the received SinkRef. When the pipeline terminates, the branch completes or fails, the handshake fails, or no reply arrives in time, the gateway SHALL drop the branch, log a Warning and request a new SinkRef after a jittered exponential backoff (1 s up to 30 s, reset after a successful attach). Re-attaching SHALL NOT touch the private fetch stream, the budget gate or requests in flight. While no branch is attached the gateway SHALL answer `FetchForecast` with `FetchForecastRejected(..., PipelineUnavailable)`.

#### Scenario: Gateway attaches on start
- **WHEN** the gateway starts and the pipeline is registered
- **THEN** it requests a SinkRef from the pipeline and forecasts of later successful fetches reach the pipeline input

#### Scenario: Gateway re-attaches after a pipeline restart
- **WHEN** the attached pipeline terminates and a new pipeline is registered
- **THEN** the gateway requests a new SinkRef after the backoff delay, and a fetch completed afterwards reaches the new pipeline's input

#### Scenario: Stale handshake reply is ignored
- **WHEN** a `RequestForecastSinkCompleted` arrives whose request id is not the gateway's current one
- **THEN** no forecast branch is attached from it

### Requirement: A stopped fetch stream restarts the gateway
If the gateway's private fetch stream completes or fails while the gateway is running, the gateway SHALL log a Warning, answer every request it has not answered yet with `FetchForecastFailed(location, modelId, Transport, detail)`, and then fail, so its backoff-on-failure supervisor restarts it with a fresh fetch stream and a new attach to the pipeline input. A late offer result or fetch result for a request that was already answered SHALL be ignored. The pipeline, its analysis state and its consumers SHALL NOT be affected by a gateway restart.

#### Scenario: Open requests are failed and fetching resumes
- **WHEN** the fetch stream stops while a request for (lucerne, icon_d2) is open
- **THEN** the requester receives `FetchForecastFailed` for (lucerne, icon_d2) with reason `Transport`
- **AND** after the gateway restarted and re-attached, a subsequent `FetchForecast(lucerne, icon_d2)` is answered with `FetchForecastCompleted`

#### Scenario: Requests arriving around a stream stop are each answered once
- **WHEN** six requests arrive while the fetch stream is failing
- **THEN** each of the six requesters receives exactly one reply

#### Scenario: Gateway restart keeps the pipeline state
- **WHEN** the gateway restarts between two fetches of the same location
- **THEN** the pipeline's consumers keep their SourceRefs and the analysis of the second forecast still compares against the consensus of the first (trends receive a previous consensus)

### Requirement: Every fetch request ends with exactly one reply
The gateway SHALL answer every `FetchForecast` exactly once, with `FetchForecastCompleted`, `FetchForecastFailed` or `FetchForecastRejected`, whatever the state of its fetch queue, throttle or pipeline attachment. A request whose offer to the private queue is not enqueued (queue full or closed) SHALL be answered with `FetchForecastRejected(location, modelId, QueueFull)`; an offer that faults SHALL be answered with `FetchForecastFailed(location, modelId, Transport, detail)`. A request answered this way SHALL be forgotten, so the requester's normal retry applies. Two requests for the same location and model that are in flight at the same time SHALL both be fetched and both be answered.

#### Scenario: More requests than the fetch queue holds
- **WHEN** 48 requests for distinct models arrive at once while the fetches are held
- **THEN** the requests beyond the queue's capacity are answered `FetchForecastRejected` with reason `QueueFull`, the others are fetched after release, and every one of the 48 requesters receives exactly one reply

#### Scenario: Concurrent requests for the same target
- **WHEN** two `FetchForecast(lucerne, "icon_d2")` requests are in flight at the same time and both fetches succeed
- **THEN** the requester receives two `FetchForecastCompleted` replies
