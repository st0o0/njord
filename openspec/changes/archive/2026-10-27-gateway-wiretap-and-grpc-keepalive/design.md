## Context

The `OpenMeteoGateway` uses `AlsoTo(ReplySink)` to fork replies to the poll shard region. `AlsoTo` propagates backpressure from the side sink, which is a latent risk if the reply path ever gains processing steps.

The `GrpcConsumer` materializes a per-client `SourceRef` stream for each `RequestGrpcStream` call. These streams connect to the consumer's `BroadcastHub` and live until the SourceRef consumer completes. A dead gRPC client (ha-njord crash) stops consuming but the stream stays open forever, holding a hub slot and buffer memory. There is no liveness detection.

Aaron Stannard's dynamic-hub-keepalive pattern (github.com/Aaronontheweb/akka-dynamic-hub-keepalive) solves this: `KeepAlive` on the producer side injects heartbeats during quiet periods, `IdleTimeout` on the consumer side tears down dead connections, and a backpressure `Buffer` decouples consumer speed from the timeout.

## Goals / Non-Goals

**Goals:**
- Prevent reply sink backpressure from ever affecting pipeline data delivery (WireTap)
- Detect and clean up dead gRPC client streams (KeepAlive + IdleTimeout)
- Preserve slow-but-alive clients by injecting heartbeats during poll-interval gaps
- Switch per-client buffer to Backpressure to avoid silent data loss

**Non-Goals:**
- Changing the pipeline-level stream topology
- HTTP/2 PING-based keepalive (transport concern)
- Rate-limiting gRPC clients

## Decisions

### 1. WireTap replaces AlsoTo on the gateway reply sink

One-line change in `OpenMeteoGateway.OnSinkCompleted`:
```
.AlsoToMaterialized(ReplySink(pollRegion), Keep.Left)
  → .WireTap(ReplySink(pollRegion))
```

`WireTap` returns `NotUsed` as materialized value while `AlsoToMaterialized` returns the left mat value. Since the left mat value is `ISourceQueueWithComplete<FetchDomain>` from `Source.Queue`, we need to use `.Via(Flow.Create<T>().WireTap(...))` or restructure slightly. Actually, `WireTap` on `Source<T, TMat>` preserves the materialized value of the source, so the call site becomes:
```csharp
// Before: .AlsoToMaterialized(ReplySink(pollRegion), Keep.Left)
// After:  .WireTap(ReplySink(pollRegion))
```

No functional difference today since `pollRegion.Tell()` never backpressures. Purely defensive.

### 2. PipelineEvent.Heartbeat sentinel

Add a new `PipelineEvent` case:
```csharp
public sealed record Heartbeat : PipelineEvent;
```

This is a singleton sentinel. The gRPC mapping layer (WeatherGrpcService) already filters events by type -- Heartbeat will not match any domain data or enrichment filter and will be naturally excluded. For safety, add an explicit `Where(e => e is not PipelineEvent.Heartbeat)` before the SourceRef materialization if the mapping layer doesn't fully exclude it.

### 3. KeepAlive placement: after Concat, before Buffer

The per-client stream in `HandleStreamRequest` becomes:
```csharp
Source.From(snapshot)
    .Concat(_clientHubSource)
    .KeepAlive(keepAliveInterval, () => PipelineEvent.Heartbeat.Instance)
    .Buffer(StreamClientBufferSize, OverflowStrategy.Backpressure)
    .IdleTimeout(idleTimeout)
    .Where(e => e is not PipelineEvent.Heartbeat)
    .RunWith(StreamRefs.SourceRef<PipelineEvent>(), _mat)
```

**Why this order:**
- `KeepAlive` before `Buffer`: heartbeats enter the buffer, keeping the stream "active" for IdleTimeout
- `Buffer(Backpressure)` before `IdleTimeout`: a slow client fills the buffer, backpressure propagates upstream, eventually the hub can't push, KeepAlive can't emit heartbeats, and IdleTimeout fires
- `IdleTimeout` before `Where`: the timeout measures all elements including heartbeats. If heartbeats flow, the client is alive. If nothing flows (dead client), timeout fires
- `Where` at the end: filter heartbeats before the SourceRef so they never reach the gRPC wire

### 4. Interval values

- `keepAliveInterval`: Must be shorter than `idleTimeout` but long enough to not spam. A sensible default: **30 seconds**. The poll interval is 60 minutes, so heartbeats are rare.
- `idleTimeout`: Must be longer than `keepAliveInterval`. Default: **90 seconds** (3x keepalive). This gives 3 heartbeat chances before timeout.

These should be constants in `GrpcConsumer`, not configurable options (YAGNI -- the values are implementation-internal, not operator-facing).

### 5. Buffer strategy change: DropHead to Backpressure

Currently `Buffer(256, DropHead)` silently drops older events when a client is slow. With `Backpressure`, slow clients propagate backpressure to the BroadcastHub connection. Combined with `IdleTimeout`, a truly dead client will eventually be torn down because backpressure stops all flow (including heartbeats) and the timeout fires.

**Risk:** A very slow but alive client could fill the 256-element buffer during a burst. With Backpressure, this temporarily blocks the hub for that client's slot. The BroadcastHub handles this per-consumer -- other consumers are unaffected. The slow client catches up and the buffer drains.

### 6. Handling IdleTimeout failure

When `IdleTimeout` fires, it throws `TimeoutException` into the stream. The SourceRef consumer (ha-njord) receives a gRPC error. The GrpcConsumer actor is unaffected because the per-client stream is materialized independently. No special error handling needed in the actor -- the stream self-destructs.

## Risks / Trade-offs

- **[Heartbeat serialization overhead]** Heartbeat events go through the BroadcastHub, Buffer, and IdleTimeout stages. At one heartbeat per 30s per client, this is negligible.
- **[Backpressure Buffer vs DropHead]** A slow client now backpressures its hub slot instead of silently losing data. This is the intended behavior -- data integrity over throughput. The IdleTimeout tears down truly stuck clients.
- **[WireTap drops replies under pressure]** If the reply sink ever backpressures, WireTap drops reply copies. Since the reply sink is fire-and-forget Tell, this cannot happen. If it could, the PollWorker would time out and retry.

## Open Questions

(none)
