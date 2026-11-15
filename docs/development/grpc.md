# gRPC API

njord exposes a single `NjordService` (package `njord`, `csharp_namespace = "Njord.Grpc.Proto"`) on port 8081.

## RPCs

| RPC | Type | Purpose |
|-----|------|---------|
| `GetManifest` | Unary | Entity definitions from `FeatureDescriptor.EntityMetadata` x locations |
| `Subscribe` | Server streaming | Unified entity stream: snapshot on connect, then live updates |
| `GetStatus` | Unary | Version, uptime, budget usage, per-location poll state |
| `TriggerPoll` | Unary | Manual poll trigger for a location |

<likec4-view view-id="subscribe-flow"></likec4-view>

## Entity-first design

The gRPC API is entity-first: every feature result is an entity with a unique ID, platform, device class, icon and unit. `ManifestBuilder` builds entity definitions from `FeatureDescriptor.EntityMetadata` x locations at startup.

`Subscribe` provides a unified stream:
1. **Snapshot on connect**: current state of all entities
2. **Live updates**: `EntityStateUpdate` with a per-OutputKind value oneof
3. **Availability**: `EntityUnavailable` / `EntityAvailable` events

Seven generic value types per OutputKind (`AlertValue`, `ScoreValue`, `ConditionValue`, `TrendValue`, `ForecastValue`, `WeatherValue`, `ObservationValue`) replace per-feature oneof cases. Adding a new feature in njord requires **no proto or ha-njord changes**.

## Mapping

Mapping is generic per OutputKind in `Njord.Grpc.Mapping/`:

| Mapper | Maps |
|--------|------|
| `AlertMappingExtensions` | `AlertResult` -> `AlertValue` |
| `ScoreMappingExtensions` | `ScoreResult` -> `ScoreValue` |
| `ConditionMappingExtensions` | `ConditionResult` -> `ConditionValue` |
| `TrendMappingExtensions` | `TrendResult` -> `TrendValue` |
| `ForecastMappingExtensions` | `ForecastResult` -> `ForecastValue` |
| `WeatherMappingExtensions` | `WeatherResult` -> `WeatherValue` |
| `ObservationMappingExtensions` | `ObservationResult` -> `ObservationValue` |

All mappers are extension methods (`ToProto()`). Proto-generated types live in `Njord.Grpc.Proto` and are referenced with the `Proto.` prefix. Timestamps via `ToTimestamp()`.

## Proto management

The proto source of truth is `protos/njord/njord.proto` in the njord repo. When it changes, copy to `ha-njord/protos/` and regenerate stubs:

```bash
# In ha-njord (Docker)
docker run --rm -v "$PWD:/work" -w /work python:3.12-slim \
  sh -c "pip install --quiet 'grpcio-tools>=1.70,<1.79' 'protobuf>=5.0,<6.0' && \
  python -m grpc_tools.protoc \
    -Iprotos \
    --python_out=custom_components/njord/proto \
    --grpc_python_out=custom_components/njord/proto \
    protos/njord/njord.proto"
```

A GitHub Actions workflow (`sync-protos.yml`) automates this on njord releases.

## EntityIdLookup

`EntityIdLookup` maps `(FeatureResult type, location)` to `entity_id`. The ID format is deterministic from the feature descriptor and location name. `GrpcConsumer` maintains `EntityStates` (current values) and `StaleEntities` (availability).
