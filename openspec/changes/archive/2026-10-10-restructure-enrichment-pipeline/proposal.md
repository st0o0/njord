## Why

The current enrichment architecture splits every feature across 4–5 projects
(Core constants, Enrichment compute, Mqtt presenter, Host DI) connected by
string-based `EnrichmentTypeNames` matching. Adding or modifying a feature
touches ~6 files with no compiler assistance; the presenter receives `object`
and casts internally; consensus is special-cased everywhere. The pipeline uses
a three-hop SourceRef chain (PipelineActor → ModelStateActor → Consumer) with
each intermediary running its own BroadcastHub — 150+ lines of handshaking per
consumer just to relay data. The `EnrichmentSnapshotActor` shard region
persists single `object` values per location×typeName for gRPC queries. The
result is high friction for changes, implicit knowledge scattered across
horizontal layers, and disproportionate infrastructure for a 60-minute poll
cycle. A restructuring into vertical, self-contained logic libraries with
typed input contracts — modeled after FunkArr's domain-per-project approach —
eliminates the indirection and lets the type system enforce correctness.

## What Changes

- **BREAKING**: Remove MQTT Discovery entirely (retained config payloads,
  birth-message handling, tombstoning, `MqttDiscoveryActor`,
  `DiscoveryPayloadBuilder`, `DiscoveryContext`, all `IEnrichmentPresenter`
  implementations). MQTT becomes a state-only publisher.
- **BREAKING**: Dissolve `Njord.Enrichment` into per-feature logic libraries:
  `Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices`,
  `Njord.History`. Each library owns its compute logic (moved from
  `Njord.Compute`), its typed input interface, and its DI extension method.
- **BREAKING**: Dissolve `Njord.Egress` — `ModelStateActor` relay function
  absorbed into Host pipeline, `CapabilityTracker` moves to pipeline.
- **BREAKING**: Remove the presenter pattern (`IEnrichmentPresenter` and all
  six implementations), `EnrichmentTypeNames`, `IEnrichmentFeature` hierarchy
  (`IStatelessEnrichment`, `IStatefulEnrichment`, `IActorEnrichment`), the
  `EnrichmentActor`, and `ConsensusFlowFactory`.
- **BREAKING**: Remove `EnrichmentSnapshotActor` shard region and its
  persistence DTOs (`EnrichmentSnapshotDto`, `EnrichmentEntryDto`). gRPC
  snapshot queries served from `GrpcConsumerActor` in-memory state.
- **BREAKING**: Remove all untyped enrichment messages: `UpdateEnrichment`,
  `QueryEnrichment`, `QueryEnrichmentCompleted`, `QueryAllEnrichmentsCompleted`,
  `IWithEnrichmentKey`.
- **BREAKING**: Rename `EgressEvent` → `PipelineEvent` with subtypes:
  `PerModelUpdate` → `ModelForecast`, `EnrichmentUpdate` → `AnalysisResult`
  (typed), `CapabilityLearned` → `ModelCapability`.
- Rename enrichment types: `AlertEnrichment` → `AlertAnalyzer`,
  `DerivedEnrichment` → `DerivedComputer`, `TrendEnrichment` →
  `TrendAnalyzer`, `IndexEnrichment` → `IndexComputer`,
  `HistoryEnrichment` → `HistoryTracker`.
- Move per-feature computation logic from `Njord.Compute/Analysis/` into the
  corresponding feature libraries. `Njord.Compute` retains consensus
  computation and shared types only.
- Introduce per-feature typed input interfaces (`IAlertInput`, `ITrendInput`,
  `IIndexInput`, `IHistoryInput`) defined in each library. A `PollContext`
  record in the Host implements all interfaces.
- Flatten `Njord.Core`: remove `EnrichmentTypeNames`, `EnrichmentOptions`
  feature-registry mechanics, `IsEnabled()` switch expression. Per-feature
  options move into each library under `Njord:Analysis:<Feature>`.
- Restructure `Njord.Mqtt` as a SourceRef consumer: subscribes to pipeline's
  `BroadcastHub`, receives typed `PipelineEvent`, publishes state. No
  discovery lifecycle.
- Restructure `Njord.Grpc` as a SourceRef consumer: `GrpcConsumerActor`
  subscribes to pipeline's `BroadcastHub`, holds latest state in-memory for
  unary queries, provides its own `BroadcastHub` for gRPC streaming RPCs
  (`StreamForecasts`, `StreamEnrichments`). Apply KeepAlive + IdleTimeout
  pattern (ref: akka-dynamic-hub-keepalive) for dead client cleanup.
- Rebuild pipeline: `PipelineActor` owns the complete graph from fetch through
  feature fan-out to `BroadcastHub<PipelineEvent>` endpoint. Consumers attach
  via SourceRef — one hop, no relay actors.
- Consolidate all actor registrations in a single `AkkaSetupContainer` in the
  Host (FunkArr pattern).
- Adopt FunkArr persistence convention: `Persisted*` prefix instead of `*Dto`
  suffix. Mapping extensions in domain/feature libraries, not Persistence.
- Trends previous-state managed by a `Scan` operator in the Host pipeline.
- History remains a persistent actor in `Njord.History`, with inline analysis
  per poll cycle.

## Non-goals

- Changing the polling mechanism, Open-Meteo client, or ingest pipeline.
- Adding new enrichment features — this is purely structural.
- Changing the consensus computation logic.
- Replacing Akka.Streams with another stream library.
- Adding new consumers (NAT, etc.) — the architecture enables this but does
  not implement it.
- API-budget impact: zero — no polling changes, same request patterns.

## Capabilities

### New Capabilities

- `vertical-feature-libraries`: Per-feature logic libraries with typed input
  interfaces, DI extensions, and self-contained compute logic.
- `consumer-model`: Output-adapter pattern for MQTT and gRPC as SourceRef
  consumers of the pipeline's BroadcastHub, with gRPC providing a secondary
  hub for streaming clients.
- `pipeline-composition`: PipelineActor-owned stream graph from fetch through
  feature fan-out to BroadcastHub endpoint; consumers attach via SourceRef.

### Modified Capabilities

- `enrichment-orchestration`: Orchestration moves from `EnrichmentActor` +
  `ConsensusFlowFactory` to PipelineActor's graph.
- `mqtt-enrichment-presentation`: Presenter pattern removed; MQTT consumer
  receives typed results directly via SourceRef, state-only publishing.
- `mqtt-egress`: Discovery lifecycle removed; MQTT reduced to state publishing
  and connection management.
- `enrichment-feature-registry`: Feature registry with string-based type names
  removed; features are standalone DI-registered libraries.

## Impact

- **Projects removed**: `Njord.Enrichment` (dissolved), `Njord.Egress`
  (dissolved), `Njord.Enrichment.Tests`, `Njord.Egress.Tests`
- **Projects added**: `Njord.Alerts`, `Njord.Derived`, `Njord.Trends`,
  `Njord.Indices`, `Njord.History` (+ corresponding test projects)
- **Projects modified**: `Njord.Core` (flatten), `Njord.Compute` (shrink to
  consensus + shared types), `Njord.Mqtt` (SourceRef consumer, no discovery),
  `Njord.Grpc` (SourceRef consumer with secondary hub), `Njord.Pipeline`
  (PipelineActor owns full graph), `Njord.Messages` (PipelineEvent, remove
  untyped messages), `Njord.Persistence` (rename to `Persisted*` convention,
  remove `EnrichmentSnapshotDto`), `Njord` host (AkkaSetupContainer, DI)
- **Architecture tests**: Zone rules updated for new project topology
- **Persistence**: `ForecastHistoryActor` moves to `Njord.History`; persistence
  IDs and DTO wire format unchanged for data continuity.
  `EnrichmentSnapshotActor` persistence removed entirely.
- **gRPC**: Streaming RPCs connect to `GrpcConsumerActor`'s BroadcastHub; unary
  queries read from actor's in-memory state. `EnrichmentSnapshotActor` shard
  removed.
- **Config**: `Njord:Enrichment:*` → `Njord:Analysis:<Feature>`
