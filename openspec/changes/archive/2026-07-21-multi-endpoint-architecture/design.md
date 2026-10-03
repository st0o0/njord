## Context

njord's polling pipeline is hardcoded to the Open-Meteo Weather Forecast endpoint (`/v1/forecast`). The `IOpenMeteoClient` builds a fixed URI path, `WeightedTarget` carries a `WeatherModel` field, and the `SchedulerActor` iterates `Locations × Models` only. Adding a second Open-Meteo endpoint (Air Quality, Marine, etc.) would require either duplicating the entire pipeline infrastructure or threading endpoint-type conditionals through every layer.

Open-Meteo offers ~10 location-based polling endpoints that share the same free-tier budget pool. The endpoints differ in URI path, query parameters, response schema, and domain semantics, but the operational pattern is identical: poll → throttle → fetch → parse → enrich → publish to HA.

## Goals / Non-Goals

**Goals:**

- Enable new Open-Meteo endpoints to be added by registering a self-contained module — no pipeline rewiring
- Share the budget gate across all endpoints (single token bucket)
- Isolate endpoint streams so failures in one don't affect others
- Support per-endpoint poll intervals
- Extract existing Weather Forecast code as the first module to validate the architecture

**Non-Goals:**

- Adding Air Quality or Marine endpoints (follow-up changes)
- Cross-endpoint data correlation
- Per-endpoint sub-budgets
- Supporting non-Open-Meteo providers

## Decisions

### D1: Polymorphic WeightedTarget hierarchy (over endpoint-type field)

**Decision:** `WeightedTarget` becomes an abstract record with `EndpointType`, `Location`, `CycleId`, and abstract `Weight`. Each endpoint defines a concrete subclass (e.g., `WeatherTarget` with `Model` field, `AirQualityTarget` without).

**Alternative:** Keep a single `WeightedTarget` with an `EndpointType` enum and nullable `Model?` field.

**Rationale:** The endpoints differ structurally — Weather has models, AQ doesn't, Marine has different parameter types. Nullable fields accumulate and invite invalid states. Subclasses make illegal states unrepresentable and let each sub-stream cast to the concrete type without dictionary lookups.

### D2: Partition stage after shared BudgetThrottleStage (over separate pipelines)

**Decision:** One `MergeHub → BudgetThrottleStage → Partition<WeightedTarget>(N)` graph. The partition routes by `EndpointType.Index`. Each outlet feeds into the module's self-contained sub-graph.

**Alternative A:** Separate pipelines per endpoint, each with its own budget gate.
**Alternative B:** Single pipeline, endpoint-type dispatched inside `SelectAsync`.

**Rationale:** A keeps infrastructure simple but requires coordinating budget across separate token buckets — complex and error-prone. B forces a single `SelectAsync` to handle heterogeneous fetch logic. The partition approach gives one budget gate (all requests compete fairly) and fully isolated downstream processing.

```
  MergeHub<WeightedTarget>
        │
  BudgetThrottleStage (Weight from base)
        │
  Partition<WeightedTarget>(N, t => t.EndpointType.Index)
        │
  ┌─────┼─────────┐
  │     │         │
  Out0  Out1    Out2    ← one per registered module
  │     │         │
  Wx    AQ      Marine  ← fully typed sub-graphs
  │     │         │
  └─────┼─────────┘
        │
  MergeHub<EgressEvent>
        │
  EgressActor
```

### D3: IEndpointModule as composition root (over monolithic class)

**Decision:** Each endpoint is defined by an `IEndpointModule` implementation that composes smaller parts via DI:

```
IEndpointModule
├── EndpointType EndpointType
├── CreateTargets(location, cycleId) → WeightedTarget[]
├── Flow<WeightedTarget, EgressEvent> BuildSubGraph()
└── DeviceInfo[] GetDeviceDefinitions(location)
```

The module internally wires: cast → client → outcome → snapshot → enrichments → EgressEvent mapping. Each part is a separate injectable class.

**Alternative:** Interface with many methods (fetch, parse, enrich, map) that the pipeline orchestrates.

**Rationale:** A flat interface couples the pipeline to each endpoint's internal structure. The module pattern lets each endpoint own its internal composition — Weather needs a snapshot aggregation step (multi-model), AQ doesn't. The pipeline only touches the `Flow<WeightedTarget, EgressEvent>` surface.

### D4: Namespace-per-endpoint with composition (B+C from exploration)

**Decision:**

```
Njord/
├── Endpoints/
│   ├── IEndpointModule.cs
│   ├── EndpointType.cs
│   ├── Weather/
│   │   ├── WeatherModule.cs
│   │   ├── WeatherTarget.cs
│   │   ├── WeatherClient.cs          ← was IOpenMeteoClient
│   │   ├── WeatherOutcome.cs
│   │   ├── WeatherSnapshot.cs        ← was ModelSnapshot
│   │   └── Enrichments/
│   │       ├── ConsensusEnrichment.cs
│   │       ├── AlertEnrichment.cs
│   │       └── ...
│   └── AirQuality/                   ← future, not this change
│       └── ...
├── Pipeline/
│   ├── PipelineActor.cs              ← builds partition from modules
│   ├── BudgetThrottleStage.cs        ← unchanged
│   └── WeightedTarget.cs             ← abstract base
└── Egress/
    └── EgressActor.cs                ← unchanged
```

**Rationale:** Physical separation enforces the isolation contract. A Weather developer can't accidentally import AQ types. Each namespace is a deployable unit of functionality with clear boundaries.

### D5: Per-(Location, EndpointType) scheduling (over global tick)

**Decision:** `SchedulerActor` maintains `ModelPollState` per `(Location, EndpointType, Model?)` triple. Each endpoint type has its own configurable poll interval (Weather: 60min default, AQ: 120min default). The adaptive learning (Discovery/Steady phases) applies per state entry as before.

**Alternative:** Global tick that fires all endpoints simultaneously.

**Rationale:** AQ data updates less frequently than weather forecasts. A 60min AQ poll wastes budget. Independent timers let each endpoint match its update frequency. The scheduler already handles per-entry timers — extending the key from `(Location, Model)` to `(Location, EndpointType, Model?)` is mechanical.

### D6: Enrichments registered per module (over global flat list)

**Decision:** Each `IEndpointModule` provides its enrichment features via `IEnumerable<IEnrichmentFeature>`. The module's `BuildSubGraph()` wires them internally. No global `EnrichmentActor` orchestration needed — enrichment is part of the module's sub-graph.

**Alternative:** Keep the global `EnrichmentActor` and route snapshots to it with an endpoint-type tag.

**Rationale:** Enrichments are semantically tied to their endpoint — consensus makes no sense for AQ data. Keeping them inside the module's sub-graph eliminates cross-endpoint routing and lets each module use different enrichment interfaces if needed (Weather uses `ModelSnapshot`, AQ would use `AirQualitySnapshot`).

## Risks / Trade-offs

**[Risk] Partition outlet count is fixed at materialization** → If endpoints are enabled/disabled at runtime (future config mutation), the graph must be re-materialized. Mitigation: current design already re-materializes on config change; no new risk.

**[Risk] WeightedTarget hierarchy grows with every new endpoint** → Each endpoint adds one subclass. Mitigation: subclasses are tiny (2-3 fields); the hierarchy is flat (no multi-level inheritance). Acceptable for the expected ~5-8 endpoints.

**[Risk] Moving Weather code to new namespace breaks existing tests** → All imports and type references change. Mitigation: do the move as a dedicated task with search-and-replace; run full test suite before proceeding.

**[Risk] SchedulerActor persistence format changes** → Adding `EndpointType` to the poll state key changes the persisted event schema. Mitigation: new persistence DTO version with `EndpointType` field defaulting to `Weather` for existing events. Recovery code handles both versions.

**[Trade-off] Each module duplicates some wiring** (cast → fetch → snapshot → enrich → map). This is intentional — the alternative (shared orchestration) couples modules. The duplication is structural, not logic duplication.

**[Trade-off] Enrichment features move from global DI to per-module scope.** Testing enrichments in isolation still works (they're still DI-injectable classes), but integration tests need to go through the module.

## Open Questions

None — all key decisions were made during exploration. Follow-up changes (AQ, Marine) may surface new questions about endpoint-specific concerns.
