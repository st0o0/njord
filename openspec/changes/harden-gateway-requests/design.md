## Context

See proposal.md (Why). Facts that shape the approach:

- `OpenMeteoGateway.OnFetchForecast` stores the requester in `_pendingRequests["{msg.Location}|{msg.ModelId}"]` and calls `_fetchQueue.OfferAsync(target)` without observing the task. `FetchAsync` builds the result key from `target.Location.Name` (configured spelling). Same key for two in-flight requests of one target: the second overwrites the first.
- Akka.Streams 1.5.73 `Source.Queue(16, Backpressure)` (verified with a probe in `OpenMeteoGatewaySpec`): while the buffer has room, offers complete `Enqueued`; once it is full, one offer stays pending and every further offer faults with `IllegalStateException("You have to wait for previous offer to be resolved to send another request")`. 48 offers at once: 21 `Enqueued`, 27 faulted. The 1.5.73 `Source.Queue` has no `maxConcurrentOffers` overload; `Source.Channel` allows concurrent writers but has no per-write result beyond "written".
- After `complete-failed-poll-cycles` the gateway re-materializes a stopped fetch stream in place (generation-tagged `FetchStreamStopped`) and fails every remembered request with `Transport`.
- `PollScheduler` keys poll states by `"{location}|{model}"` built from configuration at startup, but builds the key of a reply from the reply's spelling; `ApplyHashResult` / `ApplyTransientFailure` create a fresh state for an unknown key and `ScheduleNext` then polls it, so a differently spelled reply creates a ghost target (spec run: 3 entries for one configured target).
- `ForecastReadModel` stores state in `ImmutableDictionary<string, LatestResults>` keyed by the event's location name (ordinal); models are already matched case-insensitively. `LocationId` already has case-insensitive equality and hash code.
- `WeatherGrpcService.GetEnrichments` already queries with the catalog's `CatalogLocation.Id`; `GetForecast` still uses `new LocationId(request.Location)` and the raw model string. The streaming RPCs filter with `LocationId` equality (case-insensitive) and are fine. `OpsGrpcService.TriggerPoll` passes the raw location, but the scheduler resolves it with `FindLocation` (case-insensitive).

## Goals / Non-Goals

**Goals:**
- Exactly one reply per `FetchForecast`, under every queue condition, including concurrent requests for one target.
- One canonical spelling inside the system (the configured one), with case-insensitive matching at the boundaries where names come in (gateway requests, scheduler replies, read-model queries, `GetForecast`).

**Non-Goals:**
- Changing the budget gate, throttle, fetch parallelism or scheduler backoff; deduplicating requests (see proposal Non-goals).

## Decisions

### D1: Correlate by a per-request id, not by a location/model string

Each accepted `FetchForecast` gets a gateway-local `long` request id. `FetchTarget` and the internal `FetchResult` carry it; the gateway keeps `Dictionary<long, PendingRequest>` with the requester, the requested `LocationId` and model id, the `FetchTarget` and whether it has been offered. A reply is sent through one helper that removes the entry first, so a request can be answered at most once and a late or duplicate signal (old-generation offer result, result after a stream stop) finds nothing.

Alternatives: a case-insensitive string key (rejected: still collapses two in-flight requests for the same target into one reply); coalescing duplicates onto one fetch (rejected: changes call volume and the scheduler's miss counting; out of scope).

### D2: An in-actor waiting list with one offer outstanding at a time

The gateway appends accepted requests to a `Queue<long>` and offers only when no offer is outstanding: `OfferAsync(target).PipeTo(Self, success: r => new OfferCompleted(id, generation, r), failure: ex => new OfferFailed(id, generation, ex))`. On `OfferCompleted`/`OfferFailed` for the current generation it clears the outstanding slot, answers a non-`Enqueued` result (`Dropped`, `QueueClosed`, `Failure`) or a faulted offer with `FetchForecastFailed(Transport, "Gateway could not enqueue the request: ...")`, and offers the next waiting request. `Enqueued` leaves the request waiting for its fetch result. A synchronous throw from `OfferAsync` is treated like a faulted offer.

This is the backpressure the queue was configured for: the throttle and the two HTTP slots still pace the stream, and requests that do not fit simply wait in the actor instead of being rejected. The handler never awaits; the offer result returns as a message.

Alternatives:
- `OverflowStrategy.DropNew` with concurrent offers and a failure per `Dropped` (rejected: every burst bigger than 16 — every startup with more than ~18 targets — would turn into spurious transport failures, 1-min backoffs and warnings, although the requests could simply wait).
- `Source.Channel` / `ChannelWriter.WriteAsync` with concurrent writes (rejected: one pending task per waiting request, no `QueueClosed`/`Dropped` distinction, and still needs per-write PipeTo; no gain over the explicit list).
- Bigger buffer (rejected: only moves the limit).

### D3: The waiting list is bounded; overflow is answered immediately

The waiting list holds at most 1024 requests (`MaxWaitingRequests`). Normal operation never comes close: the scheduler keeps at most one request per configured target outstanding, plus manual `TriggerPoll` requests. A request beyond the bound is answered at once with `FetchForecastFailed(Transport, "Gateway request backlog is full")`, which protects memory against a flood of manual triggers while still giving every request its reply.

### D4: `Transport` as the reason, no new enum value

The scheduler treats `Transport`, `ModelUnavailable` and `MalformedPayload` alike (transient backoff); a new `Overloaded` value would need a new scheduler case (a missing case leaves the target unscheduled — exactly the failure being fixed), proto/telemetry consideration and buys no different behaviour. The detail text distinguishes the cause in logs and in the scheduler's warning.

### D5: Stream recovery keeps waiting requests

On `FetchStreamStopped` for the current generation the gateway fails only requests that were offered (outstanding or enqueued) — the stopped stream lost them — clears the outstanding slot, re-materializes the fetch stream (new generation) and resumes offering the waiting list into the new queue. Offer results tagged with an old generation are ignored apart from the reply helper's idempotence. This keeps the `complete-failed-poll-cycles` behaviour for in-flight requests and avoids failing requests that the old stream never saw.

### D6: Requested spelling out, configured spelling in

The gateway resolves the location with `FindLocation` (case-insensitive) and the model against the location's resolved models (`NjordOptions.Models ∪ location.Models`, case-insensitive; an unknown model keeps the requested spelling, as the gateway never validated models). The `FetchTarget` uses the configured spellings, so the client, the forecast and the pipeline see configured names. The reply echoes the request's `LocationId` and model id so the requester always correlates it in its own terms.

### D7: Canonicalize at the remaining boundaries

- `PollScheduler`: `OnFetchForecastCompleted`/`OnFetchForecastFailed` resolve the reply to the configured location name and model spelling first (same resolution as `InitializeStates`); an unknown target is logged at Warning and ignored (a `Completed` still gets its `Ack` so the gateway-side ask pattern of existing callers is unaffected). The persisted `DataChanged` event then always carries the configured spelling.
- `ForecastReadModel`: the state map becomes `ImmutableDictionary<LocationId, LatestResults>`, using `LocationId`'s case-insensitive equality; stored data keeps the spelling of the event (configured).
- `WeatherGrpcService.GetForecast`: queries with `location.Id` and the catalog's model spelling (`location.Models` match, case-insensitive), as `GetEnrichments` does; the not-found message names the configured spelling.

## Risks / Trade-offs

- [Waiting requests delay startup polls rather than failing them] → intended: they are fetched as fast as the throttle and HTTP slots allow, which is what would happen if they had fitted the buffer.
- [A wedged stream with an offer that never resolves would hold the waiting list] → the existing stream-stop recovery clears the outstanding slot; a stream that neither stops nor accepts is the same hazard as before (the throttle already parks elements by design).
- [Ignoring replies for unknown targets in the scheduler hides a misrouted reply] → logged at Warning; such replies can only come from a target that is not configured, which the scheduler must not poll anyway.
- [Read model keyed case-insensitively merges two event spellings into one entry] → events carry the configured spelling; merging is the desired outcome if they ever differ.

## Migration Plan

No persisted state, message or proto changes. Deploy normally; rollback restores the previous behaviour. Scheduler states persisted under a ghost key by an earlier version stay in the snapshot but are no longer scheduled from replies (only configured targets are initialized).
