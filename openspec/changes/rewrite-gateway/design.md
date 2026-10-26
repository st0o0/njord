## Context

The `OpenMeteoGateway` (236 LOC) is a `ReceiveActor` singleton behind `BackoffOnFailure` (3-30 s,
unlimited retries). Today it dispatches fetch requests with `FetchAsync.PipeTo(Self)`, routes
replies through a `_pending` dictionary keyed by `long` id, and offers pipeline data through a
separate `_forecastQueue` (`Source.Queue<DomainDataReceived>` -> `SinkRef.Sink`). Actor resolution
is partly async (`GetActorAsync` + PipeTo for the pipeline), partly sync (`GetActor` for the budget
tracker). Location coordinates are resolved from `NjordOptions` in the actor, making the reply
carry `Resolved` coordinates instead of using `FetchDomain.Location` directly.

Three failed rewrite attempts uncovered hard constraints:
- The `ShardMessageExtractor` throws `ArgumentException` on any message not implementing
  `IWithLocationId`. Sending a `Sink.ActorRef` completion message to the poll shard region would
  crash it.
- The budget gate (`GatewayBudgetGate`) has mutable token-bucket state and must not be called from
  concurrent threads.
- `FetchTimeouts.Http` is dead code (the HTTP timeout comes from `StandardResilienceHandler`);
  `FetchTimeouts.Reply` is only the Ask timeout used by PollWorker.

FunkArr reference (`MediathekViewWebManager`, 104 LOC): `Source.Queue` ->
`SelectAsyncUnordered(maxConcurrent)` -> `Sink.ActorRef(Self)`. Actor replies from `_pending`.
Resilient via `Deciders.ResumingDecider`. The completion message goes to `Self`, not to a shard
region.

## Goals / Non-Goals

**Goals:**
- Pipeline SinkRef as the stream's main Sink (eliminate `_forecastQueue`).
- Reply branch via `AlsoTo` + `Sink.ActorRef(pollWorkerRegion)`, no actor hop.
- Separated budget gate (synchronous `Select`, serialized by backpressure) and fetch
  (`SelectAsyncUnordered(3)`) stages. No lock in the gate.
- Resilient stream via `StreamSupervision.LoggingDecider`. No generation tracking.
- Synchronous actor resolution (`Context.GetActor<T>()`).
- PollWorker Tell with own reply-timeout timer instead of Ask.
- Simplify `FetchTimeouts` (remove `Http` and `Reply`).

**Non-goals:**
- Changing `IDomainClient`, `FetchResult`, or the six client implementations.
- Changing the pipeline (`ForecastPipeline`, `MergeHub`, `FeatureEngine`).
- Changing the gateway's `BackoffOnFailure` singleton registration.
- Adding new telemetry counters beyond what exists today.

## Decisions

### 1. Pipeline SinkRef as main Sink

The fetch stream flows straight into the pipeline: after the `AlsoTo` reply branch, a `Where`
filters successes, `SelectMany` expands multi-model responses, `Buffer(16, DropHead)` absorbs
backpressure, and `SinkRef.Sink` is the terminal Sink. This eliminates the separate
`_forecastQueue`, the `BranchEnded` lifecycle tracking, and the `OfferAllAsync` helper.

Alternative: keep `_forecastQueue` as today. Rejected -- it adds an unnecessary queue hop and
lifecycle plumbing (`BranchEnded`, `WatchTermination`, `OfferAsync.PipeTo`).

### 2. AlsoTo + Sink.ActorRef(region) for replies

All fetch attempts pass through the `AlsoTo` branch. A `Select` stage maps each `FetchAttempt` to
the appropriate `FetchDomainResponse` (Completed, Failed, or Rejected).
`Sink.ActorRef<FetchDomainResponse>(region, endMarker, ex => failMarker)` delivers each reply to
the poll shard region, which routes it to the correct PollWorker entity via `IWithLocationId`.

Alternative: `Sink.ForEach(region.Tell)`. Works, but `Sink.ActorRef` participates in stream
lifecycle (backpressure, dead-letter detection) and is the established pattern in this codebase
(`GrpcConsumer` line 71, `GatewayTestActors` line 30). `ForEach` is a black box.

### 3. PostStop cancels CTS only -- no queue.Complete()

The `ActorMaterializer` is created via `Context.Materializer()` and auto-disposes when the actor
stops. Abrupt materialization shutdown does NOT invoke `onUpstreamFinish` on `Sink.ActorRef`, so
the completion message is never sent to the poll region. The `CancellationTokenSource` cancels
in-flight HTTP calls promptly.

If `_requestQueue.Complete()` were called, the stream would complete gracefully, sending the
completion marker to the region and crashing its extractor.

The `Sink.ActorRef` completion and failure markers can be simple private record types handled by
the gateway itself (e.g. `Receive<StreamEnded>` that throws for supervision restart). They will
never reach the region under normal operation, but if they did, the gateway would already be gone
and the region would dead-letter them. As a defence-in-depth measure, the markers implement
`IWithLocationId` with a well-known sentinel name so the extractor does not throw.

### 4. Synchronous Select for budget gate, SelectAsyncUnordered(3) for fetch

The budget gate is a synchronous token check -- no I/O, no async. A plain `Select` stage runs
it in the stream's single-threaded context, serialized by backpressure from the downstream
`SelectAsyncUnordered(3)`. No `MapAsync`, no lock.

The `GatedRequest` intermediate carries either a rejection reason (gate denied or unknown client)
or the resolved `IDomainClient`. The subsequent `SelectAsyncUnordered(3)` passes rejections
through unchanged and calls `FetchAsync` only on gated requests, giving up to 3 concurrent HTTP
calls.

### 5. PollWorker Tell + reply timer instead of Ask

Today: `gateway.Ask<FetchDomainResponse>(FetchDomain, timeout).PipeTo(Self, ...)`. The Ask creates
a temporary actor whose ref becomes `Sender` in the gateway. The reply goes back to that temporary
actor, which completes the Task, which PipeTo wraps into `DomainFetchReplied`.

New: `gateway.Tell(FetchDomain)`. PollWorker starts a named timer per domain
(`StartSingleTimer($"fetch-{domain}", FetchReplyTimeout(domain), timeout)`). The gateway's stream
routes the reply to the shard region, which delivers it to the PollWorker entity as a regular
message. PollWorker handles `FetchDomainResponse` in a new `Receive<FetchDomainResponse>` handler
that cancels the timer and calls the existing `OnDomainFetchResult`. If the timer fires first,
it produces a `FetchDomainFailed(Transport)` -- the same outcome as an `AskTimeoutException` today.

The timeout value is the sum of admission + max HTTP: 60 + 30 = 90 s. This is shorter than the
current 120 s Ask timeout, which included headroom that is no longer needed.

Alternative: keep Ask and carry `IActorRef` in the stream tuple. Rejected -- couples the stream
data to actor references and prevents shard-routed replies.

### 6. FetchDomainResponse implements IWithLocationId

The abstract `FetchDomainResponse` gains:
```
public abstract Location Location { get; }
public string LocationName => Location.Name;
```
All three subtypes already carry `Location` as a positional parameter. The `ShardMessageExtractor`
already routes on `IWithLocationId` without changes.

Alternative: wrapper message. Rejected -- adds a type with no purpose; the Location is already
there.

### 7. FetchTimeouts simplified

`Http` is dead code -- the HTTP timeout is configured by the `StandardResilienceHandler` on the
typed `HttpClient`. `Reply` is the Ask timeout -- no longer needed when PollWorker uses Tell with
its own timer. Only `Admission` remains (the pipeline SinkRef handshake timeout, 60 s).

`FetchTimeouts` becomes a single-field record. The gateway constructor takes it for the SinkRef Ask.

### 8. Stream topology

```
Source.Queue<FetchDomain>(64, DropNew)
  .Select(request => {                           // synchronous, backpressure-serialized
      client lookup + gate.TryAcquire
      → GatedRequest(request, client?, rejection?)
  })
  .SelectAsyncUnordered(3, async gated => {      // concurrent HTTP, CancellationToken
      if rejected → FetchAttempt with rejection
      else → client.FetchAsync → FetchAttempt
      catch → FetchAttempt with error
  })
  .AlsoTo(                                       // reply branch
      Flow.Create<FetchAttempt>()
          .Select(toReply)                       // → FetchDomainResponse
          .To(Sink.ActorRef<FetchDomainResponse>(
              region, StreamEnded.Instance, _ => StreamEnded.Instance))
  )
  .Where(a => a.Result is FetchSuccess)          // only successes to pipeline
  .SelectMany(toDomainDataReceived)              // multi-model expansion
  .Buffer(16, DropHead)
  .WithAttributes(supervision)                   // LoggingDecider: Resume all
  .To(sinkRef.Sink)                              // pipeline IS the sink
  .Run(materializer)
  → returns ISourceQueueWithComplete<FetchDomain>
```

Telemetry (`FetchTotal`, `FetchDuration`) is recorded inside the `SelectAsyncUnordered` lambda,
after the fetch or on rejection. The counters are static and thread-safe.

### 9. NjordOptions removed from the gateway

The gateway today resolves `Location` from `NjordOptions.FindLocation(msg.Location.Name)` and
replies with the configured coordinates. The `FetchDomain` message already carries a `Location`
with name, latitude and longitude (set by PollWorker from its persisted state, which comes from
the same options at startup). The gateway can use `FetchDomain.Location` directly.

This removes `IOptions<NjordOptions>` from the constructor and the "location not found" failure
path.

## Risks / Trade-offs

- [PollWorker reply timer adds code to PollWorker] → The timer is a single
  `StartSingleTimer`/`CancelTimer` pair per domain, replacing the Ask+PipeTo+DomainFetchReplied
  chain. Net LOC is similar or lower.
- [Budget gate fault drops the element, not the actor] → `StreamSupervision` resumes; the
  PollWorker's reply timer fires a transport failure. Better than today (gate fault in PipeTo
  mapper crashes the actor and loses all in-flight requests).
- [Sink.ActorRef completion message on unexpected graceful completion] → Defence-in-depth:
  `StreamEnded` implements `IWithLocationId` with a sentinel name so the extractor does not throw.
  Under normal operation (PostStop = materializer dispose = abrupt termination) the message is never
  sent.
- [FetchDomain messages before SinkRef handshake] → `_requestQueue` is null until
  `OnSinkCompleted`; `OnFetchDomain` rejects with `PipelineUnavailable`. Same as today.

## Open Questions

None.
