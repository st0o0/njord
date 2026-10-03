## Context

njord has 7 enrichment types (Consensus, Alerts, Derived, Trends, Indices,
Energy, History) that are wired through identical patterns across 8+ files.
Each file contains a manual enumeration of all types — if-chains,
switch-arms, parallel method sets — that must stay in sync. There is no
compiler enforcement; missing a location is a silent bug. The
DiscoveryPayloadBuilder alone is 740 lines with 8 copies of the same device
envelope JSON. Adding a new enrichment type touches every layer from
configuration through MQTT egress.

Two of the seven types are structural outliers:
- **Trends** needs the previous `ModelSnapshot` for diff-based computation.
- **History** needs per-location child actors and `Ask`-based communication
  (currently with a blocking `.Result` bug on the stream thread).

## Goals / Non-Goals

**Goals:**

- A single type hierarchy (`IEnrichmentFeature`) that makes each enrichment
  self-contained: adding a new type = implementing one class + DI registration.
- Eliminate all 7-fold if-chains and switch-arms in EnrichmentActor,
  DiscoveryActor, and MqttEgressActor.
- Move discovery payload building and state message mapping into feature
  classes, eliminating the parameter-threading problem.
- Extract the device envelope boilerplate into a single helper.
- Fix the blocking `.Result` in the History stream.
- Fix `DateTimeOffset.UtcNow` in `ForecastHistoryActor`.

**Non-Goals:**

- Changing the actual compute algorithms (AlertEvaluator, IndexScorer, etc.).
- Replacing string-based parameter lookups with typed access.
- Restructuring domain-to-configuration coupling.
- Modifying the per-model forecast pipeline (`PerModelUpdate`).

## Decisions

### Decision 1: Three-interface hierarchy, not one

```
IEnrichmentFeature (base)
├─ string TypeName
├─ bool Enabled
├─ string DeviceId(string location)
├─ string BuildDiscoveryPayload(DiscoveryContext ctx, string location)
├─ IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic)
│
├── IStatelessEnrichment<TResult> : IEnrichmentFeature
│   └─ IEnumerable<EgressEvent> Compute(ModelSnapshot snapshot,
│                                        IReadOnlyList<string> locations)
│   (5 types: Consensus, Alerts, Derived, Indices, Energy)
│
├── IStatefulEnrichment<TResult> : IEnrichmentFeature
│   └─ IEnumerable<EgressEvent> Compute(ModelSnapshot snapshot,
│                                        ModelSnapshot? previous,
│                                        IReadOnlyList<string> locations)
│   (1 type: Trends)
│
└── IActorEnrichment : IEnrichmentFeature
    └─ void Materialize(Source<ModelSnapshot, NotUsed> source,
                        Sink<EgressEvent, NotUsed> sink,
                        IMaterializer mat,
                        IUntypedActorContext context)
    (1 type: History)
```

**Why not one interface with a Strategy:** Trends and History are
fundamentally different — Trends needs state (previous snapshot), History
needs actors. A single interface with optional `previous` parameter and
optional `Materialize` method would be a leaky abstraction that every
consumer must null-check. Three interfaces make the contract explicit: the
EnrichmentActor handles each variant differently because they ARE different.

**Why `IEnumerable<EgressEvent>` instead of `TResult`:** The compute method
iterates over all locations and produces one `EgressEvent` per location.
Returning the list directly means the EnrichmentActor doesn't need to know
about locations — the feature handles it.

### Decision 2: Dependencies via constructor injection, not method params

Today: `ConsensusResult.Compute(snapshot, parameters, horizons, location,
timeProvider, trimPercent)` — 6 parameters, 4 of which never change.

After: `ConsensusEnrichment` receives `ResolvedParameterSet`, horizons,
`TimeProvider`, and `ConsensusOptions` via DI constructor. The `Compute`
method only takes `(snapshot, locations)` — what actually varies per call.

This eliminates the parameter-threading problem where `(parameters, horizons,
mqtt, pollInterval, version)` is passed through multiple layers.

### Decision 3: DiscoveryContext record

```csharp
public sealed record DiscoveryContext(
    string Location,
    MqttOptions Mqtt,
    TimeSpan PollInterval,
    string Version);
```

Replaces the 4 individual parameters that every `BuildDiscoveryPayload`
call needs. Constructed once in DiscoveryActor, passed to each feature.

### Decision 4: EgressEvent.EnrichmentUpdate replaces 7 records

```csharp
public abstract record EgressEvent
{
    public sealed record PerModelUpdate(...) : EgressEvent;  // unchanged
    public sealed record EnrichmentUpdate(
        string Location,
        string TypeName,
        object Result) : EgressEvent;
}
```

The `MqttEgressActor` no longer pattern-matches on the concrete result type.
Instead, it looks up the `IEnrichmentFeature` by `TypeName` and calls
`feature.ToStateMessages(result, baseTopic)`. This inverts the dependency:
features know how to serialise themselves.

**Why `object Result` instead of generics:** The `EgressEvent` flows through
Akka.Streams hubs (`MergeHub<EgressEvent>`, `BroadcastHub<EgressEvent>`)
which require a single concrete element type. Generic covariance on records
would require `EgressEvent` to be an interface, breaking the existing stream
graph types. `object` is the pragmatic choice — the feature class that
created the result also consumes it in `ToStateMessages`, so the cast is
always safe.

### Decision 5: Device envelope helper in DiscoveryPayloadBuilder

```csharp
public static string BuildDeviceEnvelope(
    string deviceId,
    string location,
    string typeLabel,
    string version,
    JsonObject components)
```

The 8 copies of the `{"dev":..., "o":..., "qos":1, "cmps":...}` structure
become a single call. Each feature's `BuildDiscoveryPayload` constructs its
`components` JsonObject and delegates to this helper.

### Decision 6: TopicScheme parameterisation

The 14 type-specific methods collapse to:

```csharp
static string EnrichmentDeviceId(string location, string typeName)
    => $"njord_{Slug(location)}_{typeName}";

static string EnrichmentTopic(string baseTopic, string location, string typeName)
    => $"{baseTopic}/{Slug(location)}/{typeName}";
```

Features that need sub-topics (Consensus with horizons, Alerts with alert
types, Derived with horizons + meta) build them in their own
`BuildDiscoveryPayload` / `ToStateMessages` using these base methods.

The existing per-model `DeviceId(location, model)` and `ConfigTopic` stay
unchanged — they are not enrichment methods.

### Decision 7: History fixes

**Blocking `.Result`:** `IActorEnrichment.Materialize` gives History full
control over stream wiring. It uses `SelectAsync` instead of `.Result`:

```csharp
source.SelectAsync(1, async snapshot => { ... await actor.Ask<>(...) ... })
```

**`DateTimeOffset.UtcNow`:** `ForecastHistoryActor` receives `TimeProvider`
via constructor injection, replacing the two direct `DateTimeOffset.UtcNow`
calls.

### Decision 8: DI registration pattern

```csharp
services.AddSingleton<IEnrichmentFeature, ConsensusEnrichment>();
services.AddSingleton<IEnrichmentFeature, AlertEnrichment>();
// ... etc.
```

The `EnrichmentActor`, `DiscoveryActor`, and `MqttEgressActor` receive
`IEnumerable<IEnrichmentFeature>` via DI. Order does not matter —
features are identified by `TypeName`.

## Risks / Trade-offs

- **[Casting] `object Result` in `EnrichmentUpdate`** → Each feature casts
  back to its concrete result type in `ToStateMessages`. The cast is safe
  because the same feature class produces and consumes the result. A wrong
  cast would throw `InvalidCastException` immediately, not silently fail.

- **[Snapshot testing] Discovery payloads will change formatting** → The
  existing Verify snapshots for DiscoveryPayloadBuilder will need to be
  re-approved. The content is semantically identical but the code path is
  different. Mitigated by re-running snapshot tests and verifying diff.

- **[History complexity] `IActorEnrichment.Materialize` is a power API** →
  History gets full control over stream wiring, which is more responsibility
  than the other features. This is acceptable because History IS fundamentally
  more complex (child actors, persistence, ask-based queries).

- **[Breaking existing tests] Many test files will need updates** → The
  EgressEvent type changes affect every test that constructs or matches on
  `ConsensusUpdate`, `AlertUpdate`, etc. This is unavoidable but mechanical.
