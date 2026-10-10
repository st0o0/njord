## Context

njord's enrichment system currently splits every feature across horizontal
layers connected by string-based `EnrichmentTypeNames`: compute logic in
`Njord.Enrichment`, MQTT presentation in `Njord.Mqtt` (via
`IEnrichmentPresenter`), type constants and options in `Njord.Core`, and DI
wiring in the Host. Adding a feature touches ~6 files across 4 projects with
no compiler assistance. The presenter receives `object` and casts internally.
Consensus is special-cased outside the feature interface. MQTT Discovery adds
substantial lifecycle complexity (birth handling, tombstoning, retained config
payloads) for a feature whose value is unclear.

The pipeline uses a three-hop SourceRef chain: `PipelineActor` broadcasts
`FetchOutcome` → `ModelStateActor` and `EnrichmentActor` each run their own
`BroadcastHub` → `MqttStateActor`, `MqttDiscoveryActor`, and
`GrpcSnapshotConsumerActor` each subscribe to one or both upstream hubs.
`GrpcSnapshotConsumerActor` alone is 150+ lines of SourceRef handshaking to
forward events to a shard of `EnrichmentSnapshotActor`s that each persist a
single `object? _result`. All this machinery exists to cache and relay data
from a 60-minute poll cycle.

FunkArr demonstrates that vertical-slice libraries — each domain owning its
full concern — eliminate this indirection while keeping the architecture
testable and extensible.

## Goals / Non-Goals

**Goals:**
- Eliminate string-based feature registry and runtime type dispatch
- Each feature is a self-contained library with typed input contracts
- MQTT and gRPC become SourceRef consumers of a single pipeline BroadcastHub
- Pipeline composition is explicit: one actor, one graph, one BroadcastHub
- Adding a new feature requires one new project + Host registration, nothing else
- Adding a new consumer requires one SourceRef subscription, nothing else
- History persistence continuity (no data migration)
- Adopt FunkArr persistence conventions (`Persisted*` prefix, domain-owned mapping)

**Non-Goals:**
- Changing ingest, consensus computation, or polling logic
- Adding new features or consumers
- Optimizing stream throughput (compute is already sub-millisecond)
- Re-introducing Discovery (may come later as a consumer-level feature)

## Decisions

### D1: Dissolve Njord.Enrichment into per-feature libraries

**Choice**: One project per feature (`Njord.Alerts`, `Njord.Derived`,
`Njord.Trends`, `Njord.Indices`, `Njord.History`). Each library contains its
compute logic (moved from `Njord.Compute/Analysis/`), its typed input
interface, its options, and its DI extension.

**Alternative considered**: Keep a single `Njord.Enrichment` with internal
namespaces per feature. Rejected because: features have different
characteristics (stateless vs stateful vs actor-based), independent options,
and no shared code beyond what Core provides. Separate projects enforce
independence at the build level.

### D2: Move computation from Njord.Compute into feature libraries

**Choice**: `AlertEvaluator`/`AlertResult` → `Njord.Alerts`,
`DerivedComputer`/`DerivedResultComputer`/`DerivedResult` → `Njord.Derived`,
`TrendComputer`/`TrendAnalyzer`/`TrendResult` → `Njord.Trends`,
`IndexComputer`/`IndexScorer`/`IndexResult` → `Njord.Indices`,
`HistoryComputer`/`HistoryAnalyzer`/`HistoryResult` → `Njord.History`.
`Njord.Compute` retains only `ConsensusComputer`, `ConsensusResult`,
`ConsensusSnapshot`, `ConsensusSnapshotFactory`, and shared types.

**Rationale**: Each feature library owns its full vertical slice — input
interface, computation, result types. No reason for a separate Compute project
to hold logic that belongs to one feature.

### D3: Typed input interfaces defined per library

**Choice**: Each library defines its own input interface (`IAlertInput`,
`ITrendInput`, etc.). The Host defines a `PollContext` record implementing all
interfaces.

**Alternative considered**: A shared contracts project defining all input
interfaces. Rejected because: it creates a central point that must change
whenever a feature's input requirements change — the same problem as
`EnrichmentTypeNames`.

### D4: Remove MQTT Discovery entirely

**Choice**: Drop all Discovery lifecycle code. MQTT publishes state only.

**Rationale**: Discovery drives a disproportionate amount of architectural
complexity (presenter pattern, device ID generation, birth-message actor,
tombstoning, retained config management). Its value for a single-user
self-hosted service is unclear. Removing it simplifies MQTT to a pure
state publisher. If needed later, it can be re-introduced as a clean
consumer-level concern without infecting the core architecture.

### D5: PipelineActor owns the full graph, ending at BroadcastHub

**Choice**: `PipelineActor` materializes the complete stream graph: fetch →
consensus → feature fan-out → merge → `BroadcastHub<PipelineEvent>`. The
BroadcastHub is the single endpoint. Consumers attach via SourceRef — one hop.

```
PipelineActor (owns, materializes, supervises)
│
└──▶ Fetch (Queue → Throttle → HTTP → Aggregate)
     → Consensus
     → Scan (previous snapshot for trends)
     → Build PollContext (consensus + sensors + previous + models)
     → Broadcast
           ├──▶ Select(alerts.Compute)     ──┐
           ├──▶ Select(derived.Compute)    ──┤
           ├──▶ Select(trends.Compute)     ──┤  Merge
           ├──▶ Select(indices.Compute)    ──┤
           └──▶ SelectAsync(history.Ask)   ──┘
     → KeepAlive (heartbeat during 60min idle)
     → Buffer (decouple consumer backpressure)
     → IdleTimeout (detect dead consumers)
     → BroadcastHub<PipelineEvent>
```

KeepAlive + Buffer + IdleTimeout pattern from
github.com/Aaronontheweb/akka-dynamic-hub-keepalive ensures dead consumers
are cleaned up without affecting healthy ones.

**What PipelineActor does NOT do**: no business logic, no state beyond graph
lifecycle. It materializes the graph, keeps it alive, restarts on failure.

**What is eliminated**: `ModelStateActor` (relay → absorbed into graph),
`EnrichmentActor` (relay → absorbed into graph), `ConsensusFlowFactory`
(inline in graph), `GrpcSnapshotConsumerActor` (150-line SourceRef
handshaker → replaced by GrpcConsumerActor), all intermediate BroadcastHubs.

### D6: Consumers attach via SourceRef

**Choice**: Each consumer requests a `SourceRef<PipelineEvent>` from
`PipelineActor` and runs its own sink. One hop from pipeline to consumer.

```
BroadcastHub<PipelineEvent>
        │
        ├──SourceRef──▶ MqttConsumerActor
        │                 (publishes state to MQTT)
        │
        └──SourceRef──▶ GrpcConsumerActor
                          (holds latest state in-memory)
                          (provides secondary BroadcastHub for gRPC streaming)
                               │
                               ├──SourceRef──▶ StreamForecasts (gRPC client)
                               ├──SourceRef──▶ StreamEnrichments (gRPC client)
                               └──SourceRef──▶ ...
```

**GrpcConsumerActor** serves two roles:
1. **Unary queries** (`GetForecast`, `GetEnrichments`): responds from in-memory
   state (`Dictionary<Location, LatestResults>`). No persistence needed — on
   restart, state refills after the first poll cycle.
2. **Streaming RPCs** (`StreamForecasts`, `StreamEnrichments`): provides a
   secondary `BroadcastHub` that gRPC streaming clients attach to via SourceRef.
   Uses the same KeepAlive + IdleTimeout pattern for dead gRPC client cleanup.

**What is eliminated**: `EnrichmentSnapshotActor` shard region (persisted one
`object` each), `ForecastSnapshotRegion` (if forecast snapshots also move to
GrpcConsumerActor state), `GrpcSnapshotConsumerActor` (150-line SourceRef
handshaker).

### D7: Dissolve Njord.Egress

**Choice**: `Njord.Egress` is removed entirely. `ModelStateActor`'s function
(extracting per-model forecasts and capabilities from `FetchOutcome` and
broadcasting as events) is absorbed into PipelineActor's graph as inline
`Select` stages. `CapabilityTracker` moves to the pipeline.

### D8: Remove untyped enrichment messages

**Choice**: Remove all string+object messages: `UpdateEnrichment`,
`QueryEnrichment`, `QueryEnrichmentCompleted`, `QueryAllEnrichmentsCompleted`,
`IWithEnrichmentKey`. Replace with typed queries to `GrpcConsumerActor`.

### D9: Rename EgressEvent to PipelineEvent

`EgressEvent` → `PipelineEvent` with renamed subtypes:
- `PerModelUpdate` → `PipelineEvent.ModelForecast`
- `EnrichmentUpdate` → `PipelineEvent.AnalysisResult` (typed, no `object`)
- `CapabilityLearned` → `PipelineEvent.ModelCapability`

"Pipeline" describes origin, "Event" describes that it flows through the
stream. Subtypes are nouns (what they are) not verbs (what they do).

### D10: Trends previous-state via Scan

**Choice**: The Host's stream graph uses a `Scan` operator to carry the
previous `ConsensusSnapshot`. The `TrendAnalyzer` is stateless — it receives
current and previous via `ITrendInput`.

### D11: History remains a persistent actor, invoked inline

**Choice**: `ForecastHistoryActor` stays in `Njord.History` as a persistent
actor. It is invoked inline in the pipeline via `SelectAsync` + Ask (record +
query + compute per cycle).

**Persistence IDs and DTO wire format remain unchanged** to avoid data
migration.

### D12: Config section naming — `Njord:Analysis:<Feature>`

Per-feature config lives under `Njord:Analysis:Alerts`,
`Njord:Analysis:Trends`, etc. Grouping under `Analysis` keeps the config
hierarchy tidy and mirrors the project structure.

### D13: Consensus stays in the pipeline

Consensus computation remains in `Njord.Compute` as pure logic. The Host
pipeline calls it directly before feature fan-out. It is not a feature
library — it is infrastructure that produces the `ConsensusSnapshot` all
features depend on.

### D14: Centralized actor registration

All actors registered in a single `AkkaSetupContainer` in the Host, organized
by domain in private methods. FunkArr's proven pattern.

### D15: FunkArr persistence convention

Adopt `Persisted*` prefix instead of `*Dto` suffix for persistence types.
Mapping extensions (`*MappingExtensions.cs`) live in the feature library that
owns the actor, not in `Njord.Persistence`. `Njord.Persistence` holds only
the DTO records themselves (extend-only, versioned).

Remove `EnrichmentSnapshotDto` and `EnrichmentEntryDto` entirely (no more
persisted enrichment snapshots).

### D16: Feature naming convention

`AlertAnalyzer`, `DerivedComputer`, `TrendAnalyzer`, `IndexComputer`,
`HistoryTracker`. No "Enrichment" prefix.

## Project structure after restructuring

```
src/
  # Foundation
  Njord.Weather/           Pure weather records (unchanged)
  Njord.Sensors/           Pure sensor records (unchanged)
  Njord.Persistence/       Persisted* DTOs (extend-only, Persisted* prefix)
  Njord.Messages/          PipelineEvent, actor messages (no untyped enrichment msgs)
  Njord.Compute/           ConsensusComputer + shared types only (feature logic moved out)

  # Core (flattened)
  Njord.Core/              Options, validators, actor keys, diagnostics
                           (no EnrichmentTypeNames, no feature registry)

  # Feature libraries (NEW — replace Njord.Enrichment + Njord.Compute/Analysis)
  Njord.Alerts/            IAlertInput, AlertAnalyzer, AlertOptions, AddAlerts()
  Njord.Derived/           IDerivedInput, DerivedComputer, AddDerived()
  Njord.Trends/            ITrendInput, TrendAnalyzer, AddTrends()
  Njord.Indices/           IIndexInput, IndexComputer, IndexOptions, AddIndices()
  Njord.History/           IHistoryInput, HistoryTracker, ForecastHistoryActor,
                           HistoryOptions, AddHistory(), mapping extensions

  # Consumers (restructured)
  Njord.Mqtt/              MqttConsumerActor (SourceRef subscriber), state publisher,
                           connection actor (no discovery)
  Njord.Grpc/              GrpcConsumerActor (SourceRef subscriber, in-memory state,
                           secondary BroadcastHub for streaming RPCs), gRPC services

  # Ingest (unchanged)
  Njord.Ingest/
  Njord.SensorHub/
  Njord.Pipeline/          PipelineActor (owns full graph: fetch → features → BroadcastHub)

  # Host
  Njord/                   PollContext, AkkaSetupContainer, DI composition

  # Removed
  # Njord.Enrichment/      → dissolved into feature libraries
  # Njord.Egress/          → absorbed into PipelineActor graph
```

## Risks / Trade-offs

**[Risk] Large blast radius** → This touches most of the codebase. Mitigated
by incremental implementation: create new libraries first, migrate logic,
update pipeline, remove old code last. Tests provide a safety net (836
existing tests).

**[Risk] gRPC unary queries return empty after restart** → GrpcConsumerActor
has no persistence; state refills after the first poll cycle (≤60 min).
Acceptable for a self-hosted weather service.

**[Risk] Breaking config change for users** → `Njord:Enrichment:*` →
`Njord:Analysis:*`. While the project is 0.x, breaking config changes are
expected.

**[Risk] Discovery removal breaks existing HA setups** → Users relying on
auto-discovery will need to manually configure entities. Deliberate scope
reduction; can be re-introduced later.

**[Risk] Dead gRPC streaming clients occupy BroadcastHub slots** → Mitigated
by KeepAlive + IdleTimeout pattern from akka-dynamic-hub-keepalive.

**[Trade-off] More projects (5 feature libs, minus 2 dissolved = net +3)** →
Each project is small and focused. Independence and typed contracts outweigh
overhead.

**[Trade-off] PollContext knows all input interfaces** → The Host is the
composition root — it knows everything. Same as FunkArr.

**[Trade-off] PipelineActor graph is large** → One graph is easier to reason
about than three actors with SourceRef chains. The graph is built from
composable pieces (feature flows, consumer setup) assembled in the Host.
