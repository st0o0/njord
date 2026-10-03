## Context

The njord egress layer currently has three independent paths from data producers to MQTT:

1. **MqttPublisherActor** subscribes directly to the Pipeline BroadcastHub, transforms `FetchOutcome.Success` into `MqttMessage`, and sends to `MqttConnectionActor`'s MergeHub.
2. **EnrichmentActor** requests an `ISinkRef<MqttMessage>` from `MqttConnectionActor` and sends `MqttMessage` directly from its 7 sub-graphs (consensus, alerts, derived, trends, indices, energy, history).
3. **EgressActor** accepts `RegisterPublisher` and forwards `PublishStateResult` via Tell — but the EnrichmentActor bypasses it entirely, making it effectively dead code.

This means `EnrichmentActor` has a hard dependency on `Njord.Mqtt` (imports `MqttMessage`, `StatePayloadBuilder`, `TopicScheme`), making it impossible to add a second output protocol without duplicating enrichment logic.

The `EgressActor` was intended as the boundary between domain and output but never became one. This design corrects that.

## Goals / Non-Goals

**Goals:**
- Single protocol-neutral boundary (`EgressActor` with MergeHub/BroadcastHub) between all data producers and all output consumers
- `EnrichmentActor` has zero knowledge of output protocols
- `ModelStateActor` (renamed `MqttPublisherActor`) has zero knowledge of output protocols
- Adding a new output protocol (SignalR, gRPC) requires only a new consumer actor — no changes to producers
- MQTT wire format (topics, payloads, discovery) is unchanged

**Non-Goals:**
- Implementing SignalR or any second consumer
- Changing enrichment computation logic
- Modifying the ingest pipeline or scheduler
- Reworking the MqttConnectionActor's internal MergeHub (it stays as-is, receiving `MqttMessage` from `MqttEgressActor` and `DiscoveryActor`)

## Decisions

### 1. EgressEvent as abstract record with sealed variants

**Choice:** `EgressEvent` is an abstract record with 8 sealed record variants, each wrapping a domain result type plus location context.

**Why not a single envelope:** A generic `EgressEvent<T>` would lose exhaustive pattern matching. Discriminated variants let `MqttEgressActor` do `switch` with compiler-enforced coverage.

**Why not keep `PublishStateResult(object Result)`:** The current `object` typing provides no compile-time safety and forces `is`/`as` casting. Sealed variants make the type system enforce that all categories are handled.

### 2. MergeHub + BroadcastHub in EgressActor (mirroring PipelineActor)

**Choice:** Same Akka.Streams hub pattern as `PipelineActor` — pre-materialized MergeHub and BroadcastHub, StreamRefs vended on request.

**Why not actor-based Tell/Ask:** Tell provides no backpressure. If a consumer (e.g. a slow SignalR push) falls behind, messages pile up in the mailbox unbounded. BroadcastHub gives each consumer independent backpressure.

**Why not Akka EventStream or DistributedPubSub:** These are fire-and-forget. We need reliable delivery with backpressure per consumer.

**Buffer sizes:** MergeHub per-producer buffer = 8 (matches PipelineActor), BroadcastHub buffer = 64 (matches EnrichmentActor's current snapshot hub). These are small enough to bound memory but large enough to absorb burst from 8 models × 7 enrichment types arriving near-simultaneously.

### 3. Dedup moves to protocol consumers, not producers

**Choice:** Enrichment sub-graphs no longer maintain `Dictionary<string, string> lastPublished`. Dedup is the responsibility of each protocol consumer (e.g. `MqttEgressActor`).

**Why:** Different protocols may have different dedup needs. MQTT deduplicates by topic+payload (retained messages). SignalR might want to always push (the UI shows "last updated" timestamps). Pushing dedup into producers would force all consumers into MQTT's semantics.

**Trade-off:** Slightly more data through the EgressActor hub when values haven't changed. Acceptable because: (a) the data volume is small (max ~60 events per poll cycle), (b) BroadcastHub handles this efficiently in-process, (c) dedup in producers was duplicated 8× already.

### 4. ModelStateActor keeps BuildPerHorizon logic

**Choice:** `ModelStateActor` still calls `StatePayloadBuilder.BuildPerHorizon()` to compute the per-horizon JSON payloads, and carries them in `PerModelUpdate.HorizonPayloads`.

**Alternative considered:** Have `PerModelUpdate` carry the raw `ModelForecast` and let each consumer compute horizons. Rejected because horizon computation is domain logic (which horizons, which parameters, how to format values), not protocol logic. Every consumer would need the same computation.

**Note:** `StatePayloadBuilder.BuildPerHorizon` stays in its current location but is conceptually a domain concern. `StatePayloadBuilder.From*` methods (consensus→JSON, alerts→JSON) are protocol-specific and move into `MqttEgressActor`.

### 5. MqttEgressActor is a stream-only actor

**Choice:** `MqttEgressActor` is a `ReceiveActor` that materializes a single stream graph: `EgressSourceRef.Source → SelectMany(mapToMqttMessages) → MqttSinkRef.Sink`. No actor message processing in steady state.

**Why:** The previous `MqttPublisherActor` mixed actor messages (`PublishStateResult`) with a stream graph (Pipeline consumer). This dual-path was confusing. A pure stream graph is simpler and provides end-to-end backpressure.

### 6. PerModelUpdate dedup stays in ModelStateActor

**Choice:** Unlike enrichment events (decision 3), per-model dedup stays in the producer. `ModelStateActor` skips emitting `PerModelUpdate` when horizon payloads haven't changed.

**Why:** Per-model updates are high-frequency (every poll cycle per model) and the horizon payload computation is the expensive part. Skipping early avoids both the EgressEvent allocation and the BroadcastHub transit. Enrichment events are lower-frequency and the dedup key (topic string) is protocol-specific, so those belong downstream.

## Risks / Trade-offs

- **[Risk] Stream graph complexity** — EgressActor adds a second hub layer (Pipeline hub → Egress hub → MQTT hub = 3 hops). → Mitigation: All in-process, no serialization. Latency is negligible. The alternative (no hub) means every new consumer needs direct access to every producer.

- **[Risk] Enrichment dedup removal increases MQTT publishes** — Without enrichment-side dedup, `MqttEgressActor` sees events even when values haven't changed. → Mitigation: `MqttEgressActor` maintains its own dedup dictionary (same `Dictionary<string, string>` pattern). Net publish count to the broker is unchanged.

- **[Risk] PerModelUpdate carries pre-formatted JSON strings** — The `HorizonPayloads` dictionary contains JSON strings, which is opaque to non-MQTT consumers. → Mitigation: This is the same data that `BuildPerHorizon` currently produces. A future SignalR consumer could either parse it or we add a richer domain representation later. Pragmatic for now.

## Open Questions

None — all decisions were explored in the preceding discovery session.
