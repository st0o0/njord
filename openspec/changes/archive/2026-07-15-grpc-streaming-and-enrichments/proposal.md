## Why

The ha-njord HACS integration needs a real-time, streaming API from njord — not polling-based. njord's gRPC API currently only exposes unary RPCs (`GetLocations`, `GetModels`, `GetForecast`). ha-njord needs to subscribe once and receive forecast updates, enrichment results, and config changes as they happen. Additionally, the 7 enrichment features (Alerts, Indices, Trends, Energy, Derived, History, Consensus) are only available via MQTT today; ha-njord needs them over gRPC so it can create sensor/binary_sensor/weather entities without touching MQTT.

## What Changes

- **`StreamForecasts` RPC**: Server-streaming on `ForecastService`. Subscribes to the EgressActor BroadcastHub, filters `PerModelUpdate` events, maps to `ForecastUpdate` proto messages, and pushes to all connected gRPC clients in real-time. Same data as the existing `GetForecast`, but pushed instead of polled.
- **`StreamEnrichments` RPC**: Server-streaming on `ForecastService`. Subscribes to the BroadcastHub, filters `EnrichmentUpdate` events, dispatches to typed proto messages (`AlertUpdate`, `IndexUpdate`, `TrendUpdate`, `EnergyUpdate`, `DerivedUpdate`, `HistoryUpdate`, `ConsensusUpdate`), and pushes to clients. ha-njord creates sensors from these; ConsensusUpdate specifically becomes a `weather.njord_{location}_consensus` entity.
- **`GetEnrichments` RPC**: Unary RPC returning the latest enrichment snapshot for a location — initial state on connect before the stream takes over.
- **`EnrichmentSnapshotStore`**: In-memory singleton (same pattern as `ForecastSnapshotStore`) capturing latest enrichment results per (location, type) from the BroadcastHub.
- **`ConfigService`**: New gRPC service with `GetConfig` (unary, returns current `NjordConfig`) and `StreamConfig` (server-streaming, pushes config snapshots on change). Read-only — no write endpoints. ha-njord uses this to discover locations/models and react to config changes.
- **Proto messages for all enrichment types**: Alert (9 types × severity), Index (8 scores + HDD/CDD + frost + VPD), Trend (directions + timing + stability), Energy (heating/COP/shading/battery), Derived (per-horizon + scalars), History (per-model MAE/weights + anomaly), Consensus (per-parameter per-horizon median/spread/agreement).

## Non-goals

- Write endpoints for config (no `SetConfig`, `AddLocation`, etc. — that's a separate change).
- HA-specific mapping or condition strings in proto messages — consumers handle that.
- Replacing MQTT egress — MQTT stays for generic consumers; gRPC streaming is an additional egress path.
- Phase 3 features (Zeroconf discovery, diagnostics, repairs).

## Capabilities

### New Capabilities

- `grpc-forecast-streaming`: Server-streaming RPC for real-time forecast updates via gRPC.
- `grpc-enrichment-api`: Unary + streaming RPCs for enrichment data (alerts, indices, trends, energy, derived, history, consensus) with typed proto messages and in-memory snapshot store.
- `grpc-config-service`: Read-only gRPC service for config discovery and change notification.

### Modified Capabilities

- `grpc-forecast-service`: Add `StreamForecasts` and `GetEnrichments` RPCs to the existing service. Existing unary RPCs unchanged.

## Impact

- **Proto files**: Significant expansion — enrichment messages, streaming RPCs, new `config_service.proto`.
- **New code**: `EnrichmentSnapshotStore`, `ConfigGrpcService`, streaming handler logic in `ForecastGrpcService`.
- **Existing code**: No changes to egress actors, MQTT, or pipeline — streaming endpoints are new BroadcastHub consumers.
- **Dependencies**: No new NuGet packages (gRPC streaming is built into `Grpc.AspNetCore`).
- **API budget**: Zero impact — reads existing in-memory data only.
