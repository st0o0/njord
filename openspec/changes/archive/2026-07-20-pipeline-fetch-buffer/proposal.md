## Why

The service uses ~550 MB RAM, more than expected for a weather poller. Two
structural issues contribute:

1. **Fetch-downstream coupling**: `SelectAsyncUnordered(2)` in the poll pipeline
   is backpressured by downstream processing (~1.5s per slot instead of ~50ms
   HTTP roundtrip), making initial discovery take ~42s instead of ~5s.

2. **BroadcastHub buffer amplification**: The enrichment pipeline's
   `BroadcastHub(bufferSize: 8)` allocates 8 buffered `ModelSnapshot`s **per
   consumer** (7 features). With each snapshot holding all 28 forecasts (~5 MB),
   this holds up to 56 snapshot references alive for GC. The
   `ImmutableDictionary` churn in the `Scan` stage compounds this — 28 new
   immutable tree nodes per poll cycle, kept alive by the per-consumer buffers.

3. **Dedup cache stores full payloads**: The MQTT egress `lastPublished` dict
   caches the full JSON string per topic for deduplication. With thousands of
   topics, this is ~5-15 MB of strings that could be hashes instead.

4. **Journal never trimmed**: `ForecastHistoryActor` saves snapshots but never
   calls `DeleteMessages` afterward. The journal grows unbounded on disk and
   slows recovery.

## What Changes

- **Pipeline buffer**: Add `Buffer(32, Backpressure)` between
  `SelectAsyncUnordered` and BroadcastHub in `PipelineActor`.
- **Enrichment hub restructure**: Add `Buffer(8, Backpressure)` before the
  enrichment `BroadcastHub`, reduce hub `bufferSize` from 8 to 1. Cuts
  max snapshot references from 56 to 15.
- **Pipeline hub reduction**: Reduce PipelineActor's `BroadcastHub` bufferSize
  from 16 to 2 (the new Buffer(32) absorbs the fetch burst).
- **Egress hub reduction**: Reduce EgressActor's `BroadcastHub` bufferSize
  from 16 to 4.
- **Dedup by hash**: Replace `lastPublished` from `Dictionary<string, string>`
  (topic→JSON) to `Dictionary<string, int>` (topic→hash code), eliminating
  multi-MB string retention.
- **Journal trimming**: Call `DeleteMessages` on `SaveSnapshotSuccess` in
  `ForecastHistoryActor`.

## Non-goals

- Changing `SelectAsyncUnordered` parallelism (stays at 2).
- Changing budget rate or token-bucket parameters.
- Replacing `ImmutableDictionary` in `ModelSnapshot` (larger refactor).
- Switching Server GC to Workstation GC (runtime config, separate decision).

## API-budget impact

No change. Same requests per cycle; they complete faster. The token-bucket
remains the governing constraint.

## Capabilities

### New Capabilities

_(none)_

### Modified Capabilities

- `poll-pipeline`: Buffer stage added to the fetch→broadcast segment.

## Impact

- `src/Njord/Pipeline/PipelineActor.cs` — pipeline graph: Buffer + reduced hub size.
- `src/Njord/Enrichment/EnrichmentActor.cs` — enrichment graph: Buffer before hub, hub size 1.
- `src/Njord/Egress/EgressActor.cs` — egress hub bufferSize 16 → 4.
- `src/Njord/Mqtt/MqttEgressActor.cs` — `lastPublished` to hash-based dedup.
- `src/Njord/Enrichment/ForecastHistoryActor.cs` — journal trim on snapshot success.
