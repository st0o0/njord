## Why

The `PollScheduler` re-schedules a target only when the gateway answers its `FetchForecast`, so an unanswered request silences that target for the life of the process — the failure class that `complete-failed-poll-cycles` closed for throwing fetches and stopped streams. Two more paths still lose answers:

- The gateway offers every request into a backpressured `Source.Queue` (buffer 16) without looking at the result. In Akka.Streams 1.5.73 only one offer may be pending on such a queue; every further offer while the buffer is full fails with `IllegalStateException: You have to wait for previous offer to be resolved to send another request`. A probe with 48 requests sent at once showed 21 enqueued and 27 rejected unobserved; even with fetches completing immediately, 5 of the 48 requests never got a reply. This happens whenever more targets are due than the stream holds (startup, a slow API, a throttle that waits for tokens).
- Requests are correlated by the string `"{location}|{model}"`, filed under the requester's spelling and looked up under the configured one. A request whose location differs in case from the configuration is never answered, and two requests for the same target in flight share one slot, so only one of them is answered.

Location names also disagree in case further out: the read model keys its data by the exact location string, so gRPC `GetForecast` for "Lucerne" passes the case-insensitive catalog check against configured "lucerne" and then reports no data; the scheduler keys poll states by the exact reply spelling, so a reply spelled differently creates a second, ghost target that is then polled on its own.

## What Changes

- Every `FetchForecast` is answered exactly once. The gateway correlates each request with its own id instead of a location/model string, offers to the fetch queue one at a time from an in-actor waiting list, observes every offer result, and answers a request that cannot be enqueued (queue closed, offer rejected or failed, waiting list full) with `FetchForecastFailed(Transport, detail)`, so the scheduler's normal backoff retries it.
- Requests that have not been offered yet survive a fetch-stream recovery and are offered to the new stream; requests already offered are failed as today.
- The gateway answers in the location and model spelling of the request and fetches with the configured location and model spelling, so forecasts reach the pipeline under the configured names.
- `PollScheduler` maps every reply to the configured target (case-insensitive location and model); a reply for an unknown target is logged and ignored instead of creating a new target.
- `ForecastReadModel` matches locations case-insensitively for forecast and analysis queries.
- gRPC `GetForecast` queries the read model with the configured location and model spelling found in the catalog, as `GetEnrichments` already does.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `poll-pipeline`: requests are correlated per request, every request gets exactly one reply including requests the fetch queue cannot accept, replies keep the requester's spelling, fetches use the configured spelling, and not-yet-offered requests survive a stream recovery.
- `failure-routing`: the scheduler attributes every reply to the configured target regardless of case and ignores replies for unknown targets.
- `consumer-model`: the read model matches locations case-insensitively.
- `grpc-v2-weather-service`: `GetForecast` matches location and model case-insensitively and queries with the configured spelling.

## Non-goals

- No change to the throttle, the token bucket, the budget gate, `SelectAsyncUnordered(2)` or the retry policy of the scheduler (backoff 1, 2, 4, 8 min, then the discovery interval).
- No new `PollFailureReason` value; the wire contracts, protos and persisted scheduler state stay as they are.
- No deduplication of concurrent requests for the same target: both are fetched and answered as before the regression.
- No case-normalisation of MQTT topics, config writes or sensor input (already case-insensitive or out of scope).

## API budget

No additional Open-Meteo requests. Requests that used to be lost silently are now either fetched (they wait in the gateway instead of being dropped) or answered with a transport failure and retried under the scheduler's backoff, which is the call volume the budget estimate already assumes (one call per target per poll). The ghost targets the scheduler could create from a differently spelled reply would have doubled the calls for that target; removing them can only lower usage. Default free-tier budget stays unchanged: 300k/month, 10k/day; startup validation against 80 % of the monthly budget is untouched.

## Impact

- `src/Njord.OpenMeteo/OpenMeteoGateway.cs` (request correlation, offer handling, waiting list), `src/Njord.Pipeline/PollScheduler.cs` (reply canonicalisation), `src/Njord.Pipeline/ForecastReadModel.cs` (case-insensitive keys), `src/Njord.Grpc/WeatherGrpcService.cs` (`GetForecast`).
- Tests: `Njord.OpenMeteo.Tests`, `Njord.Pipeline.Tests`, `Njord.Grpc.Tests`; `Njord.Tests.Shared/FakeOpenMeteoClient` gains hold/release.
- No proto, message, persistence or configuration changes.
