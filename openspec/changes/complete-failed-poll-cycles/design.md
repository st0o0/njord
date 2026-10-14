## Context

See proposal.md (Why) for the two defects. Current state that shapes the approach:

- `PollScheduler.RecordPollCompletion` is called only from `OnFetchForecastCompleted`. Its per-location `PollCycleTracker(Start, Changed, Reported)` counts replies, not distinct models, and completes at `Reported >= total`. `OnFetchForecastFailed` never touches it.
- Models of one location are scheduled independently (discovery interval, learned cycle, transient backoff). A "cycle" is therefore "every resolved model of the location reported once since the tracker opened", not a synchronized tick.
- The failure-routing spec retries every failure reason forever with transient backoff (1, 2, 4, 8 min, then the discovery interval). There is no "final failure" state in the scheduler.
- The tracker dictionary is keyed by configured location, so it is bounded (one per location); the defect is miscounting, not an unbounded leak. Reports for unknown locations already return early.
- Akka.Streams 1.5.73 `SelectAsync`/`SelectAsyncUnordered` (verified with a throwaway probe): a task that faults after an `await` reaches the decider unwrapped; a task that is already faulted when handed to the stage (`Task.FromException`, an `async` method that throws before its first real suspension) or an exception that is itself an `AggregateException` (`.Result` inside) reaches the decider as `AggregateException`, which `Classify` maps to `Stop`.
- `OpenMeteoClient.FetchAsync` catches `HttpRequestException`, timeout `TaskCanceledException` and `JsonException`, but can still throw: `InvalidOperationException` from `JsonElement.GetDouble()` on a non-numeric array entry, `HttpIOException`/`IOException` while reading the body (`ResponseHeadersRead`), `NotSupportedException` for an unsupported content charset, `ArgumentOutOfRangeException` from absurd unix timestamps, and caller-cancellation `OperationCanceledException`.
- When the fetch stream stops, `Sink.ActorRef` sends `StreamCompleted`; the gateway only logs a warning. `_pendingRequests` keeps the in-flight requesters forever, later `OfferAsync` calls fail unobserved, and the scheduler re-schedules a target only when it gets a reply, so those targets never poll again.
- The gateway runs under a `BackoffOnFailure` supervisor; its forecast `BroadcastHub` feeds `ForecastPipeline` through a `SourceRef`.

## Goals / Non-Goals

**Goals:**
- Every poll cycle completes once each resolved model has a final outcome, with correct duration and counts; trackers removed on completion.
- A throwing fetch produces `FetchForecastFailed` for its target; the fetch stream survives it.
- A fetch stream that stops anyway recovers without a process restart and without leaving requesters unanswered.

**Non-Goals:**
- Changing retry policy, budget, throttle, or the client's own exception mapping (see proposal Non-goals).

## Decisions

### D1: A failure is a final outcome for the current cycle; the cycle does not wait for the retry

Retries are unbounded and can be up to a discovery interval apart, so waiting for them would keep a cycle open for the whole outage and the summary would never show failures, which is exactly the visibility we want. The retry belongs to whatever cycle is open when it reports. If the retry lands while the same cycle is still open (other models outstanding), it replaces that model's outcome (latest wins), so a completed summary describes the latest known state of each model and each model is counted once.

Alternatives: wait for the retry (rejected: cycles never complete during outages); first outcome wins (rejected: a model already recovered would be reported failed).

### D2: Track outcomes per model, not a reply counter

Extract an internal `PollCycleTracker` (in `Njord.Pipeline`) holding, per location, the cycle start and a model-id → outcome map (`Changed`, `Unchanged`, `Failed`, case-insensitive model ids). `Record(location, model, resolvedModels, outcome, now)` returns a `PollCycleSummary(Location, Total, Changed, Failed, Duration)` when the map covers every resolved model and removes the entry; reports for a model outside the resolved set open nothing. It is pure and unit-testable without an actor; `OpenCycles` exposes the count so specs can prove nothing accumulates. `PollScheduler` feeds it from both `OnFetchForecastCompleted` (inside the persist callback for changed data, as today) and `OnFetchForecastFailed` (all reasons).

### D3: Metrics stay compatible; failures get their own gauge

`njord_poll_cycle_duration_seconds` and `njord_poll_cycle_models` keep their names, units and `location` label; `njord_poll_cycle_models` records the number of models in the completed cycle (all of them, including failed ones), which equals what it recorded for an all-success cycle before. A new gauge `njord_poll_cycle_failed_models` (`{model}`, label `location`) records the failed count (0 for a clean cycle), so dashboards can alert on it. Adding an `outcome` label to the existing gauge was rejected: it would split the existing series. The Debug summary becomes `"Poll complete for {Location}: {Changed}/{Total} models changed, {Failed} failed in {Duration}ms"`.

### D4: The decider unwraps single-inner `AggregateException`

`Classify` unwraps `AggregateException` while it has exactly one inner exception, then classifies the inner one. An aggregate with several inner exceptions stays `Stop` (no single cause to judge). The log line names the unwrapped type. This applies to every stream using the decider; it only turns wrapped transient faults into `Resume`, which is what those streams already do for the unwrapped ones.

### D5: The gateway converts a throwing fetch into a `Transport` failure

The `SelectAsyncUnordered` lambda wraps `FetchAsync` in `try/catch (Exception)` and returns `FetchResult(new PollOutcome.Failure(location, model, Transport, "Fetch threw <Type>: <message>"), key)`, logging a warning with the exception. Relying on the decider alone is not enough: even unwrapped, `InvalidOperationException` is `Stop`, and `Resume` drops the element, so the requester would still never get feedback and the target would never be re-scheduled. `Transport` is chosen because the scheduler treats all non-rate-limited reasons alike (transient backoff) and the cause is unknown; the detail carries the type.

### D6: A stopped fetch stream is re-materialized in place, not by crashing the actor

On `StreamCompleted` for the current stream generation, the gateway logs a warning, answers every entry in `_pendingRequests` with `FetchForecastFailed(Transport, "Gateway fetch stream stopped")`, clears it, and materializes a new fetch stream (new queue, same `GatewayBudgetGate`, same forecast queue and hub). Each materialization carries a generation number in its `StreamCompleted` message so a late completion from a replaced stream is ignored.

Alternative: throw so the `BackoffOnFailure` supervisor restarts the gateway. Rejected: a restart re-materializes the forecast `BroadcastHub` and kills `ForecastPipeline`'s `SourceRef`, resets the budget gate's token bucket, and the requesters still receive no reply (the scheduler watches the gateway but a restarted child is not `Terminated`), so their targets would stay unscheduled. Re-materializing in place keeps the hub and the budget state and turns lost requests into ordinary retries. With D5, the stream can only stop on an internal defect, so a re-materialization loop is bounded by the scheduler's backoff.

## Risks / Trade-offs

- [The catch-all in D5 could hide programming errors as transport failures] → each one is logged at Warning with the exception and type in the detail, and retries follow the backoff, so it stays visible without stopping polling.
- [Latest-wins can hide a short-lived failure from the cycle summary] → each failure is still logged at Warning and the retry's own outcome is reported; the summary is about the state at completion.
- [A model that is scheduled far ahead (steady phase) keeps a cycle open for hours] → unchanged from today; the tracker stays bounded at one per location.
- [Decider change affects pipeline and MQTT streams] → it only resumes faults that are already resumed when unwrapped; covered by the existing decider specs plus new wrapped cases.

## Migration Plan

No persisted state changes. Deploy normally; rollback restores the previous behavior. Dashboards can start using `njord_poll_cycle_failed_models`.
