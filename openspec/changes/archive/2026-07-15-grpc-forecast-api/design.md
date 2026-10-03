## Context

njord currently exposes forecast data only via MQTT sensors. The egress layer uses a BroadcastHub pattern: producers (ModelStateActor, EnrichmentActor) emit `EgressEvent` instances into a MergeHub, and consumers (MqttEgressActor) subscribe via the BroadcastHub. This architecture was designed to support multiple consumers — adding a gRPC service as a new consumer fits naturally.

The gRPC h2c (plaintext HTTP/2) approach was validated in `spike/grpc-h2c/`: .NET 10 Kestrel with `HttpProtocols.Http2` on a dedicated port serves gRPC without TLS, while HTTP/1.1 REST endpoints stay on the existing port. Python `grpcio` connects via `insecure_channel()` successfully.

Kestrel currently has no explicit configuration — it binds to `http://+:8080` via the `ASPNETCORE_URLS` environment variable in the Dockerfile.

## Goals / Non-Goals

**Goals:**

- Expose forecast data via a typed gRPC API (unary RPCs) for consumption by a future HA companion integration.
- Provide location/model metadata queries so clients can discover available forecasts.
- Map WMO weather codes to HA-compatible condition strings in the gRPC response.
- Maintain an in-memory snapshot of the latest forecast per (location, model) so gRPC queries don't touch actors or the stream graph.
- Coexist with existing REST endpoints and MQTT egress without affecting them.

**Non-Goals:**

- Server-streaming or bidirectional RPCs (future change).
- The HA companion integration itself (separate project).
- TLS / authentication (internal network deployment).
- Replacing MQTT egress.

## Decisions

### D1: Snapshot store as a singleton, populated from BroadcastHub

**Decision:** A `ForecastSnapshotStore` class (registered as singleton in DI) holds a `ConcurrentDictionary<(string Location, string ModelId), ForecastSnapshot>` with the latest forecast data per model. A `SnapshotConsumerActor` subscribes to the EgressActor's BroadcastHub (via `RequestEgressSource`) and updates the store on each `PerModelUpdate`.

**Rationale:** The gRPC service needs synchronous read access to forecast data. Querying actors via Ask-pattern from a gRPC request handler would create latency, backpressure coupling, and a dependency on actor availability. A read-only snapshot decouples the query path from the stream graph.

**Alternatives considered:**
- Ask-pattern to ModelStateActor → couples gRPC request latency to actor mailbox; no guarantee of response time.
- Direct stream materialization in gRPC handler → overly complex, each request would need its own stream.
- Redis/SQLite cache → external dependency for simple in-memory data; overkill.

### D2: Dual-port Kestrel binding

**Decision:** Explicit `ConfigureKestrel` with two listeners:
- Port 8080: `HttpProtocols.Http1` — existing REST endpoints (health checks, alive).
- Port 8081: `HttpProtocols.Http2` — gRPC (h2c, plaintext HTTP/2).

Ports are configurable via `NjordOptions.Grpc.Port` (default 8081) and `NjordOptions.Http.Port` (default 8080).

**Rationale:** `Http1AndHttp2` on a single port requires TLS for ALPN negotiation (validated in spike — without TLS, Kestrel falls back to HTTP/1.1). Separate ports with explicit protocol binding is the only way to serve both without TLS.

**Alternatives considered:**
- Single port with TLS → requires certificate management in Docker, dev-cert trust issues.
- Single port HTTP/2-only → breaks `curl` and health check probes that expect HTTP/1.1.
- HTTP/1.1 + gRPC-Web → adds complexity (gRPC-Web proxy), loses native gRPC semantics.

### D3: Proto file location and versioning

**Decision:** Proto files live at `protos/njord/v1/` in the repo root. The `Njord.csproj` references them via `<Protobuf Include="..\..\protos\njord\v1\*.proto" GrpcServices="Server" />`. Future HA companion integration will reference the same proto files (or consume them as a published artifact).

**Rationale:** Repo-root `protos/` makes the contract visible and shareable. The `v1` namespace allows future breaking changes via `v2` without affecting existing clients.

### D4: WMO-to-HA condition mapping

**Decision:** A static `WeatherConditionMapper` class maps WMO weather codes (0-99) to HA condition strings. The mapping is a pure function with no state. It's used by the gRPC service when building `ForecastResponse` messages.

**Rationale:** The mapping is deterministic and well-defined (WMO codes are standardized). HA supports 15 condition values. The mapping belongs in the domain layer, not in the gRPC service itself, so it can be reused if other egress paths need it.

WMO code ranges:
- 0: `sunny` / `clear-night` (based on `is_day`)
- 1-3: `partlycloudy` → `cloudy`
- 45-48: `fog`
- 51-67: `rainy` (drizzle/rain intensity maps to `rainy`/`pouring`)
- 71-77: `snowy`
- 80-82: `rainy` (showers)
- 85-86: `snowy` (snow showers)
- 95-99: `lightning-rainy` / `lightning`

### D5: ForecastSnapshot data model

**Decision:** The snapshot captures the parsed horizon data (not the raw JSON strings from `HorizonProjection`). Structure:

```
ForecastSnapshot:
  Location: string
  Model: WeatherModel
  UpdatedAt: DateTimeOffset
  HourlyPoints: IReadOnlyList<HourlySnapshotPoint>
    - Timestamp: DateTimeOffset
    - Temperature: double?
    - ApparentTemperature: double?
    - Precipitation: double?
    - Humidity: double?
    - WindSpeed: double?
    - WindBearing: double?
    - CloudCover: double?
    - WeatherCode: int?
    - (additional fields as needed)
  DailyPoints: IReadOnlyList<DailySnapshotPoint>
    - Date: DateOnly
    - TemperatureMax: double?
    - TemperatureMin: double?
    - PrecipitationSum: double?
    - Sunrise: string?
    - Sunset: string?
```

**Rationale:** Parsing JSON strings on every gRPC request wastes CPU. The snapshot stores typed data that maps directly to proto message fields. The data model is intentionally HA-focused (the fields match what a `weather` entity needs), not a generic dump of all parameters.

**Alternatives considered:**
- Store raw JSON strings and parse per-request → wasteful, repeated deserialization.
- Store the full `ModelForecast` domain object → carries too much data (all parameters at all points), and the domain model isn't gRPC-friendly.

## Risks / Trade-offs

**[Risk] Snapshot store becomes stale** → The store updates on every `EgressEvent.PerModelUpdate`. If the stream graph stalls, the snapshot holds old data. Mitigation: include `UpdatedAt` in the response; the HA integration can check staleness.

**[Risk] Memory usage for large deployments** → One snapshot per (location, model). With 1 location × 8 models, this is trivial. With 10 locations × 8 models = 80 snapshots, still negligible (each is a few KB). Not a concern at njord's scale.

**[Risk] Port conflict with existing infrastructure** → Port 8081 might be in use. Mitigation: make the gRPC port configurable.

**[Trade-off] Duplicate data in memory** → The egress stream already produces per-horizon JSON payloads for MQTT. The snapshot store parses and re-stores this data in a typed form. This is intentional — the gRPC read path should not depend on MQTT's serialization format.

**[Trade-off] No streaming yet** → Unary RPCs only. The HA integration will poll. For 60-minute poll intervals, polling is fine. Streaming can be added as a separate change when real-time updates are needed.

## Open Questions

None — the approach is validated by the spike and the integration points are clear.
