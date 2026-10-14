## Why

The failed-fetch path loses information in two places. `PollScheduler` counts only `FetchForecastCompleted` replies toward a location's poll cycle, so a cycle that contains a failed fetch never completes on its own: no summary log and no `njord_poll_cycle_*` metrics are emitted for it, and the open counter then borrows replies from the next round, so the next "completed" cycle mixes two rounds (wrong duration, a model counted twice, a failed model reported as present). Separately, if `IOpenMeteoClient.FetchAsync` throws instead of returning a `PollOutcome.Failure`, the gateway's fetch stream can stop (an exception from an already-faulted task reaches the supervision decider as an `AggregateException`, which it classifies as `Stop`; an `InvalidOperationException` from parsing a non-numeric JSON value is `Stop` even unwrapped). The gateway then only logs "Gateway fetch stream completed unexpectedly", never answers the in-flight requests and silently drops every later `FetchForecast`, so polling goes quiet until the process restarts.

## What Changes

- `PollScheduler` tracks a poll cycle per location as one outcome per resolved model (changed, unchanged, failed). A `FetchForecastFailed` is a final outcome for the current cycle; the cycle completes once every resolved model has an outcome. A retry that lands before completion replaces that model's outcome. Completed trackers are removed; reports for unknown locations or models open no tracker.
- The poll summary log reports changed, failed and total models; `njord_poll_cycle_models` keeps counting every model of the completed cycle, and a new gauge `njord_poll_cycle_failed_models{location}` records how many of them failed.
- `StreamSupervision.Classify` unwraps an `AggregateException` with a single inner exception (recursively) before classifying it.
- `OpenMeteoGateway` turns an exception thrown by `FetchAsync` into a `PollOutcome.Failure` with reason `Transport` for that target, so the requester receives `FetchForecastFailed` and the stream keeps running.
- If the fetch stream nevertheless stops, the gateway answers every in-flight request with `FetchForecastFailed(Transport)` and re-materializes only the fetch stream (same budget gate, same forecast hub), instead of staying silently dead.

## Non-goals

- No change to retry policy, backoff durations, the throttle/token bucket or the budget accounting.
- No reclassification of exceptions inside `OpenMeteoClient` (for example a mid-body `HttpIOException`); the gateway guard covers them generically.
- No change to `njord_openmeteo_fetch_total`: a fetch that throws is not counted there (it may not have reached the API).
- No handling of a failed `Source.Queue.OfferAsync` in the gateway (separate follow-up).
- No change to the independent per-model scheduling; a "cycle" stays "every resolved model of a location has reported once".

## API-budget impact

No new requests on the happy path. A fetch that throws used to stop that target (and, with a stopped stream, every target) from polling at all; now it follows the existing transient backoff (1, 2, 4, 8 min, then the discovery interval of 20 min). Worst case per permanently throwing target: about 72 requests/day, about 2,160/month, which equals discovery-phase polling. For 2 locations × 6 models all failing permanently: about 864/day and 25,920/month, well within the free-tier limits of 10k/day and 300k/month. A re-materialized fetch stream fails its in-flight requests into the same backoff (first retry after 1 min), so it adds no burst.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `structured-logging`: the poll cycle summary counts failed fetches as final outcomes and reports them.
- `prometheus-metrics`: pipeline metrics add `njord_poll_cycle_failed_models` and define `njord_poll_cycle_models` as all models of the completed cycle.
- `stream-supervision`: the decider unwraps single-inner `AggregateException`.
- `poll-pipeline`: a throwing fetch reports back as `FetchForecastFailed`; a stopped fetch stream fails in-flight requests and is re-materialized.

## Impact

- `src/Njord.Pipeline/PollScheduler.cs`, new `src/Njord.Pipeline/PollCycleTracker.cs`, `src/Njord.Pipeline/Telemetry.cs`.
- `src/Njord.Core/Actors/StreamSupervision.cs` (affects every stream using the decider: gateway, pipeline, MQTT consumers).
- `src/Njord.OpenMeteo/OpenMeteoGateway.cs`.
- Tests in `Njord.Pipeline.Tests`, `Njord.Core.Tests`, `Njord.OpenMeteo.Tests`.
- Operators: a new Prometheus series `njord_poll_cycle_failed_models`; the Debug summary text gains a failed count.
