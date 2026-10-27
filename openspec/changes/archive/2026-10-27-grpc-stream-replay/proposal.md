## Why

gRPC server-streaming RPCs (`StreamDomainData`, `StreamEnrichments`) deliver no data to
clients that connect after the most recent poll cycle. The `BroadcastHub` in `GrpcConsumer`
is live-forward-only: it pushes events to subscribers connected at the time of emission and
does not replay past state. In practice this means every new subscriber waits up to 60 minutes
(the poll interval) for its first event. The unary RPCs serve the same data instantly from
the cached `GrpcConsumerState`, but the stream path skips this cache entirely.

## What Changes

- **Replay cached state on stream subscribe.** When `GrpcConsumer` handles a
  `RequestGrpcStream`, it prepends the current `GrpcConsumerState` snapshot as a
  `Source.From(snapshot)` before the live `BroadcastHub` source via `Concat`. New subscribers
  receive all cached domain data and feature results immediately, then transition seamlessly
  to live events.
- No new messages, no new actors, no protocol changes. The downstream filters in
  `WeatherGrpcService` already separate domain data from enrichments, so the replay burst is
  filtered identically to live events.

## Non-goals

- **Deduplication.** A subscriber may briefly see the same event from the snapshot and from the
  hub if a poll completes during materialization. gRPC consumers use overwrite semantics, so
  duplicates are harmless.
- **BroadcastHub replacement.** No custom replay hub or GraphStage. The existing hub plus a
  prepended source is sufficient.
- **ha-njord changes.** The integration already handles repeated state updates correctly.

## Capabilities

### New Capabilities

- `grpc-stream-replay`: GrpcConsumer prepends a snapshot of cached PipelineEvents to the
  live BroadcastHub source when creating a SourceRef for a gRPC stream subscriber.

### Modified Capabilities

_(none -- no spec-level requirement changes to existing capabilities)_

## Impact

- **Code:** `GrpcConsumer.HandleStreamRequest` (~6 lines changed). No other production code.
- **Tests:** New test verifying that a stream subscriber receives cached state before live
  events. Existing tests unaffected (replay is additive).
- **API budget:** Zero impact -- no additional polling or API calls.
- **Wire protocol:** Unchanged. Clients receive the same `PipelineEvent` types; only the timing
  of the first events changes.
