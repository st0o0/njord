## Context

njord's EgressActor BroadcastHub already pushes all forecast and enrichment events to multiple consumers (MqttEgressActor, SnapshotConsumerActor). Adding gRPC streaming means adding one more consumer to the same hub — architecturally identical to the existing pattern. The `ForecastSnapshotStore` pattern (singleton + BroadcastHub subscription) is proven and will be replicated for enrichment data.

ha-njord (the HACS integration) will use the "Snapshot + Subscribe" pattern: unary RPCs for initial state on connect, then streaming RPCs for ongoing updates. This avoids polling and gives real-time data flow.

## Goals / Non-Goals

**Goals:**

- Expose all forecast and enrichment data via gRPC server-streaming.
- Provide a read-only config service so ha-njord can discover locations, models, and react to config changes.
- Map all 7 enrichment domain types to proto messages with full fidelity.
- Support multiple concurrent gRPC stream consumers.
- Consensus data suitable for ha-njord to create a `weather.njord_{location}_consensus` entity.

**Non-Goals:**

- Config write endpoints (separate change).
- Client-streaming or bidirectional streaming.
- Replacing MQTT egress.
- HA-specific mappings in proto messages.

## Decisions

### D1: Streaming via gRPC handler + Akka Streams, no dedicated actor

**Decision:** The streaming RPCs (`StreamForecasts`, `StreamEnrichments`, `StreamConfig`) are implemented directly in the gRPC service handlers. Each call materializes an Akka Stream from the BroadcastHub's SourceRef into the gRPC `IServerStreamWriter`. No new actors — the gRPC handler IS the stream consumer.

**Rationale:** A dedicated actor per stream would add complexity without benefit. The gRPC handler's lifetime is the stream's lifetime — when the client disconnects, the handler exits and the stream is disposed. Akka Streams handles backpressure. Multiple concurrent clients each get their own handler, each with its own BroadcastHub subscription.

**Alternatives considered:**
- Dedicated StreamingActor that fans out to gRPC writers → unnecessary indirection.
- Channel\<T\> (System.Threading.Channels) instead of Akka Streams → loses integration with existing BroadcastHub.

### D2: EnrichmentSnapshotStore with typed storage

**Decision:** `EnrichmentSnapshotStore` stores the latest result per (location, enrichment type) as the original domain type (`AlertResult`, `IndexResult`, etc.) wrapped in an `EnrichmentSnapshot` record. The gRPC service maps domain types to proto messages on read.

**Rationale:** Storing the domain types preserves full fidelity and avoids double-mapping (domain → snapshot type → proto). The mapping to proto happens once per gRPC read, which is infrequent compared to the update rate.

### D3: Separate proto file for ConfigService

**Decision:** `config_service.proto` is a new file alongside `forecast_service.proto`. The `ConfigService` is a separate gRPC service, not methods on `ForecastService`.

**Rationale:** Config and forecast are different concerns with different consumers. Separating them allows independent evolution. A client that only needs config doesn't have to import forecast message types.

### D4: Enrichment RPCs on ForecastService (not a separate service)

**Decision:** `StreamEnrichments` and `GetEnrichments` are RPCs on `ForecastService`, not a separate `EnrichmentService`. Enrichments are derived from forecast data and conceptually part of the same data domain.

**Rationale:** From a consumer perspective, enrichments are "more forecast data." ha-njord opens one channel and gets everything. A separate service would mean separate proto imports and potentially separate channel management for no real benefit.

### D5: ConsensusUpdate as structured per-parameter data

**Decision:** `ConsensusUpdate` carries per-parameter per-horizon data (median, spread, agreement, available models) — not a pre-flattened weather-entity-compatible structure. ha-njord extracts temperature, humidity, wind etc. from the per-parameter data to build the weather entity.

**Rationale:** njord doesn't know which parameters map to HA weather entity attributes — that's consumer knowledge. Providing the full per-parameter consensus lets any consumer use the data. ha-njord knows that `temperature_2m` → `temperature` and `relative_humidity_2m` → `humidity`.

### D6: StreamConfig pushes full NjordConfig snapshots

**Decision:** `StreamConfig` pushes a complete `NjordConfig` message on every change, not a delta. The first message is sent immediately on subscription (current state).

**Rationale:** Full snapshots are idempotent and simpler to handle on the client side. The config is small (locations, models, flags — a few KB). Deltas would require the client to track state and handle ordering, which is unnecessary complexity for this data volume.

## Risks / Trade-offs

**[Risk] Many concurrent stream clients exhaust BroadcastHub** → Each stream subscription adds a consumer to the hub. Mitigation: BroadcastHub handles this natively; the buffer (64) is per-consumer. In practice, njord will have 1-2 gRPC clients (ha-njord, maybe a debug tool).

**[Risk] Stream disconnects create brief data gaps** → Between disconnect and reconnect, the client misses updates. Mitigation: the Snapshot+Subscribe pattern — on reconnect, the client calls unary RPCs for current state, then re-subscribes. No data loss, just a brief delay.

**[Trade-off] Proto message explosion** → 7 enrichment types × their sub-messages = many proto messages. Mitigated by clean organization (one `message` per domain concept) and proto3's optional fields reducing boilerplate.

**[Trade-off] Domain → Proto mapping is manual** → Each enrichment Result type needs a hand-written mapper. Mitigated by keeping mappers as simple static methods, one per type. The mapping is straightforward (record fields → proto fields).

## Open Questions

None.
