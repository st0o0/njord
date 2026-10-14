# poll-pipeline Specification

## Purpose

The `OpenMeteoGateway` fetch stream: `FetchForecast` targets enter a source queue, are throttled to the per-minute budget, fetched with bounded parallelism, reported back to the `PollScheduler`, and successful forecasts fan out via a BroadcastHub to the `ForecastPipeline`.

## Requirements
### Requirement: Fetch requests arrive at the gateway as FetchForecast
`OpenMeteoGateway` (src/Njord.OpenMeteo/OpenMeteoGateway.cs, marker `IForecastGateway`) SHALL accept `FetchForecast(LocationId Location, string ModelId)` from `PollScheduler`. On receipt it SHALL remember the sender under the key `"{location}|{modelId}"`, resolve the location from `NjordOptions`, create an internal `FetchTarget` carrying the location, the model, a `CycleId` stamped with the gateway's `TimeProvider` at receipt, and the pre-computed API weight, and offer it into an internal `Source.Queue<FetchTarget>` (buffer 16, backpressure). There SHALL be no MergeHub, no SinkRef and no tick source; all poll timing is owned by `PollScheduler`. A request for a location that is not configured SHALL be answered immediately with `FetchForecastFailed(..., ModelUnavailable, ...)`.

#### Scenario: Request is queued and its sender remembered
- **WHEN** `PollScheduler` sends `FetchForecast(lucerne, "icon_d2")`
- **THEN** the gateway stores the sender for `lucerne|icon_d2` and offers a `FetchTarget` into its queue

#### Scenario: No raw queue handle crosses actor boundaries
- **WHEN** the gateway materializes its streams
- **THEN** no `ISourceQueueWithComplete` and no SinkRef is exposed to another actor

#### Scenario: CycleId is assigned when the request arrives
- **WHEN** the gateway receives `FetchForecast` at 12:00:00 UTC
- **THEN** the `FetchTarget` carries a `CycleId` with timestamp 12:00:00 UTC

#### Scenario: Unknown location fails fast
- **WHEN** `FetchForecast` names a location that is not configured
- **THEN** the sender receives `FetchForecastFailed` with reason `ModelUnavailable` and nothing is queued

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
Each fetch result SHALL come back to the gateway actor, which SHALL look up the remembered sender for its key and reply: on `PollOutcome.Success` with `FetchForecastCompleted(location, modelId, hash)`, where `hash` is `ForecastDataHash.Compute(forecast, timeProvider)`, and on `PollOutcome.Failure` with `FetchForecastFailed(location, modelId, reason, detail)`. Every successful `Forecast` SHALL then be offered into a second `Source.Queue<Forecast>` (buffer 16, `DropHead`) that feeds a `BroadcastHub<Forecast>` (buffer size 2); failures SHALL NOT reach the hub. The gateway SHALL NOT deduplicate by hash: unchanged forecasts still reach the hub. On `RequestForecastStream(RequestId)` the gateway SHALL reply `RequestForecastStreamCompleted(RequestId, SourceRef<Forecast>)`, or `RequestForecastStreamFailed` when the hub is not materialized or the SourceRef cannot be created. A slow or absent consumer (for example the MQTT broker being down) MUST NOT stall fetching.

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

