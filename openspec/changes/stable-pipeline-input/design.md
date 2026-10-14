## Context

See proposal.md - Why. Today `OpenMeteoGateway` owns the fetch stream and the forecast `BroadcastHub`, answers
`FetchForecast` from a waiting list fed into a backpressured queue one offer at a time, and re-materializes
its fetch stream in place with generation tags. `ForecastPipeline` resolves and watches the gateway, asks it
for a `SourceRef<Forecast>`, builds its analysis graph and output hub per received SourceRef, asks the
SensorHub once per forecast, and rewires (with its own backoff and rewire counter) when the SourceRef ends.
`PollScheduler` sends `FetchForecast` with `Tell` and acknowledges replies with `Ack`.

Constraints: `Njord.OpenMeteo` and `Njord.Pipeline` must not reference each other (they meet through
`Njord.Core` markers and messages); `Njord.Messages` stays Akka-free, so records carrying `ISinkRef` live in
`Njord.Core/Actors/StreamWiringMessages.cs`; `TimeProvider` for all time decisions.

## Goals / Non-Goals

**Goals:**
- The pipeline's analysis state and its consumers' SourceRefs survive any gateway restart.
- Every fetch request ends with exactly one reply, rejection or requester timeout.
- No fetch is spent on a forecast that cannot reach the pipeline.

**Non-Goals:**
- Consumer changes and a permanent drain on the output hub (change B); poll sharding (change C).

## Decisions

### D1 Gateway private stream (FunkArr pattern)
`Source.Queue<FetchTarget>(16, DropNew)` → `GatewayThrottleStage` → `SelectAsyncUnordered(2, try/catch)` →
`Sink.ActorRef(Self)`, materialized in `PreStart` with `Context.Materializer()`. `DropNew` accepts concurrent
offers (a backpressured queue faults on a second pending offer), so each request is offered directly and its
`IQueueOfferResult` comes back via `PipeTo`; anything but `Enqueued` answers `Rejected(QueueFull)` (a failed
offer is a `Failed(Transport)`). Pending requests are a `Dictionary<long, PendingRequest>`; a reply removes the
entry first, so a request is answered at most once. The stream's completion or failure message makes the
gateway answer every open request with `Failed(Transport, "gateway stream stopped")` and then throw, so
BackoffOnFailure restarts it with a fresh stream. Alternatives: keep the in-place rebuild (rejected — it is
the complexity the concept removes); Backpressure queue + waiting list (rejected for the same reason).

### D2 Budget rejection instead of unbounded waiting
`FetchTarget` carries an admission deadline = receipt time (`TimeProvider`) + `FetchTimeouts.Admission`
(20 s). The throttle stage pushes an `Admission` (admitted target or rejection) instead of the bare target:
- tokens available → admitted (and `RecordApiCall`, unchanged accounting);
- no tokens and `now + EstimateDelay(weight) ≤ deadline` → park and retry on the timer (burst smoothing);
- otherwise → `Rejected(Budget)` without consuming tokens; a target that reaches the stage already past its
  deadline → `Rejected(Expired)`.
Rejections flow through `SelectAsyncUnordered` as completed tasks, so every request still produces exactly one
element towards `Self`. With the free tier (480 cost-units/min, 8/s) a weight-1 request waits at most ~125 ms,
so `Budget` rejections only occur with a very low `BudgetOverride` or an exhausted burst bucket. The stage
gets the `TimeProvider` for the deadline check; it still contains no token-bucket logic.
Alternative: a pre-check in the actor (rejected — the gate's state is owned by the stream thread).

### D3 Named timeouts, one place
`FetchTimeouts(Admission = 20 s, Http = 30 s, Reply = 60 s)` (`Njord.Core.Configuration.Polling`) is a DI
singleton (`TryAddSingleton(FetchTimeouts.Default)` in the OpenMeteo and Pipeline setup containers). The
gateway uses `Admission`, the typed `HttpClient` uses `Http`, the scheduler's Ask uses `Reply`, and a spec
pins `Reply > Admission + Http`, so a reply always arrives before the requester gives up. Tests register short
values. Alternative: a `PollingOptions` value (rejected — not a user-facing setting).

### D4 Forecast branch and SinkRef handshake
On start (and on re-attach) the gateway resolves `IForecastPipeline` with `Context.GetActorAsync` (waits for
registration), sends `RequestForecastSink(RequestId)` with `Self` as sender and starts a timer
(`FetchTimeouts.Admission`) for that request id. Replies with another request id are ignored.
`RequestForecastSinkCompleted` → watch the replying actor (the real pipeline incarnation, not the singleton
proxy), run `Source.Queue<Forecast>(16, DropHead).WatchTermination(Keep.Both).To(sinkRef.Sink)` and pipe
its termination task to `Self` tagged with a branch id. Branch ended / pipeline `Terminated` / handshake
failed / timed out → drop the branch and retry after jittered exponential backoff (1 s → 30 s, reset on a
successful attach); a re-attach after a lost branch increments `njord_openmeteo_pipeline_reattaches_total`.
A successful fetch's forecast is offered into the branch and the requester gets `Completed(hash)` only when
the offer is `Enqueued`; with no branch (or a failed offer) the reply is `Rejected(PipelineUnavailable)`, so
the scheduler does not record a hash the pipeline never saw. A `FetchForecast` arriving without a branch is
rejected before it is queued, so no fetch is wasted. A pipeline restart therefore only re-attaches this
branch; the fetch stream is untouched.

### D5 Pipeline hubs once per incarnation
`PreStart` materializes, with `Context.Materializer()`: `MergeHub.Source<Forecast>(16)` → a three-way
`ZipWith(forecast, sensor map, settings)` (both side channels `Source.ActorRef(10, DropHead)` with an initial
element and `RepeatPrevious`) → `StatefulSelectMany` → `WatchTermination` → `BroadcastHub.Sink(2)`. On
`RequestForecastSink` the pipeline runs `StreamRefs.SinkRef<Forecast>()` → `RecoverWithRetries(_ =>
Source.Empty, 1)` → merge sink and replies with the request id. The recovery matters: Akka.NET's MergeHub
fails the whole hub when one producer fails (documented on `MergeHub.Source`), and a SinkRef source fails
when its remote partner dies; a completed producer never completes the hub. The analysis graph ending
(completed or failed) makes the pipeline throw; it is registered behind BackoffOnFailure, so a restart builds
fresh hubs and producers/consumers re-attach (the only case they do). The pipeline drops its gateway
resolution, watch, stash, backoff, rewire logic and the rewire counter.

### D6 Sensor snapshots are pushed
`SensorSnapshots(ImmutableDictionary<LocationId, SensorSnapshot> Snapshots)` (`Njord.Messages/Sensors`). The
SensorHub builds the map from its state with the staleness window applied at push time and tells it to the
pipeline marker on every accepted `UpdateReading`, on every expiry tick (60 s) and once when
`GetActorAsync<IForecastPipeline>` resolves at start; until then pushes are skipped (`TryGetActor`). The
pipeline forwards the map to its sensor side channel; the stage looks up `new LocationId(forecast.Location)`
(case-insensitive by `LocationId` equality); no entry → `null` for `IndexAnalyzer`. A reading that turns stale
between pushes leaves the map at the next tick at the latest (≤ 60 s). A restarted pipeline starts with an
empty map and gets the full map again with the next tick. Alternative: keep the per-forecast ask (rejected —
it puts an ask with a timeout inside the analysis stream, the seam pattern the concept removes).

### D7 Scheduler Ask (transitional)
`OnScheduledPoll` asks the gateway with `FetchTimeouts.Reply` and pipes the `FetchForecastResponse` to `Self`;
a failed Ask becomes `FetchForecastFailed(Transport, "no reply within …")`. `FetchForecastRejected` maps to
`ApplyRateLimited` (≥ 5 min) for `Budget` and to `ApplyTransientFailure` for every other reason, and counts
as a failed outcome for `PollCycleTracker`. The `Ack` replies are dropped: piped replies have no sender, so an
`Ack` would only reach dead letters.

## Risks / Trade-offs

- [A forecast is enqueued into the branch but lost when the SinkRef dies right after] → the next poll of the
  target fetches it again; the scheduler's hash is the only effect (one cycle delay at worst).
- [The output hub still backpressures without consumers] → unchanged from today; branch and MergeHub buffers
  (16 + 16) hold startup forecasts until the read model attaches, older ones drop first. Change B adds the
  permanent drain.
- [Pipeline restart drops the sensor map for up to one expiry tick] → indices fall back to configured values
  for at most 60 s.
- [In-flight replies after the requester's Ask timed out reach dead letters] → `Reply` (60 s) exceeds
  admission + HTTP timeout, so this only happens if the client hangs past its own timeout.

## Migration Plan

Internal protocol only; no persisted data changes. Rollback is a revert of the change's commits.
