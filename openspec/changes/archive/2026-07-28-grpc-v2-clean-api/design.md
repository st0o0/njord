## Context

The v1 gRPC API grew organically over multiple changes: ForecastService (6 RPCs) handles
weather reads and streams, ConfigService (11 RPCs) mixes config reads, CRUD mutations,
settings mutations, status queries, and operations. Time fields use three different
representations. Model discovery requires 2-3 sequential calls.

Njord is pre-release with no external API consumers. This is the right time for a
clean break before stabilizing.

## Goals / Non-Goals

**Goals:**

- Three client-aligned services: WeatherService (read/stream), AdminService (config),
  OpsService (status/trigger).
- All temporal fields use `google.protobuf.Timestamp` (except date-only fields).
- Shared types in `common.proto` — one place for LocationInfo, ModelInfo, forecast
  points, enrichment payloads.
- Single `GetCatalog` RPC replaces `GetLocations` + `GetModels`.
- Declarative `SetLocations` replaces Add/Remove/Update CRUD.
- `SetSettings` consolidates forecast settings + default_models.

**Non-Goals:**

- New functionality beyond v1 parity.
- Internal actor/message changes.
- Optimistic concurrency on mutations.
- v1 backward compatibility layer.

## Decisions

### Hard cut over v1 coexistence

Delete v1 protos and service implementations entirely. No deprecated aliases, no
dual registration.

Alternative: Run v1 and v2 in parallel. Rejected — no external consumers exist,
and dual maintenance adds complexity for zero benefit.

### Three services, four proto files

```
protos/njord/v2/
├── common.proto     ← shared types, imported by all three
├── weather.proto    ← WeatherService
├── admin.proto      ← AdminService
└── ops.proto        ← OpsService
```

Alternative: One monolithic proto with three services. Rejected — clients that only
need weather data would import config and ops types unnecessarily.

Alternative: Two services (Weather + Admin with ops mixed in). Rejected — ops concerns
(trigger, status) serve a different client persona than config management.

### Declarative SetLocations (replace-all semantics)

`SetLocations` receives the complete location list. Missing locations are removed,
new ones are added, changed ones are updated. No separate Add/Remove/Update RPCs.

The client reads current config via `GetConfig`, modifies the list, sends it back.
No etag — single-admin assumption for a Home Assistant appliance.

Alternative: Keep CRUD RPCs. Rejected — three RPCs for what is conceptually
"here are my locations" is unnecessary for a config set that rarely exceeds 5 entries.

### Timestamp for all temporal fields

Every time field uses `google.protobuf.Timestamp`:
- `HourlyForecast.valid_at` (was `timestamp` as int64)
- `ForecastResponse.updated_at` (was int64)
- `ModelStatus.next_poll`, `last_change` (was int64)
- `EnrichmentEvent.updated_at` (was int64)

Exception: `DailyForecast.date` stays `string` ("2026-07-28") because it represents
a calendar date without a time component. `google.type.Date` would add a dependency
on googleapis for one field.

Alternative: Keep int64 for consistency with existing clients. Rejected — there are
no existing clients to break.

### Enrichment payloads move 1:1

AlertUpdate, IndexUpdate, TrendUpdate, EnergyUpdate, DerivedUpdate, HistoryUpdate,
ConsensusUpdate and their sub-messages move unchanged to `common.proto`. The enrichment
domain is stable and well-tested; redesigning it is out of scope.

### Service implementation: three classes replace two

```
ForecastGrpcService.cs  ──┐
                          ├──→  WeatherGrpcService.cs
ConfigGrpcService.cs    ──┤     AdminGrpcService.cs
                          └──→  OpsGrpcService.cs
```

Each class gets only the dependencies it needs. WeatherGrpcService does not need
ConfigPersistence or mutation locks. OpsGrpcService does not need IOptionsMonitor.

### Namespace: Njord.Grpc.V2

Generated C# code uses `csharp_namespace = "Njord.Grpc.V2"`. Service implementations
stay in the `Njord.Grpc` namespace (no version suffix on the C# service classes).

## Risks / Trade-offs

- [Big bang change] All gRPC tests must be rewritten simultaneously.
  → Mitigated by: internal actor tests are unaffected; only the gRPC layer tests change.
  Tests can be split across tasks (per service).
- [SetLocations race] Two parallel admin clients could overwrite each other.
  → Accepted: njord is a single-admin Home Assistant appliance. Document the
  single-writer assumption.
- [Proto import chain] `common.proto` imported by all three services creates a
  coupling point. → Acceptable: these are shared domain types that genuinely belong
  together. Changes to common types should affect all services.
