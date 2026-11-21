## Context

The gRPC Subscribe stream uses a SourceRef to hand entity events from the GrpcConsumer actor to the NjordGrpcService handler. A KeepAlive stage injects heartbeats during idle periods, and an IdleTimeout tears down dead streams. Both stages are currently on the producer side (before the SourceRef), following a misreading of the reference pattern from Aaronontheweb/akka-dynamic-hub-keepalive.

Decompiled Akka.NET 1.5.75 source shows `Idle<T>.OnPush()` is the only method that resets the timeout deadline. `OnPush()` fires only when the downstream SourceRef pulls. During transient demand stalls in the SourceRef transport protocol, heartbeats accumulate in the Buffer but never "pass through" IdleTimeout, causing 15 spurious TimeoutExceptions in a 48h soak test.

The reference pattern places IdleTimeout on the consumer side, measuring wire liveness. A consumer-side Buffer between the filter and the gRPC write keeps demand flowing through IdleTimeout even when WriteAsync stalls.

## Goals / Non-Goals

**Goals:**
- Eliminate spurious SourceRef idle timeouts at production polling intervals (1h+)
- Match the reference pattern: KeepAlive on producer, IdleTimeout on consumer
- Prove the fix with stream topology tests before changing production code

**Non-Goals:**
- Changing timeout values (30s keepalive / 90s idle timeout are correct)
- Replacing SourceRef with Channel/IAsyncEnumerable (future consideration)
- Adding keepalive to the Pipeline-to-GrpcConsumer SourceRef (reconnects gracefully)

## Decisions

### Decision 1: Move IdleTimeout to consumer side of SourceRef

**Choice:** Place IdleTimeout immediately after `sourceRef.Source` in `NjordGrpcService.Subscribe`, remove it from `GrpcConsumer.HandleEntityStreamRequest`.

**Why:** IdleTimeout on the consumer side measures whether elements arrive across the SourceRef, which is the actual liveness signal. On the producer side it measures whether the SourceRef pulls, which conflates transport-layer demand management with stream health.

**Alternative considered:** Remove IdleTimeout entirely, rely on gRPC CancellationToken + KillSwitch for disconnect detection. Rejected because IdleTimeout provides defense-in-depth against silent producer failures (e.g., GrpcConsumer actor restart losing the BroadcastHub).

### Decision 2: Remove producer-side Buffer(256, Backpressure)

**Choice:** Remove the Buffer stage from the producer pipeline. The producer becomes: `Source.From(initial).Concat(hubSource).KeepAlive(30s).RunWith(SourceRef)`.

**Why:** The Buffer was placed between KeepAlive and IdleTimeout to absorb bursts. With IdleTimeout gone from the producer, the Buffer serves no purpose. The SourceRef has its own internal buffer (32 elements default). Burst absorption moves to the consumer-side Buffer.

### Decision 3: Add consumer-side Buffer(128, Backpressure) after heartbeat filter

**Choice:** Place `Buffer(128, Backpressure)` between `.Where(not ConsumerKeepAlive)` and `.Select(ToProto)` in the consumer pipeline.

**Why:** This is the core insight from the reference pattern. The Buffer pre-fetches elements, keeping demand flowing through IdleTimeout even when the gRPC WriteAsync temporarily stalls. Without it, a slow gRPC write could stop pulling through IdleTimeout, causing the same false-timeout problem on the consumer side.

**Buffer size 128 (not 256):** The consumer-side buffer only needs to absorb gRPC write stalls (milliseconds). 128 elements is generous. The old producer-side 256 was oversized for the same reason.

### Decision 4: IdleTimeout before Where(not ConsumerKeepAlive)

**Choice:** Stream order is `sourceRef.Source.IdleTimeout(90s).Where(not KA).Buffer(128)...`

**Why:** Heartbeats must pass through IdleTimeout to reset its deadline. If Where filters them first, IdleTimeout only sees real entity events, which during 1h+ polling gaps means nothing -- and the timeout fires. This ordering matches the reference pattern exactly.

### Decision 5: Topology proof tests with short intervals

**Choice:** New `SourceRefIdleTimeoutTests` class building standalone stream graphs with KeepAlive(200ms) and IdleTimeout(1s). These test the stream mechanics, not the production code.

**Why:** Testing the actual 30s/90s values would require 90+ second test runs. The topology behavior is interval-independent -- the demand/push mechanics are the same at any timescale. Short intervals give fast, deterministic proofs.

## Risks / Trade-offs

- **[Consumer-side Buffer adds memory per subscriber]** Each Subscribe call allocates a 128-element buffer. With the current single-location, single-subscriber setup this is negligible. At scale (many locations, many ha-njord instances), monitor memory. Mitigation: 128 is conservative; can be tuned down.

- **[IdleTimeout on consumer side triggers on producer issues, not client issues]** The old producer-side IdleTimeout was intended to detect dead clients. The new consumer-side IdleTimeout detects dead producers. Dead clients are still detected by the gRPC CancellationToken + KillSwitch. Mitigation: this is actually more correct -- the SourceRef should not be responsible for detecting its own consumer's death.

- **[Transient SourceRef failures still cause reconnects]** Moving the timeout doesn't eliminate all SourceRef issues. If the SourceRef transport has a prolonged failure (not just a demand stall), the consumer-side timeout will correctly fire. Mitigation: ha-njord already handles reconnects gracefully.

## Open Questions

None -- the design is confirmed by the decompiled Akka.NET source and matches the reference pattern.
