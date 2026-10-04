## Why

The actor architecture has grown complex: 14 production actors interconnected through 3 hub actors (PipelineActor, EgressActor, MqttConnectionActor-as-hub) using SinkRef/SourceRef handshakes, each requiring ~60 lines of resolve/retry boilerplate via StreamConsumerActor. The SchedulerActor is a 500-LOC monolith where 4 of 5 Become phases exist solely for pipeline connection ceremony. Shared messages contain Akka.Streams infrastructure types (`ISinkRef<T>`, `ISourceRef<T>`), domain boundaries are inconsistent (ModelStateActor sits in "Egress" but does processing), and entity actors (snapshots, history) use ad-hoc patterns instead of the ShardRegion routing established in other projects (FunkArr).

## What Changes

- **BREAKING**: Eliminate EgressActor (hub) entirely — consumers subscribe directly to producers
- **BREAKING**: Simplify PipelineActor — remove MergeHub, receive `ScheduledPoll` via Tell, keep only BroadcastHub for output
- **BREAKING**: Remove all `ISinkRef`/`ISourceRef` records from `Njord.Messages` (10 records) and the `Akka.Streams` package dependency — stream wiring becomes actor-internal
- Introduce producer-broadcast pattern: ModelStateActor and EnrichmentActor each own a BroadcastHub and serve `RequestXxxSource` to consumers
- Simplify PollSchedulerActor: Tell-based connection to PipelineActor eliminates 4 Become phases (~300 LOC)
- Add `Messages/Mqtt/` namespace (move `MqttConnected`, `SubscribeInbound`, `HaBirthDetected` from actor-internal to shared messages)
- Add `Messages/Enrichment/` namespace (move `RecordSnapshot`, `QueryHistory`/Response from actor-internal)
- Rename MqttEgressActor → MqttStateActor, DiscoveryActor → MqttDiscoveryActor for domain clarity
- Convert entity actors to ShardRegions with passivation: ForecastHistoryActor (per location), ForecastSnapshotActor (per location|modelId), EnrichmentSnapshotActor (per location|typeName)
- Add `IWithLocation`, `IWithModelKey`, `IWithEnrichmentKey` marker interfaces on messages for ShardRegion routing
- Add `NjordMessageExtractor` (FunkArr-style `HashCodeMessageExtractor`)
- Simplify StreamConsumerActor: remove SinkRef tracking, only SourceRef dependencies remain

## Non-goals

- Removing Akka.Streams from the data pipeline — streams stay as the data transport
- Changing the enrichment feature interface hierarchy (IStatelessEnrichment, IStatefulEnrichment, IActorEnrichment)
- Altering polling frequency, budget logic, or Open-Meteo API interaction — no API-budget impact
- Multi-node clustering — ShardRegions are introduced for clean entity routing and passivation, not for distribution
- Changing persistence DTOs or wire formats — the `Njord.Persistence` layer is untouched
- Replacing `object Result` in `EnrichmentUpdate` with generic typing (separate change)

## Capabilities

### New Capabilities
- `producer-broadcast-pattern`: Each producing actor (PipelineActor, ModelStateActor, EnrichmentActor) owns a BroadcastHub and serves SourceRefs to consumers — replacing the central hub actor topology
- `shard-region-entities`: ShardRegion routing for entity actors (ForecastHistory, ForecastSnapshot, EnrichmentSnapshot) with IWithLocation/IWithModelKey/IWithEnrichmentKey markers and NjordMessageExtractor
- `message-domain-separation`: FunkArr-style message cleanup — remove infrastructure types from shared messages, add Mqtt/ and Enrichment/ namespaces, actor-internal wiring records

### Modified Capabilities
- `stream-consumer-actor`: Remove SinkRef tracking and SinkRef-related boilerplate from the base class; only SourceRef dependencies remain
- `pipeline-actor`: Remove MergeHub and SinkRef API; receive ScheduledPoll via Tell; simplified to BroadcastHub-only producer
- `poll-scheduler`: Eliminate pipeline connection ceremony (4 Become phases); Tell-based connection to PipelineActor; simplified to scheduling + persistence + hash tracking
- `mqtt-actor-topology`: Rename MqttEgressActor → MqttStateActor, DiscoveryActor → MqttDiscoveryActor; both subscribe directly to producer BroadcastHubs instead of EgressActor hub
- `egress-stream-graph`: EgressActor hub eliminated; ModelStateActor owns its own BroadcastHub<EgressEvent>
- `snapshot-actors`: Convert from singleton actors with Dictionary state to ShardRegion entities with per-key PersistenceId
- `message-conventions`: Add IWithLocation/IWithModelKey/IWithEnrichmentKey routing markers; add Mqtt/ and Enrichment/ message namespaces

## Impact

- **Projects modified**: Njord.Messages, Njord.Core, Njord.Pipeline, Njord.Egress, Njord.Enrichment, Njord.Mqtt, Njord.Grpc, Njord (host registration)
- **Actors removed**: EgressActor
- **Actors renamed**: MqttEgressActor → MqttStateActor, DiscoveryActor → MqttDiscoveryActor
- **Actors simplified**: PollSchedulerActor (~500 → ~200 LOC), PipelineActor (~160 → ~80 LOC)
- **New infrastructure**: NjordMessageExtractor, 3 ShardRegion registrations in NjordActorSystemSetup
- **Test impact**: All actor specs that interact with EgressActor hub need rewiring; StreamConsumerActor specs need SinkRef removal; ShardRegion specs need TestKit sharding support; architecture tests (LayerReferenceSpec, ZoneArchitectureSpec) may need updates for new message namespaces
- **Package dependencies**: Akka.Streams removed from Njord.Messages; Akka.Cluster.Sharding added to Njord.Core (for MessageExtractor) and Njord (host, for WithShardRegion)
- **Wire compatibility**: No persistence DTO changes — existing journals/snapshots remain compatible
