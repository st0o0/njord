## Context

The service runs at ~550 MB RAM for 3 locations × ~9 models. Profiling
identified several structural inefficiencies: fetch-downstream coupling in the
poll pipeline, BroadcastHub per-consumer buffer amplification in enrichment,
full-payload dedup in MQTT egress, and unbounded persistence journal growth.
None are leaks, but together they inflate working set and GC pressure.

## Goals / Non-Goals

**Goals:**
- Decouple HTTP fetch throughput from downstream processing speed.
- Reduce GC pressure from BroadcastHub buffer amplification.
- Eliminate multi-MB string retention in MQTT dedup cache.
- Prevent unbounded persistence journal growth.

**Non-Goals:**
- Replacing `ImmutableDictionary` in `ModelSnapshot` (larger refactor).
- Switching GC mode (runtime decision, orthogonal).
- Changing poll parallelism or budget parameters.

## Decisions

### 1. Pipeline: Buffer(32) before BroadcastHub, hub bufferSize 16 → 2

The buffer decouples `SelectAsyncUnordered(2)` from downstream backpressure.
Size 32 covers a full poll cycle (~28 targets) with margin. The hub's own
buffer drops from 16 to 2 since the upstream buffer absorbs bursts and the
hub only serves two consumer paths (feedback + egress/model-state).

Memory impact: 32 × ~194 KB = ~6 MB max (negligible vs current overhead).

**Why not increase SelectAsync parallelism?** The bottleneck is downstream
consumption, not HTTP concurrency. Adding a buffer solves the coupling; more
parallelism would just fill the buffer faster without helping throughput.

### 2. Enrichment: Buffer(8) before BroadcastHub, hub bufferSize 8 → 1

```
Before: Scan → BroadcastHub(8) → 7 consumers  = 7×8 = 56 snapshot refs
After:  Scan → Buffer(8) → BroadcastHub(1) → 7 consumers = 8 + 7×1 = 15 refs
```

The buffer before the hub absorbs tempo differences. The hub at 1 means
the slowest consumer gates the pipeline — but the buffer provides 8 elements
of slack so the Scan stage isn't blocked by individual consumer hiccups.

**Why not `bufferSize: 0`?** BroadcastHub requires `bufferSize >= 1`.

### 3. Egress: BroadcastHub bufferSize 16 → 4

The EgressActor's hub carries `EgressEvent`s (small messages, not full
snapshots). 16 was generous; 4 is sufficient with the upstream buffers
absorbing bursts.

### 4. Dedup by hash instead of full payload

Replace `Dictionary<string, string>` (topic → JSON payload) with
`Dictionary<string, int>` (topic → `string.GetHashCode()`). Hash collisions
are theoretically possible but practically irrelevant for dedup (worst case:
one redundant MQTT publish). Saves ~5-15 MB of retained JSON strings.

**Why not SHA256?** GetHashCode is sufficient for dedup (not security-critical).
Collision rate for weather JSON payloads is negligible.

### 5. Journal trimming on SaveSnapshotSuccess

After a successful snapshot save, call `DeleteMessages(snapshotSequenceNr)` to
trim journal events that are now covered by the snapshot. Without this, the
journal grows indefinitely and slows recovery (all events replayed then
filtered by the snapshot).

Also delete old snapshots with `DeleteSnapshots` to keep only the latest one.

## Risks / Trade-offs

**[Enrichment hub bufferSize 1 means slowest consumer gates all]** → Mitigated
by the Buffer(8) before the hub. If a single enrichment feature is consistently
slow, it affects others — but this was already true with bufferSize 8, just
delayed. The real fix for a slow feature is fixing the feature, not buffering.

**[Hash collision in dedup yields one redundant MQTT publish]** → Acceptable.
Weather payloads change frequently anyway; one extra publish has zero user
impact.

**[DeleteMessages on snapshot success adds IO]** → One journal truncation per
snapshot interval (default: every N events). The cost is negligible compared
to the journal growth it prevents.
