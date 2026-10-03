## Why

Memory analysis of the live container (950 MB after 80 min) identified oversized BroadcastHub buffers and a full-dictionary clone in `ModelSnapshot.Update()` as major contributors. The Pipeline BroadcastHub buffers 256 `FetchOutcome` objects (each wrapping a ~1.75 MB `ModelForecast`), the EnrichmentActor BroadcastHub buffers 64 `ModelSnapshot` objects (each ~35 MB with 20 forecasts), and every `ModelSnapshot.Update()` allocates a new `Dictionary` copying all entries. These buffers rarely fill up — consumers process faster than producers — but the allocated capacity still pressures the GC and inflates resident memory.

## What Changes

- **Reduce `PipelineActor` BroadcastHub buffer** from 256 to 16.
- **Reduce `EnrichmentActor` ModelSnapshot BroadcastHub buffer** from 64 to 8.
- **Reduce `EgressActor` BroadcastHub buffer** from 64 to 16.
- **`ModelSnapshot.Update()`: use `ImmutableDictionary`** instead of cloning a mutable `Dictionary` on every update. Structural sharing means only the changed entry is allocated, not a full copy.

## Non-goals

- Changing `ForecastPoint` to use arrays instead of dictionaries — that's a separate, larger change.
- Changing MergeHub `perProducerBufferSize` — those are already small (8–16).
- No API budget impact.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `pipeline-actor`: BroadcastHub buffer size reduced.
- `enrichment-actor`: ModelSnapshot BroadcastHub buffer size reduced.
- `egress-event`: EgressActor BroadcastHub buffer size reduced.

## Impact

- **Memory**: estimated reduction of 100–300 MB from smaller buffer allocations and less GC pressure from `ImmutableDictionary` structural sharing.
- **Throughput**: no impact — consumers process within milliseconds, buffers never fill to current capacity.
- **Tests**: no functional changes — buffer sizes are internal stream configuration.
