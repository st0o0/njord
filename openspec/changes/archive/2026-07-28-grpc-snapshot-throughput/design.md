## Context

The `GrpcSnapshotConsumerActor` subscribes to the EgressActor's BroadcastHub and forwards every `EgressEvent` to `ForecastSnapshotActor` and `EnrichmentSnapshotActor` via Ask. With 28 model-location pairs and 7 enrichment features × 3 locations, each poll cycle produces ~49 events. These are serialized through `SelectAsync(1)` — each Ask requires a full Akka mailbox round-trip before the next event is processed.

Meanwhile, gRPC `GetForecast` requests also use Ask to the same `ForecastSnapshotActor`. A read arriving mid-cycle queues behind all pending writes in the actor's FIFO mailbox, causing 5-second AskTimeoutExceptions.

The snapshot actors' update handlers are synchronous and fast (~µs). The bottleneck is the 49 sequential Ask round-trips in the stream, not the actor processing time.

## Goals / Non-Goals

**Goals:**
- Eliminate gRPC timeouts during poll cycles
- Ensure reads are never starved by write batches
- Maintain data consistency — snapshot actors remain the single source of truth

**Non-Goals:**
- Changing the persistence strategy (SaveSnapshot remains)
- Changing the gRPC service layer or Ask timeout
- Memory optimization (separate concern)
- Modifying the EgressActor BroadcastHub topology

## Decisions

### 1. Tell instead of Ask in the consumer stream

**Decision**: Replace `SelectAsync(1, async e => await actor.Ask<Ack>(...))` with `Select(e => { actor.Tell(...); return e; })` in `GrpcSnapshotConsumerActor.MaterializeGraph`.

**Rationale**: The consumer doesn't need acknowledgement — it's a fire-and-forget snapshot cache. The Ack served no purpose beyond backpressure, which is unnecessary here because:
- The snapshot actors process updates in-memory (~µs per message)
- The BroadcastHub already provides backpressure between egress producers and the hub itself
- There's no data loss risk — the next cycle overwrites the snapshot anyway

**Alternatives considered**:
- `SelectAsync(8)` — still Ask-based, reduces latency but doesn't eliminate it. Adds complexity with concurrent Asks to the same actor (messages can reorder).
- `Batch` + bulk Ask — adds complexity, still has the Ask overhead.

### 2. ReadPriorityMailbox for snapshot actors

**Decision**: Create a custom `UnboundedPriorityMailbox` that gives read messages (GetForecast, GetAllForecasts, GetEnrichment, GetAllEnrichments) priority 0 and write messages (UpdateForecast, UpdateEnrichment) priority 1. Configure both snapshot actors to use it via HOCON and `WithActors` props.

**Rationale**: Belt-and-suspenders with the Tell fix. Even with Tell, if the mailbox fills during a burst, reads should still jump the queue. The priority mailbox is a low-cost safety net — it adds no overhead when the mailbox is empty (which is the steady-state case).

**Implementation**: Akka.NET `UnboundedPriorityMailbox` takes a `PriorityGenerator` function in its constructor. We subclass it, configure via HOCON (`akka.actor.deployment./forecast-snapshot.mailbox-type`), and register in `NjordActorSystemSetup`.

**Alternatives considered**:
- `UnboundedStablePriorityMailbox` — preserves FIFO within the same priority. Slightly better ordering guarantees. Will use this variant instead.
- Separate dispatchers for reads vs writes — overkill for this use case.

### 3. Remove Ack message

**Decision**: Remove the `Ack` record from `GrpcMessages.cs` and remove `Sender.Tell(new Ack(), Self)` from both snapshot actor update handlers.

**Rationale**: No consumer needs the Ack anymore. The gRPC service uses Ask for reads (GetForecast/GetEnrichment) which have their own response types. Keeping dead response paths is confusing.

**Pre-check**: Grep for `Ack` usage to confirm no other consumer depends on it.

## Risks / Trade-offs

- **[Write ordering with Tell]** — With Tell, the stream no longer waits for each write to complete before processing the next. Since the snapshot actor is single-threaded and Akka guarantees message ordering from a single sender, ordering is preserved. No risk.
- **[Stale reads during burst]** — With priority mailbox, a read during a write burst may return data that doesn't include the writes still queued behind it. This is acceptable — the data is at most one poll cycle stale, and the next cycle overwrites everything.
- **[HOCON config complexity]** — Adding mailbox config adds one HOCON block. Minimal complexity.
