## Context

The current data flow has a JSON roundtrip problem: `ModelStateActor` serializes `ModelForecast` to JSON via `HorizonProjection` for MQTT, then `SnapshotConsumerActor` parses it back for gRPC. The stores are plain `ConcurrentDictionary` wrappers with no supervision, no persistence, and no proper actor semantics. The consumer actors are boilerplate adapters between Akka Streams and DI singletons.

njord already uses Akka Persistence for the `ForecastHistoryActor` (history enrichment). The persistence infrastructure (SQLite/PostgreSQL journal and snapshot store) is configured and working.

## Goals / Non-Goals

**Goals:**
- Eliminate the JSON roundtrip by carrying typed `ModelForecast` in `EgressEvent`.
- Replace `ConcurrentDictionary` stores with proper Akka Persistence actors.
- Move JSON serialization (HorizonProjection + delta-dedup) to MqttEgressActor — the only consumer that needs JSON.
- Clean Ask/Ack pattern for backpressure between consumer and snapshot actors.
- Restart resilience via Akka Persistence snapshots.

**Non-Goals:**
- Event sourcing — we use snapshot-only persistence (no event replay needed for ephemeral forecast data).
- Changing the MQTT wire format.
- Adding new gRPC functionality.

## Decisions

### D1: Snapshot-only persistence (no event journal)

**Decision:** The snapshot actors use `SaveSnapshot`/`LoadSnapshot` only, no `Persist` events. The state is the latest forecast/enrichment data — there's no value in replaying individual updates.

**Rationale:** Forecasts are ephemeral (replaced every 60 minutes). Event-sourcing a stream of forecast updates would grow the journal endlessly with no benefit. A single snapshot per actor captures the complete current state.

**Alternatives considered:**
- Full event sourcing → journal grows unboundedly with no replay benefit.
- No persistence at all → 60-minute data gap after restart. Acceptable but not ideal.

### D2: One ForecastSnapshotActor (not per-model)

**Decision:** A single `ForecastSnapshotActor` holds all (location, model) forecasts. Not one actor per model.

**Rationale:** With 1 location × 8 models, per-model actors are overkill. The state is small (a few KB per model). A single actor simplifies querying (`GetAllForecasts` is a local dictionary read) and reduces actor count. If scale demands it later, cluster sharding can split by location.

### D3: SnapshotConsumerActor uses Ask/Ack for backpressure

**Decision:** The consumer actor sends updates via `Ask` and waits for `Ack` before processing the next event from the stream. This creates backpressure from the snapshot actors back to the BroadcastHub consumer.

**Rationale:** Without Ack, the consumer could flood the snapshot actors' mailboxes during burst updates. Ask/Ack ensures the consumer processes at the rate the snapshot actors can absorb. The latency cost is negligible (in-process actor messaging).

### D4: EgressEvent.PerModelUpdate carries ModelForecast

**Decision:** Replace `IReadOnlyDictionary<string, string> HorizonPayloads` with `ModelForecast Forecast` in `PerModelUpdate`. This is a breaking change to the egress event shape.

**Rationale:** The typed `ModelForecast` is the canonical representation. Each consumer should serialize it in whatever format they need. MqttEgressActor needs JSON (via HorizonProjection). gRPC needs Proto mapping. The snapshot actor stores it as-is. No consumer should receive pre-serialized data they need to deserialize.

### D5: Delta-dedup moves to MqttEgressActor

**Decision:** The per-horizon JSON string comparison for delta publishing moves from `ModelStateActor` to `MqttEgressActor`. It runs after `HorizonProjection` produces JSON strings.

**Rationale:** Delta-dedup is an MQTT optimization (avoid publishing unchanged retained messages). It shouldn't pollute the egress event stream. The gRPC streaming handler pushes every update — the client can dedup if needed. The snapshot actors always overwrite — they want the latest data regardless of whether it changed.

### D6: gRPC service queries via Ask with timeout

**Decision:** `ForecastGrpcService` resolves the snapshot actors from the `ActorRegistry` and uses `Ask<T>` with a 5-second timeout for unary queries.

**Rationale:** Ask is the standard Akka pattern for request/response from outside the actor system. The 5-second timeout is generous for an in-process actor (actual response time is sub-millisecond). On timeout, the gRPC service returns `UNAVAILABLE`.

## Risks / Trade-offs

**[Risk] Persistence snapshot size grows with locations** → With 10 locations × 8 models, the snapshot is ~80 ModelForecasts. Each is a few KB. Total: < 1 MB. Not a concern.

**[Risk] Ask latency in gRPC hot path** → In-process actor Ask is sub-millisecond. For a weather API polled every 60 minutes, this is irrelevant.

**[Trade-off] MqttEgressActor becomes more complex** → It gains HorizonProjection + dedup. But this logic belongs here — MQTT is the consumer that needs JSON. The complexity is moved, not added.

**[Trade-off] Snapshot actors need persistence IDs** → Two new persistent actor IDs (`forecast-snapshot`, `enrichment-snapshot`). These use the existing journal/snapshot infrastructure.

## Open Questions

None.
