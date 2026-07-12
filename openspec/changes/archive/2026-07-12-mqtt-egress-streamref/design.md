## Context

After the stream-pipeline-refactoring, the system has a flat pipeline graph (MergeHub → Expand → Throttle → Fetch → PublishStage) hosted in a `PipelineHostedService` with manual KillSwitch management. The `PublishStage` calls `IMqttPublisher.PublishAsync` directly, while the `MqttConnectionActor` also calls the same interface for discovery/availability/tombstoning. This creates shared-connection ownership, mixed-responsibility interfaces, and non-actor lifecycle for the pipeline.

The architectural guardrail states: "Streams for data flow, actors for lifecycle." Both the pipeline and the egress are long-lived data flows that should be materialized by actors for clean lifecycle. The connection between them should be a typed stream bridge (StreamRef), not shared interface access.

## Goals / Non-Goals

**Goals:**
- Single publish path: all MQTT messages flow through one MergeHub → one Sink
- Actor-bound materialization for both pipeline and egress graphs
- Clean failure-domain separation: pipeline crash ≠ egress crash
- StreamRef as typed bridge between the two actor-owned graphs
- Interface split: connection-management vs. transport separated by responsibility

**Non-Goals:**
- Remote StreamRefs (cluster/network) — local only
- MQTT command source integration (Phase 2 enablement only)
- Changing discovery/state payload builders or topic scheme
- Consensus computation

## Decisions

### 1. Two actors, two graphs, StreamRef bridge

**Choice:** `MqttEgressActor` materializes the egress graph (MergeHub → Publish Sink) and exposes a `SinkRef<MqttMessage>`. `PipelineActor` materializes the pipeline graph and connects its output to the SinkRef.

**Why over a single actor:**
- Independent failure domains — a fetch exception can't kill the broker connection
- Each actor's `Context.Materializer()` scopes the stream lifecycle to that actor
- StreamRef is a typed, backpressure-aware bridge — cleaner than shared interface access
- Enables future architecture where pipeline and egress scale independently

**Why over IHostedService + Actor:**
- Actor-bound materialization provides lifecycle management without KillSwitch
- Consistent model (both are actors) simplifies supervision and startup ordering
- No mixed paradigms (DI-managed service lifetime vs. actor lifetime)

### 2. MergeHub<MqttMessage> as the single egress funnel

**Choice:** All MQTT publishes (state, discovery, availability, tombstone) converge in one `MergeHub<MqttMessage>`. The egress actor feeds internal sources (discovery, availability, tombstone) via `Source.Queue`. The pipeline feeds via StreamRef.

**Why over direct PublishAsync calls:**
- Single backpressure-aware publish path — broker saturation propagates consistently
- All messages are equal from the sink's perspective — content-agnostic publish
- Easy to add monitoring/wiretap on the single funnel
- Conflation/buffering strategies apply uniformly

### 3. Source.Queue for actor-internal sources

**Choice:** The egress actor creates `Source.Queue<MqttMessage>` instances for discovery, availability, and tombstone messages. On events (Connected, HA Birth, Stale Config), the actor offers messages into the appropriate queue.

**Why over Source.ActorRef:**
- Explicit capacity and overflow strategy per queue
- Actor controls when messages enter — no external Tell needed
- DropHead overflow for discovery is safe (stale discovery will be re-sent on next trigger)

### 4. Pipeline obtains SinkRef via Ask with Stash

**Choice:** `PipelineActor` sends a `RequestEgressSink` message to the egress actor in `PreStart`, stashes incoming commands until the SinkRef arrives, then materializes the full pipeline graph.

**Why over ActorRegistry lookup:**
- The SinkRef is only valid while the egress actor's graph is materialized — it can't be statically registered
- Ask + Stash gives natural backpressure: pipeline doesn't start until egress is ready
- If egress restarts, pipeline can watch it and re-request

### 5. Interface split: IMqttConnection + raw transport in sink

**Choice:** `IMqttConnection` covers connect, subscribe, and disconnect callbacks (used only by the egress actor). The publish sink calls `MqttClient.PublishAsync` directly via a thin `IMqttTransport` interface (single method: `SendAsync`).

**Why split:**
- Connection callbacks (onMessage, onDisconnected) are actor-specific — the sink doesn't need them
- `IMqttTransport.SendAsync` is trivially mockable for sink tests
- Clean SRP: connection management vs. message delivery

### 6. Egress actor watches for disconnect and buffers

**Choice:** On disconnect, the egress actor keeps the MergeHub running but the Publish Sink pauses (backpressure or bounded buffer). On reconnect, buffered messages drain. Buffer overflow strategy: DropHead (latest-wins semantics for retained state).

**Why not drop all on disconnect:**
- Discovery/availability messages sent on reconnect are important
- State messages are "latest wins" due to retain — losing old ones is acceptable
- Bounded buffer (e.g., 64 messages) prevents memory growth during extended outages

## Risks / Trade-offs

**[StreamRef invalidation on egress restart]** → If the egress actor restarts, the SinkRef becomes invalid and the pipeline's stream completes. Mitigation: PipelineActor watches the egress actor; on `Terminated`, re-requests a new SinkRef and rematerializes the pipeline graph.

**[Startup ordering]** → Pipeline depends on egress being ready. Mitigation: Ask + Stash pattern; pipeline doesn't materialize until SinkRef is obtained. Akka.Hosting registration order ensures egress actor is created first.

**[Buffer overflow during extended outage]** → If broker is down for hours, buffer fills. Mitigation: DropHead strategy means only newest messages survive — acceptable since state is "latest wins" and discovery is re-sent on reconnect anyway.

**[StreamRef overhead]** → StreamRef adds a small per-element overhead vs. direct call. Mitigation: negligible for the ~24 messages per poll cycle (60-minute interval). Not a hot path.

## Open Questions

- Should the egress actor expose a single SinkRef or one per producer? A single SinkRef shared across all external producers (just the pipeline for now) is simpler.
- Buffer size: 64 messages covers 2-3 full poll cycles — enough for short outages. Make configurable?
