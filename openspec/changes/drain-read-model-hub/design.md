## Context

`ForecastReadModel.MaterializeGraph` runs `pipelineSourceRef.Source → killSwitch → Select(UpdateState) → BroadcastHub.Sink(bufferSize: 2)`. The hub's source is only materialized in `HandleStreamRequest`, once per gRPC streaming call, into `StreamRefs.SourceRef<PipelineEvent>()`. The pipeline output hub in `ForecastPipeline` (`bufferSize: 2`) feeds the read model and, when enabled, the MQTT state publisher, each through its own SourceRef.

Two backpressure paths follow from Akka.NET hub semantics:

1. **No subscriber:** a `BroadcastHub` with no attached consumer keeps elements and backpressures its upstream instead of dropping them. After the hub buffer and the in-flight stream-ref demand are used up, `UpdateState` stops running, the read model's SourceRef stops pulling, and the pipeline hub, which advances at its slowest consumer's pace, stops emitting to MQTT too.
2. **Slow subscriber:** a streaming client whose gRPC response stream is slow propagates backpressure through its SourceRef into the secondary hub, and from there along the same path.

## Goals / Non-Goals

**Goals:**
- The read model and the pipeline never wait on gRPC streaming clients.
- A client that keeps up sees exactly the events it sees today.

**Non-Goals:**
- Changing the pipeline output hub or the MQTT publisher's stream.
- Guaranteed delivery to slow streaming clients (they reconcile with the unary RPCs and the next poll cycle, as on reconnect today).

## Decisions

### D1: Permanent `Sink.Ignore` drain on the secondary hub

In `MaterializeGraph`, after pre-materializing the hub, run `hubSource.RunWith(Sink.Ignore<PipelineEvent>(), Mat)` so that the hub always has a consumer. A BroadcastHub only advances at its slowest consumer, and `Sink.Ignore` always signals demand, so the drain alone never slows the hub. The drain is bound to the read model's kill switch implicitly: when the upstream completes or fails (kill switch, dependency loss), the hub completes and the drain completes with it.

*Alternatives:*
- Replace the hub with a `Source.Queue` per client: more code; it duplicates what the hub does.
- Use a larger hub buffer: it only delays the stall.

### D2: Per-client `Buffer(256, OverflowStrategy.DropHead)` before the SourceRef

In `HandleStreamRequest`, materialize `_secondaryHubSource.Buffer(256, OverflowStrategy.DropHead).RunWith(StreamRefs.SourceRef<PipelineEvent>(), Mat)`. The buffer always signals demand to the hub, so a slow client never backpressures it; once the buffer is full the oldest event is dropped. `DropHead` keeps the newest data, which is what a weather client wants after a stall.

**Why 256:** one poll cycle emits at most about 6 events per forecast. For the default config (one location, a handful of models) that is a few dozen events per cycle, so 256 covers several cycles of a briefly stalled client while keeping memory per client small.

*Alternatives:*
- Use `DropTail` or `DropNew`: these would keep stale events and drop fresh ones.
- Fail the client stream on overflow (`OverflowStrategy.Fail`): this forces ha-njord to reconnect. Silently dropping the oldest events is consistent with the client's existing reconcile path, and it avoids reconnect churn.

### D3: Tests drive the read model with a TestKit-owned pipeline

The specs go into `ForecastReadModelSpec`, using the existing test pattern for the pipeline dependency: a test actor registered as `IForecastPipeline` that answers `RequestPipelineOutput` with a SourceRef from a `Source.Queue`-fed hub. The specs check:

- **No streaming client:** offer about 50 events (well above the hub buffer of 2 plus the stream-ref demand) and assert, with a bounded wait, that `QueryLatestAnalysis` reflects the last one, and that a second consumer attached to the same test hub received all of them.
- **Non-pulling client:** request a read-model stream but do not run it, offer 300 events, and assert the same as above. Then run the client's source and assert that it receives at most 256 events and that the last one received is the last one offered.
- **Keeping-up client:** a client that is attached and pulling receives every event, in order.

The first two specs must fail before D1/D2 are in place (TDD). If the no-subscriber spec unexpectedly passes without D1, record the observed Akka.NET behavior in this design before continuing, because D1 rests on it.

## Risks / Trade-offs

- [A slow client silently loses events] → Clients already reconcile via unary queries and the next poll cycle. The loss is limited to the slow client and is preferable to stalling MQTT and every other client.
- [Stream-ref subscription timeout: a SourceRef that the gRPC side never subscribes to fails after the stream-refs subscription timeout] → This is unchanged behavior. With the buffer in front, the hub is not held up during that window.
- [Memory: up to 256 events per attached streaming client] → There are few clients (ha-njord instances), and the events are references shared with the read model state.

## Migration Plan

This is an in-process behavior fix with no config, wire-format or persistence change. Roll back by reverting the commit.
