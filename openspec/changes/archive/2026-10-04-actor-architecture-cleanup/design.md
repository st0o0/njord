## Context

Njord's actor architecture interconnects 14 production actors through 3 hub actors using SinkRef/SourceRef stream references. Each StreamConsumerActor subclass repeats ~60 lines of resolve/retry boilerplate per dependency. The SchedulerActor alone has 5 Become phases, 4 of which exist solely for pipeline connection setup. Shared messages contain Akka.Streams infrastructure types.

FunkArr (a sibling project) demonstrates a cleaner pattern: ShardRegion entities routed by marker interfaces, clean API/persistence/message separation, and no hub intermediaries. This design applies those patterns to Njord.

## Goals / Non-Goals

**Goals:**
- Eliminate all hub actors (EgressActor, PipelineActor-as-hub)
- Introduce producer-broadcast: each producing actor owns its BroadcastHub
- Remove Akka.Streams dependency from Njord.Messages
- Convert entity actors to ShardRegions (FunkArr pattern)
- Reduce SchedulerActor from ~500 LOC / 5 phases to ~200 LOC / 1-2 phases
- Clean domain namespaces in Messages (add Mqtt/, Enrichment/)

**Non-Goals:**
- Removing Akka.Streams from the data pipeline
- Changing enrichment feature interfaces or persistence DTOs
- Multi-node clustering (ShardRegions are for routing/passivation, not distribution)
- Replacing `object Result` typing in EnrichmentUpdate

## Decisions

### D1: Producer-Broadcast over central hub

**Decision**: Each producing actor (PipelineActor, ModelStateActor, EnrichmentActor) materializes its own BroadcastHub and serves SourceRefs to consumers on request. No central EgressActor hub.

**Why not keep the hub**: The hub pattern adds an indirection layer (MergeHub → BroadcastHub) that requires every producer to obtain a SinkRef and every consumer to obtain a SourceRef from the hub. With only 2-3 producers and 2-3 consumers, the hub is overhead. Direct producer-to-consumer SourceRefs eliminate the SinkRef half entirely.

**Why not pure Tell-based fan-out**: The user explicitly wants data to flow through streams. SourceRef preserves stream semantics (backpressure, supervision, materialization lifecycle) while eliminating the SinkRef ceremony.

**Alternative considered — one big materialized graph**: Rejected because it would require a central setup class wiring all actors, which mixes concerns and makes individual actor testing harder.

### D2: PipelineActor receives ScheduledPoll via Tell

**Decision**: PollSchedulerActor resolves PipelineActor from the registry, watches it, and sends `ScheduledPoll` messages via Tell. PipelineActor enqueues them into an internal SourceQueue that feeds the fetch pipeline.

**Why**: This eliminates the MergeHub (only one producer) and the entire SinkRef connection ceremony from the scheduler. The scheduler no longer needs WaitingForRefs, Connecting, or WaitingForConnection phases.

**Feedback path**: PollSchedulerActor subscribes to PipelineActor's BroadcastHub via SourceRef for hash results and failure feedback. This is a single SourceRef dependency — handled by StreamConsumerActor base class without custom Become phases.

**Shape of PollSchedulerActor**:
- `ReceivePersistentActor` (not StreamConsumerActor — it only needs one SourceRef and the base class overhead is not worth it for a single dependency)
- Phases: `Recovering` → `Ready` (2 phases instead of 5)
- Resolves PipelineActor once, watches it, sends Tell
- Receives hash/failure feedback via a simple materialized SourceRef stream (Sink.ActorRef to self)

### D3: MqttStateActor and MqttDiscoveryActor call transport directly

**Decision**: Instead of requesting a SinkRef from MqttConnectionActor, the MQTT consumer actors call `IMqttTransport.SendAsync` directly from their stream's `SelectAsync` stage.

**Why**: MqttConnectionActor currently serves as a hub (MergeHub → SendAsync). But the transport interface already handles connection state internally. The actor's role is connection lifecycle (connect, reconnect, subscribe to HA status) — not message routing. Removing the SinkRef API from MqttConnectionActor eliminates 3 SinkRef connections and the MergeHub.

**MqttConnectionActor retains**: TCP lifecycle, reconnect with backoff, HA birth detection (subscribes to `homeassistant/status`), `SubscribeInbound` API for Discovery. It no longer materializes a MergeHub or serves SinkRefs.

**Availability messages** (LWT online/offline): MqttConnectionActor sends these directly via transport on connect/disconnect — no stream needed.

### D4: ShardRegion entities (FunkArr pattern)

**Decision**: ForecastHistoryActor, ForecastSnapshotActor, and EnrichmentSnapshotActor become ShardRegion entities routed by marker interfaces.

**Entity key scheme**:
| Region | Entity key | Example |
|--------|-----------|---------|
| ForecastHistory | `{location}` | `borken` |
| ForecastSnapshot | `{location}\|{modelId}` | `borken\|icon_d2` |
| EnrichmentSnapshot | `{location}\|{typeName}` | `borken\|consensus` |

**MessageExtractor**: `NjordMessageExtractor` extends `HashCodeMessageExtractor` (like FunkArr's `ShardMessageExtractor`). Routes by `IWithModelKey` → `IWithEnrichmentKey` → `IWithLocation` (most-specific first).

**PersistenceId**: Each entity uses `$"{regionName}-{entityId}"` (e.g., `forecast-snapshot-borken|icon_d2`).

**Passivation**: `PassivateIdleEntityAfter = TimeSpan.FromMinutes(10)`, `ShouldPassivateIdleEntities = true`. Entities are re-created on next message.

**Migration from singleton**: ForecastSnapshotActor and EnrichmentSnapshotActor currently store all entries in a Dictionary with a single PersistenceId. Migration requires: (1) new entities start empty (no data migration from old journal), (2) old singleton actor and its PersistenceId are retired, (3) data repopulates naturally on the next poll cycle. This is acceptable because snapshot data is transient (rebuilt every poll).

**Why not Singleton**: These actors hold per-entity state (per location, per model). Singleton is for global state (SchedulerActor, BudgetTrackerActor). ShardRegion gives entity isolation, automatic passivation, and the FunkArr-proven routing pattern.

### D5: Message namespace organization

**Decision**: Organize messages by domain namespace, remove infrastructure types.

**Removed from Njord.Messages** (moved to actor-internal records):
- `RequestPipelineSink`, `PipelineSinkResponse`, `PipelineSinkFailed`
- `RequestPipelineSource`, `PipelineSourceResponse`, `PipelineSourceFailed`
- `RequestEgressSink`, `EgressSinkResponse`, `EgressSinkFailed`
- `RequestEgressSource`, `EgressSourceResponse`, `EgressSourceFailed`
- `StopStreams`, `StreamsStopped`, `StreamsStopFailed`
- `FailureConsumerCompleted`, `FailureConsumerFailed`

**Added namespaces**:
- `Njord.Messages.Mqtt`: `MqttConnected`, `MqttDisconnected`, `HaBirthDetected`, `SubscribeInbound(IActorRef Listener)`
- `Njord.Messages.Enrichment`: `RecordSnapshot(ModelSnapshot)`, `QueryHistory`, `QueryHistoryResult`, `QueryHistoryFailed`

**New interfaces** (in `Njord.Messages`):
- `IWithLocation { string Location { get; } }`
- `IWithModelKey : IWithLocation { string ModelId { get; } }`
- `IWithEnrichmentKey : IWithLocation { string TypeName { get; } }`

Applied to: `UpdateForecast`, `QueryForecast`, `UpdateEnrichment`, `QueryEnrichment`, `RecordSnapshot`, `QueryHistory`, and shard-routed messages.

### D6: StreamConsumerActor simplification

**Decision**: Remove SinkRef tracking from the base class. Only SourceRef dependencies remain.

**What stays**: Dependency resolution via `ResolveDependencies()`, `AllRefsReady()`, `MaterializeGraph()`, DeathWatch, retry with backoff, KillSwitch management, Stash during WaitingForRefs.

**What goes**: No SinkRef request ID tracking, no SinkRef null checks. Subclasses no longer need paired Sink+Source handling per dependency.

**Result**: `ConfigureWaitingForRefs()` in each subclass shrinks because it only handles SourceRef responses (one response type per dependency instead of two).

## Risks / Trade-offs

**[Risk] Snapshot data loss during migration** → Mitigation: Snapshot data (ForecastSnapshot, EnrichmentSnapshot) is transient — rebuilt every poll cycle (~60 min). Old singleton PersistenceIds are retired; new ShardRegion entities start empty and fill on next poll. ForecastHistory data is persistent but per-location child actors already exist — migration to ShardRegion preserves the entity-per-location pattern with the same PersistenceId scheme.

**[Risk] Direct transport.SendAsync removes MQTT send ordering** → Mitigation: Each MQTT consumer actor runs `SelectAsync(1, ...)` which serializes sends. Multiple actors sending concurrently is acceptable — MQTT topics are partitioned by device, and retained messages are idempotent.

**[Risk] Large test churn** → Mitigation: Phase the work (messages first, then hubs, then shards, then StreamConsumer). Each phase is independently testable and committable. Architecture tests (LayerReferenceSpec, ZoneArchitectureSpec) are updated per phase.

**[Trade-off] More BroadcastHubs (2 → 3)**: Each producer owns a BroadcastHub. This adds one more materialized hub (ModelStateActor) compared to the central pattern. The overhead is negligible for Njord's throughput (~20 events/hour).

**[Trade-off] Consumer connects to multiple producers**: MqttStateActor and GrpcSnapshotConsumer need SourceRefs from both ModelStateActor and EnrichmentActor. This means 2 dependency resolutions per consumer instead of 1 (the former EgressActor hub). But it removes the SinkRef half, so net boilerplate decreases.

## Migration Plan

### Phase 1: Messages cleanup
1. Move SinkRef/SourceRef records from Njord.Messages to actor-internal records
2. Remove Akka.Streams PackageReference from Njord.Messages
3. Add Messages/Mqtt/ and Messages/Enrichment/ namespaces
4. Add IWithLocation, IWithModelKey, IWithEnrichmentKey interfaces
5. Apply marker interfaces to existing snapshot and enrichment messages
6. Update architecture tests for new namespaces

### Phase 2: Hub elimination + Producer-Broadcast
1. Add BroadcastHub + RequestModelStateSource to ModelStateActor
2. Add BroadcastHub + RequestEnrichmentSource to EnrichmentActor
3. Rewire MqttStateActor (rename + subscribe to producers directly)
4. Rewire MqttDiscoveryActor (rename + subscribe to ModelStateActor)
5. Rewire GrpcSnapshotConsumerActor to subscribe to both producers
6. Simplify PipelineActor (remove MergeHub, accept Tell)
7. Simplify PollSchedulerActor (Tell + SourceRef for feedback)
8. Delete EgressActor
9. Update NjordActorSystemSetup registrations
10. Update all affected specs

### Phase 3: ShardRegions
1. Add NjordMessageExtractor to Njord.Core
2. Convert ForecastHistoryActor to ShardRegion entity
3. Convert ForecastSnapshotActor to ShardRegion entity
4. Convert EnrichmentSnapshotActor to ShardRegion entity
5. Update GrpcSnapshotConsumerActor to route via ShardRegion
6. Update HistoryEnrichment to route via ShardRegion (remove ResolveChildActor)
7. Register ShardRegions in NjordActorSystemSetup
8. Retire old singleton PersistenceIds

### Phase 4: StreamConsumerActor cleanup
1. Remove SinkRef tracking from StreamConsumerActor base
2. Simplify ConfigureWaitingForRefs in all subclasses
3. Remove unused SinkRef-related fields and methods

### Rollback strategy
Each phase is independently deployable. If a phase causes issues, revert that phase's commits. No persistence DTO changes means journals/snapshots remain compatible across all phases.

## Open Questions

1. **ForecastHistoryActor PersistenceId migration**: Current child actors use PersistenceIds like `forecast-history-borken`. ShardRegion entities would use the same scheme — confirm this is compatible or if a migration step is needed.
2. **MqttConnectionActor HA birth notification path**: With MqttDiscoveryActor subscribing directly to ModelStateActor for capabilities, the HA birth event still needs to reach MqttDiscoveryActor. Current path: `MqttConnectionActor → SubscribeInbound → MqttInboundMessage`. This path stays — MqttDiscoveryActor still resolves MqttConnectionActor and calls `SubscribeInbound`. Confirm this doesn't create a circular dependency.
3. **Akka.Cluster.Sharding package**: Adding ShardRegion requires the `Akka.Cluster.Sharding` package. For single-node, this works with `akka.cluster.sharding.state-store-mode = ddata` and a self-join cluster. Verify this doesn't add unacceptable startup overhead.
