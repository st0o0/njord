## Why

The current gRPC data layer uses `ConcurrentDictionary`-based stores (`ForecastSnapshotStore`, `EnrichmentSnapshotStore`) populated by consumer actors (`SnapshotConsumerActor`, `EnrichmentSnapshotConsumerActor`) that subscribe to the BroadcastHub and parse JSON back into typed data. This is architecturally wrong: typed data (`ModelForecast`) gets serialized to JSON in `ModelStateActor` (for MQTT), then a consumer actor parses it back to typed data (for gRPC). The consumer actors are pure boilerplate with no business logic — they exist only to bridge Akka Streams into a `ConcurrentDictionary`.

The fix: proper Akka Persistence actors that hold snapshot state, queryable via Ask, with the JSON serialization moved to where it belongs (MqttEgressActor).

## What Changes

- **`EgressEvent.PerModelUpdate` carries `ModelForecast` instead of `Dict<string, string>`**: The typed domain object flows through the BroadcastHub. No JSON in the egress event.
- **`ModelStateActor` simplifies**: Removes `HorizonProjection`, delta-dedup, and JSON serialization. Just forwards `ModelForecast` as `EgressEvent.PerModelUpdate`.
- **`MqttEgressActor` gains `HorizonProjection` + delta-dedup**: JSON serialization moves here — the only consumer that needs JSON. Delta-publishing dedup stays with MQTT where it belongs.
- **New `ForecastSnapshotActor`**: Akka Persistence actor holding `Dict<(Location, ModelId), ModelForecast>`. Receives updates via messages, responds to `GetForecast`/`GetAllForecasts` queries via Ask. Persists snapshots for restart resilience.
- **New `EnrichmentSnapshotActor`**: Akka Persistence actor holding `Dict<(Location, TypeName), object>` (enrichment Results). Receives updates, responds to queries. Persists snapshots.
- **New `SnapshotConsumerActor`**: Single actor subscribing to EgressActor BroadcastHub. Routes `PerModelUpdate` to `ForecastSnapshotActor` via Ask/Ack, routes `EnrichmentUpdate` to `EnrichmentSnapshotActor` via Ask/Ack. Backpressure via Ack.
- **Delete**: `ForecastSnapshotStore`, `EnrichmentSnapshotStore`, old `SnapshotConsumerActor`, `EnrichmentSnapshotConsumerActor`, `ForecastSnapshot.cs`, `EnrichmentSnapshot.cs`.
- **`ForecastGrpcService`**: Ask snapshot actors instead of reading stores. Maps `ModelForecast` → Proto directly (no intermediate DTO). Streaming RPCs use typed `ModelForecast`/`Result` from BroadcastHub.
- **`StreamForecasts`/`StreamEnrichments`**: Use typed data from BroadcastHub directly — no JSON parsing.

## Non-goals

- Changing the Akka Persistence provider (stays SQLite/PostgreSQL as configured).
- Changing the MQTT topic structure or payload format.
- Adding new gRPC RPCs — only changing how existing RPCs get their data.

## Capabilities

### New Capabilities

- `snapshot-actors`: Akka Persistence actors for forecast and enrichment snapshots, with Ask/Ack consumer pattern.

### Modified Capabilities

- `egress-event`: `PerModelUpdate` carries `ModelForecast` instead of `Dict<string, string>`.
- `delta-publishing`: Delta-dedup moves from `ModelStateActor` to `MqttEgressActor`.
- `grpc-forecast-service`: Queries snapshot actors via Ask instead of reading stores.
- `forecast-snapshot-store`: Replaced by `ForecastSnapshotActor` (Akka Persistence).
- `grpc-enrichment-api`: `EnrichmentSnapshotStore` replaced by `EnrichmentSnapshotActor`.

## Impact

- **EgressEvent**: Breaking shape change (`HorizonPayloads` → `Forecast`). All BroadcastHub consumers must adapt.
- **ModelStateActor**: Significantly simplified (loses ~60% of its code).
- **MqttEgressActor**: Gains HorizonProjection + dedup logic (moved from ModelStateActor).
- **gRPC**: Ask-based queries add minor latency (~microseconds for in-process actor mailbox). Acceptable for 60-minute poll intervals.
- **Persistence**: Two new persistent actor IDs in the journal. Snapshot size is small (a few KB per location×model).
- **Tests**: Significant test updates — ModelStateActor tests, MqttEgressActor tests, gRPC service tests, new snapshot actor tests.
- **API budget**: Zero impact.
