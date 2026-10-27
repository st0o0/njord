## Context

`GrpcConsumer` is a Cluster Singleton that bridges the `ForecastPipeline`'s
`BroadcastHub` to gRPC stream clients. It maintains two data paths:

1. **Unary RPCs** (`GetDomainData`, `GetEnrichments`) read from `_state`
   (`GrpcConsumerState`), an immutable cache updated on every `PipelineEvent`.
   These always return the latest data instantly.

2. **Streaming RPCs** (`StreamDomainData`, `StreamEnrichments`) get a `SourceRef`
   from `HandleStreamRequest`, which taps into the client-facing `BroadcastHub`.
   This hub is live-forward-only: subscribers receive only events emitted after
   they connect.

The gap: a stream subscriber that connects after the most recent poll cycle
receives nothing until the next poll (up to 60 minutes). The cached state
that unary RPCs serve is not used for streams at all.

## Goals / Non-Goals

**Goals:**

- New stream subscribers receive all cached domain data and feature results
  immediately upon connecting.
- The transition from replayed state to live events is seamless (no gap, no
  coordination message).

**Non-Goals:**

- Deduplication of replay vs live events (overwrite semantics make this harmless).
- Custom Akka.Streams stages or a replay-capable BroadcastHub.
- Changes to `ForecastPipeline`, `WeatherGrpcService`, messages, or proto.
- Guaranteed exactly-once delivery (streams are best-effort by design).

## Decisions

### D1: Replay via `Source.From(snapshot).Concat(liveHub)`

Prepend the cached state as a finite source before the infinite BroadcastHub
source using `Concat`. This is the standard Akka.Streams composition for
"initial state + live tail".

**Alternatives considered:**

- *BroadcastHub with built-in replay:* Akka.NET has no such primitive. A custom
  `GraphStage` would be needed, adding maintenance burden for no benefit since
  the cache already exists in actor state.
- *Replay at WeatherGrpcService level:* Would require two actor asks per stream
  setup (unary + stream), duplicate filtering logic, and mix adapter concerns
  with data replay.

**Rationale:** The actor already has the data (`_state`), the method already
builds the source graph, and the downstream filters already handle mixed event
types. One composition operator (`Concat`) is all that's needed.

### D2: Replay all cached events (unfiltered)

The snapshot includes both `DomainData.Values` and `Results.Values` regardless
of stream type. `WeatherGrpcService` already filters:

- `StreamDomainData`: `Where(e => e.ToDomainKind() is { } kind && ...)`
- `StreamEnrichments`: `Where(e => e.ToEnrichmentLocation() is { } ...)`

These filters apply identically to replayed and live events. No type-specific
replay logic needed.

**Data volume:** For 1 location with all 37 features and 6 domains: ~6 domain
events + ~37 feature results = ~43 events. Negligible.

### D3: Snapshot captured inside actor receive (no race)

`HandleStreamRequest` runs in the actor's single-threaded receive loop.
Reading `_state` is safe. The `Source.From(snapshot)` captures an
`IEnumerable<PipelineEvent>` eagerly (the ImmutableDictionary values).
No concurrent mutation is possible between snapshot and materialization.

## Risks / Trade-offs

**[Brief duplicate window]** If a poll completes between snapshot capture and
BroadcastHub subscription, the subscriber may see the same event twice (once
from snapshot, once from hub). **Mitigation:** gRPC consumers use overwrite
semantics (ha-njord replaces entity state). Duplicates are harmless.

**[Empty replay at startup]** If `GrpcConsumer` hasn't received any pipeline
events yet, the snapshot is empty. The stream degrades to today's behavior
(live-only). **Mitigation:** This is correct -- there's nothing to replay.

## Open Questions

None.
