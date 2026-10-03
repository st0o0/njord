## Why

njord currently exposes forecast data exclusively via MQTT sensors — one sensor per (parameter × horizon) per model. This creates ~186 flat entities per model device in Home Assistant, with no native forecast card support. A future companion HACS integration needs a typed, request/response API to create native HA `weather` entities with proper forecast services. MQTT is the right transport for generic IoT consumers, but a dedicated gRPC endpoint provides a first-class, typed integration surface for the HA companion — enabling structured forecast queries, metadata discovery, and a foundation for bidirectional features (on-demand fetch, config changes) later.

## What Changes

- **gRPC ForecastService**: A new `ForecastService` gRPC endpoint exposing `GetLocations`, `GetModels`, and `GetForecast` RPCs. The service reads the latest forecast data from the in-memory egress layer (no additional API calls).
- **Proto definitions**: A `protos/` directory at repo root with versioned `.proto` files (`njord/v1/`) defining the service contract, forecast messages, and HA-compatible weather condition mapping.
- **Kestrel dual-port binding**: Port 8080 remains HTTP/1.1 for REST (health endpoints). A new port 8081 serves gRPC over plaintext HTTP/2 (h2c) — validated in `spike/grpc-h2c/`.
- **WMO-to-HA condition mapping**: A static mapper translating Open-Meteo `weather_code` (WMO 0-99) to HA weather conditions (`sunny`, `cloudy`, `rainy`, etc.) for use in gRPC responses.
- **Forecast snapshot store**: An in-memory read-model that captures the latest per-(location, model) forecast data from the egress event stream, queryable by the gRPC service without touching actors or streams.
- **Dockerfile update**: Expose port 8081 alongside 8080.

## Non-goals

- Building the HA companion HACS integration (separate project, separate repo).
- Replacing MQTT egress — MQTT stays for generic consumers; gRPC is an additional egress path.
- Server-streaming or bidirectional RPCs — this change adds unary RPCs only. Streaming can be added later.
- Authentication/authorization on the gRPC endpoint — njord runs on an internal network; if needed, TLS can be layered on later.
- No change to the poll interval, request budget, or API call weight — the gRPC service reads existing in-memory data.

## Capabilities

### New Capabilities

- `grpc-forecast-service`: gRPC service definition, proto contract, and server implementation for forecast queries.
- `forecast-snapshot-store`: In-memory read-model capturing latest forecast data from the egress stream, queryable by gRPC and potentially other consumers.
- `weather-condition-mapping`: Static mapping from WMO weather codes to HA-compatible condition strings.
- `kestrel-dual-port`: Kestrel configuration for dual-port binding (HTTP/1.1 + HTTP/2 h2c).

### Modified Capabilities

_(none — this is a purely additive change; no existing specs are modified)_

## Impact

- **New dependency**: `Grpc.AspNetCore` NuGet package.
- **New files**: `protos/njord/v1/*.proto`, `src/Njord/Grpc/` directory with service implementation and snapshot store.
- **Kestrel config**: Explicit `ConfigureKestrel` call with dual-port binding. Existing health endpoints continue on 8080.
- **Dockerfile**: Additional `EXPOSE 8081`.
- **Docker Compose / Aspire**: Port 8081 mapping for gRPC.
- **Egress layer**: A new BroadcastHub consumer subscribes to the egress event stream to populate the snapshot store. No changes to existing egress actors or MQTT flow.
- **API budget**: Zero impact — no additional Open-Meteo API calls. The gRPC service reads in-memory data only.
