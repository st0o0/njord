## Why

The GrpcConsumer actor builds Proto types inside its Akka Streams BroadcastHub, coupling the
actor to the wire format and violating the api-adapter-boundary convention. Additionally,
ManifestBuilder and EntityIdLookup solve overlapping problems as separate classes (EntityIdLookup
uses reflection to match result types), and the consumer has no knowledge of the full entity set,
so non-triggered alerts are missing from the Subscribe snapshot and appear as "unavailable" in
ha-njord instead of "off".

## What Changes

- **Introduce EntityRegistry** as a DI singleton that replaces both ManifestBuilder and
  EntityIdLookup. Name-based lookup (FeatureDescriptor.Name + Location), no reflection.
- **Introduce ConsumerEvent domain types** (`ConsumerSnapshot`, `EntityStateChanged`,
  `EntityBecameUnavailable`, `EntityBecameAvailable`) that replace `Proto.EntityEvent` in the
  consumer's BroadcastHub.
- **Move Proto mapping to the gRPC boundary**: `NjordGrpcService.Subscribe()` maps
  `ConsumerEvent -> Proto.EntityEvent` via a pure `.Select()` stage. The existing per-OutputKind
  mapping extensions stay unchanged.
- **Seed full entity state on startup**: GrpcConsumer receives EntityRegistry and initializes
  `_state` with default results for all registered entities (alerts Severity.None, conditions
  Active=false, etc.). The Subscribe snapshot is always complete.
- **Delete EntityIdLookup and ManifestBuilder**: replaced by EntityRegistry and a thin
  manifest-mapping extension.
- **BREAKING**: `RequestEntityStreamCompleted.SourceRef` changes from
  `ISourceRef<Proto.EntityEvent>` to `ISourceRef<ConsumerEvent>`. Internal to Njord.Grpc, no
  wire-format change.

## Non-goals

- Changing the pipeline connection (SourceRef<PipelineEvent> via RequestPipelineOutput).
- Changing the proto contract (njord.proto stays identical).
- Changing the per-OutputKind value mapping extensions (AlertValue, ScoreValue, etc.).
- Any polling or budget changes (zero API-budget impact).

## Capabilities

### New Capabilities

- `entity-registry`: Unified DI service for entity registration, ID lookup, and manifest
  building. Replaces ManifestBuilder + EntityIdLookup with name-based resolution.
- `consumer-domain-events`: Domain-typed event model for the GrpcConsumer BroadcastHub,
  removing Proto types from the actor/stream layer.

### Modified Capabilities

- `unified-entity-stream`: The Subscribe snapshot now includes default states for all
  registered entities, not only those that have received a result.
- `api-adapter-boundary`: Proto mapping moves from GrpcConsumer (actor) to NjordGrpcService
  (gRPC boundary), enforcing the adapter pattern.

## Impact

- **Njord.Grpc**: Major refactor. EntityRegistry replaces two classes, GrpcConsumer hub type
  changes, NjordGrpcService gains mapping responsibility, new ConsumerEvent types.
- **Njord.Grpc.Tests**: All GrpcConsumer tests, EntityIdLookup tests, and ManifestBuilder tests
  need updates. FakePipeline stays.
- **Njord.Messages**: `PipelineStreamMessages.cs` SourceRef type changes (internal).
- **ha-njord**: No code change needed. Alerts that were "unavailable" will now show as "off"
  (Severity.None maps to binary_sensor off). Conditions will show as "off" (Active=false).
- **Wire format**: No change to njord.proto. Same gRPC contract.
