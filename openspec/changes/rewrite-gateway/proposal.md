## Why

The OpenMeteoGateway (235 LOC) mixes budget gating, pipeline delivery, reply routing and
request tracking in one actor. Results bounce back through the actor mailbox (PipeTo + `_pending`
dictionary), adding an unnecessary context switch per fetch. The pipeline data takes a detour
through a separate `_forecastQueue` instead of flowing straight to the SinkRef. Three previous
rewrite attempts revealed the real constraints: the ShardMessageExtractor throws on anything
without `IWithLocationId`, `Sink.ActorRef` sends a completion message on graceful stream end,
and the budget gate must not be called from concurrent threads.

## What Changes

- **Gateway stream pipeline.** Pipeline SinkRef becomes the stream's main Sink. Replies go via
  `AlsoTo(Select(toReply) -> Sink.ActorRef(pollWorkerRegion))`. The actor only owns lifecycle:
  sync resolution, SinkRef handshake, queue offering. No `_pending`, no `_forecastQueue`, no
  result routing in the actor.
- **Resilient stream.** `WithAttributes(StreamSupervision.LoggingDecider)` resumes individual
  failures. No generation tracking, no `StreamFailed`/`StreamCompleted` handlers.
- **Separated gate and fetch stages.** `Select` (synchronous, serialized by backpressure) for
  the budget gate check, `SelectAsyncUnordered(3)` for the HTTP calls. No lock needed.
- **Synchronous actor resolution.** `Context.GetActor<T>()` in constructor/PreStart instead of
  async resolve + PipeTo.
- **PostStop cancels the CTS only.** The `ActorMaterializer` auto-disposes with the actor;
  abrupt termination does not send `Sink.ActorRef`'s completion message.
- **PollWorker: Tell instead of Ask.** Sends `FetchDomain` with `Tell`, handles
  `FetchDomainResponse` as a regular message via shard routing. Own reply-timeout timer
  replaces the Ask timeout.
- **`FetchDomainResponse` implements `IWithLocationId`** so the `ShardMessageExtractor` routes
  replies to the correct PollWorker entity.
- **`FetchTimeouts` simplified.** `Http` (dead code) and `Reply` (no longer needed) removed.
  `Admission` remains for the pipeline SinkRef handshake.

## Non-goals

- Changing `IDomainClient`, `FetchResult`, or the six client implementations.
- Changing the pipeline (`ForecastPipeline`, `MergeHub`, `FeatureEngine`).
- Changing the gateway's `BackoffOnFailure` singleton registration.
- Adding new telemetry beyond what exists today.

## API budget impact

No change to polling frequency, call patterns, or call weight formulas.

## Capabilities

### New Capabilities

- `gateway-stream-pipeline`: Stream-based gateway dispatch with pipeline SinkRef as main sink,
  reply branch via `AlsoTo` + `Sink.ActorRef`, resilient supervision, and synchronous actor
  resolution.

### Modified Capabilities

- `domain-clients`: Gateway dispatches through a stream pipeline instead of PipeTo; clients
  are unchanged but the gateway's client-lookup moves into the stream.
- `stream-supervision`: Gateway stream uses `StreamSupervision.LoggingDecider` for resilience.
- `stream-composition`: Pipeline SinkRef is the stream's main sink (not a separate queue).

## Impact

- **`Njord.OpenMeteo`**: gateway rewrite, budget gate lock removed.
- **`Njord.Polling`**: PollWorker switches from Ask to Tell with reply-timeout timer.
- **`Njord.Messages`**: `FetchDomainResponse` implements `IWithLocationId`.
- **`Njord.Core`**: `FetchTimeouts` simplified (2 fields removed).
- **Test projects**: `Njord.OpenMeteo.Tests` (gateway specs), `Njord.Polling.Tests`
  (PollWorker fetch reply specs), `Njord.Pipeline.Tests` (GatewayRestartSpec).
