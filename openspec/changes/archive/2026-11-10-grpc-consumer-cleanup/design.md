## Context

The GrpcConsumer actor is the bridge between the analysis pipeline and gRPC subscribers.
It receives `PipelineEvent`s via a SourceRef from `ForecastPipeline`, maintains entity state,
and serves `Subscribe` requests through a BroadcastHub.

Current problems:
- The BroadcastHub carries `Proto.EntityEvent` (wire-format types inside the actor layer)
- `ManifestBuilder` and `EntityIdLookup` are separate classes with overlapping concerns
- `EntityIdLookup` uses reflection (`Assembly.GetTypes()` + name-suffix matching)
- The consumer only knows entities that have produced results; non-triggered alerts are
  missing from the snapshot and show as "unavailable" in ha-njord

## Goals / Non-Goals

**Goals:**
- Proto types exclusively at the gRPC service boundary
- Single DI service for entity registration, lookup, and manifest data
- Complete entity snapshot on Subscribe (including default states for non-triggered features)
- No reflection in entity ID resolution
- Stream stages remain pure (no closures on mutable actor state)

**Non-Goals:**
- Changing the pipeline connection (SourceRef<PipelineEvent> via RequestPipelineOutput)
- Changing the proto contract (njord.proto)
- Changing per-OutputKind value mapping extensions (AlertValue, ScoreValue, etc.)
- Any polling or budget changes

## Decisions

### D1: EntityRegistry as singleton DI service

**Choice**: Replace `ManifestBuilder` + `EntityIdLookup` with a single `EntityRegistry`.

**Why over keeping both**: ManifestBuilder knows features + locations + models to build entity
definitions. EntityIdLookup knows features + locations to map results to IDs. Same inputs,
same iteration, two classes. Merging eliminates the duplication and the reflection hack.

**Lookup strategy**: Two dictionaries keyed by `(string FeatureName, string LocationName)`.
One for observations (which share `ObservationResult` and need `FeatureName` dispatch), one
for everything else (keyed by `FeatureDescriptor.Name`). The `Find(FeatureResult)` method
extracts the feature name from the result's owning descriptor via a registry built at
startup -- no runtime reflection.

**How Find works without reflection**: At construction time, for each enabled feature, the
registry maps `FeatureDescriptor.Name` to the entity ID. At lookup time, we need to go from
a `FeatureResult` instance back to its feature name. Two approaches:

- Observations: already carry `FeatureName` on the result type.
- Other results: the registry builds a second dictionary `Dictionary<Type, string>` mapping
  each concrete result CLR type to its feature name. This is built once at startup by
  scanning the enabled descriptors (not `Assembly.GetTypes()`). `Find()` does
  `_typeToFeature[result.GetType()]` then looks up the entity ID.

This is still type-based for non-observations, but the type set is known at startup from the
DI-registered rules, not discovered via reflection on the assembly.

**Registration record**:
```csharp
sealed record EntityRegistration(
    string EntityId,
    string Location,
    FeatureDescriptor Descriptor);
```

**Manifest mapping**: A pure extension `EntityRegistration.ToEntityDefinition()` replaces the
`BuildDefinition` method. Called by `NjordGrpcService.GetManifest()`.

### D2: ConsumerEvent domain types

**Choice**: New abstract record hierarchy in `Njord.Grpc`:

```csharp
abstract record ConsumerEvent;
sealed record ConsumerSnapshot(IReadOnlyList<EntityState> States) : ConsumerEvent;
sealed record EntityStateChanged(string EntityId, FeatureResult Result, DateTimeOffset UpdatedAt) : ConsumerEvent;
sealed record EntityBecameUnavailable(string EntityId, DateTimeOffset Since) : ConsumerEvent;
sealed record EntityBecameAvailable(string EntityId, DateTimeOffset RecoveredAt) : ConsumerEvent;
```

**Why in Njord.Grpc, not Njord.Messages**: These are consumer-internal types. They reference
`FeatureResult` (from Messages) and `EntityState` (from Grpc). No other project needs them.

**Why not reuse PipelineEvent**: PipelineEvent is the pipeline's output contract
(domain data + feature results). ConsumerEvent is the consumer's output contract (entity
state + availability). Different concerns, different lifecycles.

### D3: BroadcastHub carries ConsumerEvent

**Before**: `Source.ActorRef<Proto.EntityEvent>` -> `BroadcastHub.Sink<Proto.EntityEvent>`
**After**: `Source.ActorRef<ConsumerEvent>` -> `BroadcastHub.Sink<ConsumerEvent>`

The consumer's `_entityHubInput` tells domain events. `NjordGrpcService.Subscribe()` receives
`ISourceRef<ConsumerEvent>` and maps via `.Select(e => e.ToProto())`.

The SourceRef type change in `EntityStreamMessages.cs`:
```csharp
sealed record RequestEntityStreamCompleted(long RequestId, ISourceRef<ConsumerEvent> SourceRef)
```

### D4: Default entity state seeding

In `PreStart`, after materializing the hub, the consumer seeds `_state` from `EntityRegistry`:

```
foreach entity in registry.All:
    create a default FeatureResult for entity.Descriptor.Output
    state = state.ApplyEntity(entity.EntityId, defaultResult, startTime)
```

Default results per OutputKind:
- Alert: `AlertSeverity.None` (maps to binary_sensor "off" in ha-njord)
- Condition: `Active = false`
- Score: score 0
- Observation: value 0
- Weather: skip (requires real data to be meaningful)
- Trend/Forecast: skip (empty extra maps are meaningless)

When real results arrive, they overwrite the defaults via the existing `ApplyEntity` path.

**How to create default results**: Each OutputKind base type needs a static factory or the
registry needs to know how to create one. Simplest: a `DefaultResult` static method on each
base result type, or a switch in `GrpcConsumerStateExtensions` that creates the default
for a given `OutputKind` and `Location`. The latter keeps the knowledge local.

### D5: Proto mapping at the boundary

New `ConsumerEventMappingExtensions.ToProto()`:

```csharp
static Proto.EntityEvent ToProto(this ConsumerEvent evt) => evt switch
{
    ConsumerSnapshot snap => new Proto.EntityEvent { Snapshot = snap.ToProtoSnapshot() },
    EntityStateChanged changed => new Proto.EntityEvent { StateUpdate = changed.Result.ToEntityStateUpdate(changed.EntityId, changed.UpdatedAt) },
    EntityBecameUnavailable u => new Proto.EntityEvent { Unavailable = ... },
    EntityBecameAvailable a => new Proto.EntityEvent { Available = ... },
    _ => throw new ArgumentOutOfRangeException(...)
};
```

This calls the existing `ToEntityStateUpdate()` which dispatches to per-OutputKind mappers.
The mapping chain stays the same, just invoked at the boundary instead of in the actor.

`NjordGrpcService.Subscribe()`:
```csharp
var sourceRef = ...;  // ISourceRef<ConsumerEvent>
await sourceRef.Source
    .Select(e => e.ToProto())
    .Select(e => wanted is null ? e : Filter(e, wanted))
    .Where(e => e is not null)
    ...
```

### D6: HandleEntityStreamRequest builds domain snapshot

Current: builds `Proto.EntitySnapshot` in the Receive handler.
New: builds `ConsumerSnapshot` with `IReadOnlyList<EntityState>` from `_state.EntityStates`.

```csharp
var snapshot = new ConsumerSnapshot(_state.EntityStates.Values
    .OrderBy(e => e.EntityId, StringComparer.Ordinal)
    .ToList());
var staleEvents = _state.StaleEntities.Order(...)
    .Select(id => (ConsumerEvent)new EntityBecameUnavailable(id, _state.EntityStates[id].LastUpdated))
    .ToList();

var initial = new List<ConsumerEvent> { snapshot };
initial.AddRange(staleEvents);

Source.From(initial).Concat(_entityHubSource)...
```

All state access happens in the Receive handler. The stream only sees the captured list.

## Risks / Trade-offs

- **[Larger diff]** Touching GrpcConsumer, NjordGrpcService, EntityIdLookup, ManifestBuilder,
  EntityStreamMessages, and all their tests in one change. Mitigated by: tests are
  comprehensive, the wire format does not change, and each piece is independently testable.
- **[Default results may mislead]** A score of 0 or observation of 0 before real data arrives
  could confuse consumers. Mitigated by: ha-njord can check `updated_at` and treat very old
  timestamps as "pending". Weather/Trend/Forecast skip defaults entirely.
- **[Type-based lookup remains for non-observations]** The registry still maps CLR types to
  feature names, but from a known set (DI-registered rules), not from assembly scanning.
  Cleaner than reflection but not fully name-based. Acceptable because the type is the
  natural identity for non-observation results (each feature has a unique result type).

## Open Questions

- Should ha-njord treat default-valued entities differently (e.g. via the `updated_at`
  timestamp being the service start time)? Not blocking for this change.
