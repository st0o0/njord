## Context

`StreamConsumerActor` (src/Njord.Core/Actors/StreamConsumerActor.cs) runs the `WaitingForRefs` /
`Ready` state machine of the stream specs. `TryTransition` calls `MaterializeGraph(killSwitch)`, which
returns `void`; both subclasses end their graph in `RunWith(...)` and drop the materialized value.
The only way back to `WaitingForRefs` is `Terminated` of a tracked dependency. `ForecastPipeline`
mirrors this towards the gateway: `MaterializeAnalysisPipeline` ends in `.To(hubSink).Run(mat)` and
only `Terminated` of the gateway triggers a new handshake. The handshake reply is not correlated by
request id (a fresh `Guid` is sent and never checked).

Ways a consumer graph ends while the dependency actor stays alive:

- a stage stops on a non-transient exception (`StreamSupervision.LoggingDecider` returns `Stop`);
- the pipeline hub completes or fails (pipeline graph rebuilt after a gateway restart, or its analysis
  stage stopped), which completes or fails every SourceRef vended from it;
- the StreamRef protocol fails (subscription timeout, remote stage failure).

## Goals / Non-Goals

**Goals:**

- One rewire mechanism in the base class, so a subclass only exposes its graph's completion and drops
  its SourceRefs.
- No rewire for a graph that the actor itself replaced or stopped.
- Backoff that grows when the graph keeps dying right after materialization (a poison pipeline must
  not produce a 1 s rewire loop).

**Non-Goals:**

- Restarting the consumer actor, replaying missed events, changing decider classification.

## Decisions

### D1 - `MaterializeGraph` returns the completion `Task`

`protected abstract Task MaterializeGraph(SharedKillSwitch killSwitch)`. Subclasses return the
termination of the part of the graph that consumes the SourceRef (`Sink.Ignore` materialized value
for MQTT, `WatchTermination` for the read model).

*Alternatives:* (a) a separate `protected void WatchGraph(Task)` the subclass must call - easy to
forget, and the compiler would not enforce it; (b) `Sink.ActorRef`-style completion messages wired by
the subclass - pushes generation bookkeeping into every subclass; (c) the base wraps the SourceRef
itself - the base does not know the subclass's element types or how many sources it has.

### D2 - Generation-tagged completion messages

`TryTransition` increments `_graphGeneration` and pipes the task to `Self` as
`GraphCompleted(Generation)` / `GraphFailed(Generation, Cause)` (project-owned records, `PipeTo` with
success and failure mappers). The handler acts only when the generation equals the current one and
the actor is `Ready`. The generation is also incremented on `Terminated` of a tracked dependency
(before the base shuts down the kill switch itself) and in `PostStop`, so completions caused by the
actor's own kill switch, by a dependency loss or by the actor's stop are stale. Handlers are
registered in both states; in `WaitingForRefs` every completion is stale by construction.

*Alternative:* a flag "shutting down" - does not distinguish two consecutive graphs whose
completions can arrive out of order relative to the new handshake.

### D3 - Rewire path and backoff

On a current completion or failure the base: logs a Warning (with the cause for a failure, "completed
unexpectedly" otherwise), increments `njord_stream_consumer_rewires_total{consumer}`, increments the
generation, shuts down and replaces the kill switch, increments the request-id counter, calls the new
virtual `OnStreamLost()` (subclasses clear the SourceRef and its request id, keep the dependency
ref), enters `WaitingForRefs` and schedules `RewireGraph(generation)` after
`retryPolicy.DelayWithJitter(graphLossCount++)`. `RewireGraph` with the current generation calls
`RequestSourceRefs()`; from there the existing handshake, `RequestPipelineOutputFailed` retry and
`TryTransition` path is reused. A `Terminated` while a rewire is pending increments the generation, so
the pending `RewireGraph` is dropped and the dependency recovery takes over.

`graphLossCount` is separate from the resolve `retryCount`, because `TryTransition` resets the
latter on every materialization - a graph that dies immediately would otherwise rewire every second
forever. It is reset by `GraphStable(generation)`, scheduled on each materialization after a
stability window (constructor parameter, default 60 s, twice the 30 s backoff cap): a graph that
survived it counts as healthy.

*Alternatives:* reset on the first element received - the base never sees elements; reading a clock
at materialization - the base has no `TimeProvider` and the scheduler-based check needs none.

The retry policy stays the existing `StreamConsumerActor.DefaultRetryPolicy` (1 s doubling, 30 s
cap, ±25 % jitter from Servus `BackoffPolicy`); tests pass a faster policy through the existing
constructor parameter.

### D4 - Counter on a `Njord.Core` meter

The base lives in `Njord.Core`, so the counter does too: `internal static class Telemetry` with
`Meter("Njord.Core")` and `njord_stream_consumer_rewires_total` (unit `{rewire}`), tag `consumer`
= the subclass's `ConsumerName` (virtual, default `GetType().Name`; bounded: two production
consumers). `CoreSetupContainer.NjordMeters` gains `Njord.Core`. `ForecastPipeline` is not a
`StreamConsumerActor` and cannot reach Core's internal telemetry, so it records
`njord_pipeline_stream_rewires_total` on its own `Njord.Pipeline` meter.

*Alternative:* a public static helper in Core - would break the "internal Telemetry per library"
convention for one call site.

### D5 - State across a rewire, per subclass

- **ForecastReadModel - latest values survive.** They are the last known truth of the pipeline and
  answer unary RPCs; dropping them would turn a transient stream hiccup into `NotAvailable` replies
  until the next poll cycle (up to an hour). The state is an actor field and is not touched by
  `OnStreamLost()`.
- **ForecastReadModel - streaming clients survive.** The secondary `BroadcastHub` and its permanent
  drain are materialized once per actor incarnation (lazily, on the first `MaterializeGraph`), fed
  through a `MergeHub`. Each pipeline subscription runs into the `MergeHub` sink; a `Recover` placed
  after `WatchTermination` keeps a failing subscription from failing the `MergeHub`, and a completing
  producer does not complete a `MergeHub`. Streaming clients therefore keep their SourceRefs and
  simply see no events while the subscription is rewired. *Alternative:* rebuild the secondary hub
  per materialization (today's behavior) - every gRPC stream would end on each rewire and ha-njord
  would have to reconnect; *alternative:* `Source.Queue` + per-element `OfferAsync` - an extra async
  hop per event for the same effect.
- **MqttStatePublisher - dedup cache resets.** The cache stays local to `MaterializeGraph`. A rewire
  means state was not delivered for an unknown time; republishing every retained topic once on the
  next cycle is cheap (one cycle of retained messages) and guarantees Home Assistant converges to the
  current state, including topics whose last publish raced with the failure. *Alternative:* keep the
  cache in an actor field - saves one cycle of publishes at the risk of never re-sending a topic whose
  retained value the broker or HA lost meanwhile.

### D6 - ForecastPipeline gets the same treatment

Fixing only the consumers would leave them re-attaching to a dead pipeline hub (a completed
`BroadcastHub` completes every new subscriber immediately) and rewiring with growing backoff forever.
`MaterializeAnalysisPipeline` therefore returns the analysis graph's `WatchTermination` task;
`ForecastPipeline` pipes it as `AnalysisGraphCompleted(Generation)` / `AnalysisGraphFailed(Generation,
Cause)`. On a current one in `Ready` it logs a Warning, records the counter, returns to
`WaitingForGateway` and schedules the existing `RetryGatewayResolve` after
`GatewayRetryPolicy.DelayWithJitter(graphLossCount++)` (same stability reset as D3). Gateway
`Terminated` increments the generation. The forecast handshake now stores its request id and ignores
replies to an older one, so a late reply to a superseded request cannot materialize a second graph.
The analysis state in the stage closure resets with the graph, as it already does after a gateway
restart (see proposal Non-goals). Consumers holding SourceRefs to the old hub see it end and rewire
through D3.

## Risks / Trade-offs

- [A graph that dies on every element keeps rewiring] → Backoff grows to 30 s and every rewire is a
  Warning plus a counter increment, so the loop is cheap and visible.
- [Events emitted while a consumer rewires are lost for that consumer] → Accepted: the pipeline is a
  live stream; the next poll cycle refills MQTT and the read model (Non-goals).
- [A subclass returns a task that never completes (e.g. `Task.CompletedTask` misuse or a detached
  graph)] → `Task.CompletedTask` would trigger an immediate rewire loop with backoff, visible in logs;
  both production subclasses return the termination of their actual sink.
- [Stale `GraphCompleted` delivered after the actor stopped] → goes to dead letters; no rewire because
  the actor is gone.

## Migration Plan

Internal change, no configuration or wire-format change. Rollback = revert the commit.
