## Why

The KeepAlive + IdleTimeout stages added in commit 732b83b6 to prevent SourceRef idle timeouts on the gRPC Subscribe stream are placed on the wrong side of the SourceRef. The 48h realcycle soak test shows 15 `TimeoutException("No elements passed in the last 00:01:30")` failures, each causing a ha-njord reconnect with a brief entity availability gap.

Decompiled Akka.NET source confirms: `Idle<T>.OnPush()` is the only place that resets the deadline, and it fires only when the downstream SourceRef actually pulls. When the SourceRef transport has a transient demand stall, heartbeats accumulate in the Buffer unseen by IdleTimeout, which fires after 90s. The reference pattern (Aaronontheweb/akka-dynamic-hub-keepalive backpressure-sample) places IdleTimeout on the consumer side of the SourceRef, where it measures wire liveness, not producer-side demand.

## What Changes

- Move `IdleTimeout(90s)` from the producer pipeline in `GrpcConsumer.HandleEntityStreamRequest` to the consumer pipeline in `NjordGrpcService.Subscribe`
- Remove `Buffer(256, Backpressure)` from the producer pipeline (no longer needed without IdleTimeout)
- Add `Buffer(128, Backpressure)` on the consumer pipeline after the KeepAlive filter, to decouple IdleTimeout from gRPC write backpressure
- Reorder consumer pipeline: IdleTimeout before `Where(not ConsumerKeepAlive)` so heartbeats reset the timer before being filtered

## Capabilities

### New Capabilities

- `sourceref-idle-timeout-topology`: Stream topology tests proving that producer-side IdleTimeout fires under demand stall and consumer-side IdleTimeout survives upstream silence

### Modified Capabilities

- `grpc-client-liveness`: IdleTimeout moves from producer to consumer side of the SourceRef; Buffer moves accordingly; the requirement changes from "elements pass through on producer side" to "elements arrive on consumer side"

## Non-goals

- Changing KeepAlive interval (30s) or IdleTimeout duration (90s) -- the values are fine, the placement was wrong
- Replacing SourceRef with Channel or IAsyncEnumerable -- possible future improvement but out of scope
- Adding keepalive to the Pipeline-to-GrpcConsumer SourceRef (SourceRef #1) -- that stream reconnects gracefully via StreamCompleted handler
- API budget impact: none (no polling changes)

## Impact

- `src/Njord.Grpc/GrpcConsumer.cs` -- simplify producer pipeline (remove Buffer + IdleTimeout)
- `src/Njord.Grpc/NjordGrpcService.cs` -- add IdleTimeout + Buffer on consumer pipeline
- `src/Njord.Grpc.Tests/SourceRefIdleTimeoutTests.cs` -- new topology proof tests
- `src/Njord.Grpc.Tests/GrpcConsumerStreamTests.cs` -- add keepalive delivery regression test
- Existing tests unchanged
