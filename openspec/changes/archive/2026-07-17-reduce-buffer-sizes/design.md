## Context

Akka.Streams `BroadcastHub` pre-allocates a ring buffer of the specified size. Each slot retains a reference to the element until all consumers have read it. With large buffers and large elements, the retained references dominate memory even when the buffer is mostly empty (the runtime still allocates the array).

Current buffer sizes were set conservatively during initial development. Live analysis shows consumers drain within milliseconds — buffers never approach capacity.

## Goals / Non-Goals

**Goals:**
- Reduce BroadcastHub buffer sizes to match actual usage.
- Replace `ModelSnapshot.Update()` dictionary clone with `ImmutableDictionary` for structural sharing.

**Non-Goals:**
- Changing stream topology or backpressure behavior.
- Optimizing `ForecastPoint` internals (separate change).

## Decisions

### D1: Buffer sizes

| Hub | Current | New | Rationale |
|-----|---------|-----|-----------|
| PipelineActor BroadcastHub (`FetchOutcome`) | 256 | 16 | One poll cycle produces ~20 outcomes; 16 gives headroom without retaining old cycles |
| EnrichmentActor BroadcastHub (`ModelSnapshot`) | 64 | 8 | ModelSnapshot is the largest object (~35 MB at scale); 8 is plenty for consumer catch-up |
| EgressActor BroadcastHub (`EgressEvent`) | 64 | 16 | EgressEvents are smaller but numerous; 16 matches the MergeHub producer buffer |

### D2: `ModelSnapshot.Update()` uses `ImmutableDictionary`

Replace:
```csharp
var dict = new Dictionary<...>(Entries) { [key] = forecast };
```
With:
```csharp
var dict = Entries.ToImmutableDictionary().SetItem(key, forecast);
```

Or better: store `_entries` as `ImmutableDictionary` internally, so `SetItem` returns a new tree sharing all unchanged nodes. The `Empty` sentinel uses `ImmutableDictionary.Empty` instead of `FrozenDictionary.Empty`.

This eliminates the O(n) copy per update. With 20 entries of ~1.75 MB each, every update saves a ~35 MB allocation.

## Risks / Trade-offs

- **[Smaller buffers could cause DropHead under load]** → BroadcastHub uses `DropHead` when full. With 16 slots and consumers processing in <10ms, a producer would need to emit 16 elements faster than any consumer reads — practically impossible with HTTP-bound API calls taking 30–100ms each.
- **[ImmutableDictionary lookup is O(log n) vs O(1)]** → With 20 entries, the difference is negligible. `ImmutableDictionary` is well-optimized for small collections in .NET.
