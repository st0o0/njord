## Context

The pipeline currently wires three actors (SchedulerActor, PipelineActor,
MqttEgressActor) through ad-hoc mechanisms: a raw `ISourceQueueWithComplete`
handle leaked across actor boundaries, a side-effecting `.Select()` that
fire-and-forget offers into a second queue for egress, and a two-step startup
handshake with stashing. These patterns break Akka's encapsulation model and
prevent proper backpressure propagation.

Akka.Streams provides first-class primitives for exactly this situation:
`StreamRefs` (SinkRef/SourceRef) for cross-actor stream connections with
backpressure, `MergeHub` for dynamic fan-in, and `BroadcastHub` for dynamic
fan-out. All three are already in the `Akka.Streams` package — no new
dependencies.

## Goals / Non-Goals

**Goals:**
- Replace raw queue-handle passing with typed StreamRefs (SinkRef/SourceRef)
  so actors communicate only through backpressure-aware, encapsulated stream
  endpoints.
- Replace the side-effecting `.Select()` egress publish with a BroadcastHub
  that cleanly separates the egress and feedback consumer paths.
- Move delta-publishing state from the PipelineActor closure to the
  EgressActor consumer graph where ownership is natural.
- Simplify the startup handshake from two sequential request/response pairs
  to a single coordination pattern per actor pair.

**Non-Goals:**
- Changing poll scheduling logic, adaptive timing, or persistence.
- Changing MQTT topic scheme, payload format, or discovery/availability flows.
- Adding new BroadcastHub consumers (metrics, consensus) — the hub enables
  this but we don't add any now.
- Modifying the EgressActor's internal MergeHub (discovery/availability/tombstone
  queues) — that stays as-is.

## Decisions

### D1: MergeHub as pipeline entry (not Source.Queue directly)

The PipelineActor materializes a `MergeHub.Source<WeightedTarget>` as the
pipeline entry point. This means any number of producers can connect via
SinkRef — today that's only the SchedulerActor, but the hub accommodates
future producers (manual refresh API, test harness) without graph changes.

Alternative considered: keeping `Source.Queue` in the PipelineActor and vending
a SinkRef to it. This works but limits the pipeline to a single producer and
doesn't leverage the hub's built-in fan-in and per-producer buffering.

### D2: SchedulerActor owns a local Source.Queue connected via SinkRef

The SchedulerActor materializes its own `Source.Queue<WeightedTarget>` and
connects it to the PipelineActor's SinkRef via `.To(sinkRef.Sink).Run(mat)`.
The `OfferAsync` calls stay in the SchedulerActor but now operate on the
actor's own queue — no foreign handle.

The queue capacity is 32 with `OverflowStrategy.Backpressure`. This propagates
backpressure from the pipeline throttle back to the scheduler's offer calls.
`OnRefreshLocation` (which fans out to N models) will naturally slow down
if the pipeline is saturated — this is the desired behavior, not the current
fire-and-forget semantics.

### D3: BroadcastHub after the filter stage

The pipeline flow ends with `BroadcastHub.Sink<FetchOutcome.Success>` after
the filter stage (Collect + Where). The hub broadcasts raw successful fetch
outcomes — each consumer transforms them for its own purpose:

- **Egress consumer** (materialized by EgressActor via SourceRef):
  `Select(BuildPerHorizon) → Select(DeltaFilter) → SelectMany(→ MqttMessage)
  → into MergeHub`
- **Feedback consumer** (materialized locally by PipelineActor):
  `Select(ComputeHash) → Ask<Ack>(scheduler) → Sink.Ignore`

Alternative considered: broadcasting a richer intermediate type (payload + hash).
Rejected because it pre-computes work that one consumer doesn't need (egress
doesn't need the hash, feedback doesn't need the payloads).

### D4: Feedback consumer lives in PipelineActor

The hash computation + Ask feedback consumer is materialized by the
PipelineActor itself, not by the SchedulerActor. Rationale:

- The PipelineActor already has the `IActorRef` to the SchedulerActor
  (looked up via registry).
- The Ask provides backpressure within the PipelineActor's materialized graph —
  if the scheduler is slow to Ack, the BroadcastHub's slowest-consumer
  semantics propagate that pressure back through the pipeline.
- The SchedulerActor stays a pure actor (timers + persistence) with no
  stream materialization concerns beyond its own local queue.

### D5: EgressActor pulls via SourceRef (not push via SinkRef)

The current flow is push: PipelineActor gets a SinkRef from EgressActor and
pushes into it. The new flow inverts this: PipelineActor vends a SourceRef
from the BroadcastHub, and the EgressActor pulls from it.

This eliminates the current `RequestEgressSink`/`EgressSinkResponse` handshake.
Instead, the EgressActor sends `RequestPipelineSource` and receives a
`PipelineSourceResponse(SourceRef<FetchOutcome.Success>)`. The EgressActor
connects the SourceRef to its consumer graph and drains into its existing
MergeHub.

### D6: Delta-publishing state moves to EgressActor

The `Dictionary<(string, string, string), string>` (key: location/model/horizon,
value: last payload) moves into the EgressActor's consumer graph as a closure
variable or a `Scan` stage accumulator. This is natural: egress owns what was
last published. The PipelineActor no longer needs `ConcurrentDictionary` (it
was single-threaded anyway).

### D7: Startup sequence

1. **MqttEgressActor.PreStart**: materializes its internal MergeHub
   (discovery/availability/tombstone queues + transport sink). Transitions to
   `Ready`.
2. **PipelineActor.PreStart**: materializes the pipeline graph
   (MergeHub → Throttle → Fetch → Filter → BroadcastHub). Materializes the
   local feedback consumer. Transitions to `Ready`, accepts
   `RequestPipelineSink` (for scheduler) and `RequestPipelineSource`
   (for egress).
3. **SchedulerActor.PreStart**: sends `RequestPipelineSink` to PipelineActor.
   On receipt of SinkRef, materializes local queue → SinkRef connection.
   Initializes poll states and starts timers.
4. **MqttEgressActor** (once pipeline is ready): sends `RequestPipelineSource`
   to PipelineActor. On receipt of SourceRef, materializes consumer graph
   (build payloads → delta filter → MergeHub).

Key simplification: PipelineActor no longer waits for the EgressActor before
materializing — it can materialize independently because it no longer needs a
SinkRef from egress. The BroadcastHub buffers until consumers connect.

### D8: Message protocol changes

| Removed | Replacement |
|---------|-------------|
| `RequestPipelineQueue` | `RequestPipelineSink` |
| `PipelineQueueResponse(ISourceQueueWithComplete)` | `PipelineSinkResponse(ISinkRef<WeightedTarget>)` |
| `RequestEgressSink` | `RequestPipelineSource` (sent by EgressActor to PipelineActor) |
| `EgressSinkResponse(ISinkRef<MqttMessage>)` | `PipelineSourceResponse(ISourceRef<FetchOutcome.Success>)` |

The `HashResult` / `Ack` Ask protocol is unchanged.

## Risks / Trade-offs

- **BroadcastHub slowest-consumer backpressure**: The BroadcastHub runs at the
  speed of the slowest consumer. If the Ask feedback path stalls (scheduler
  unresponsive for >5s), it blocks the egress path too. → Mitigation: the Ask
  already has a 5s timeout; on timeout the stream resumes via supervision. The
  hash computation itself is CPU-bound and near-instant.

- **StreamRef lifecycle on actor restart**: If the PipelineActor restarts, all
  vended SinkRefs and SourceRefs become invalid. Consumers (SchedulerActor,
  EgressActor) must detect this and re-request. → Mitigation: both actors
  already watch the PipelineActor and handle `Terminated`. On restart they
  re-request the new refs.

- **MergeHub buffer sizing**: The MergeHub's `perProducerBufferSize` determines
  how many elements can be in-flight per producer before backpressure kicks in.
  Too small → unnecessary backpressure on the scheduler; too large → wasted
  memory. → Start with 16, tune based on observation. With a single producer
  this is not critical.

- **BroadcastHub buffer sizing**: The hub's internal per-consumer buffer
  determines how far consumers can drift apart before the fast one is slowed
  to the slow one. Default (256) is generous for our throughput (max ~8
  elements per poll cycle). Acceptable as-is.

## Open Questions

- Should `OnRefreshLocation` (N offers in a loop) use `await` on `OfferAsync`
  to respect backpressure, or continue fire-and-forget on the local queue?
  Leaning toward await — it's the local queue, backpressure is the point.
