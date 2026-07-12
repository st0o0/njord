## Context

njord currently polls all weather models at a single fixed interval (default 60 min) via a `Source.Tick` → `PollAll` → fan-out pipeline. Weather models update at different cadences (ICON-D2 every 3h, ECMWF every 6h, UKMO every 6-12h), so the majority of requests return unchanged data. The Open-Meteo API does not expose a public "last updated" timestamp — the only way to detect a model update is to compare the returned forecast data.

The existing architecture has three actors: `PipelineActor` (owns the stream graph), `MqttEgressActor` (owns the MQTT connection and egress stream), and no persistence layer. The pipeline stream uses a `MergeHub` entry point with an attached `Source.Tick`.

## Goals / Non-Goals

**Goals:**
- Reduce wasted API requests by ~77% through per-model adaptive scheduling
- Learn each model's update cycle purely from data (hash comparison), no hardcoded intervals
- Persist learned rhythms across restarts via Akka.Persistence + SQLite
- Maintain the existing separation: actors own lifecycle, streams own data flow
- Keep manual refresh capabilities (RefreshModel, RefreshLocation) as schedule bypasses

**Non-Goals:**
- Hardcoded/static model update intervals
- Changes to MQTT topics, discovery payloads, or egress stream graph
- Per-model poll interval configuration knobs
- Querying the Open-Meteo metadata API (requires authentication, not publicly available)

## Decisions

### D1: SchedulerActor owns all poll timing via ScheduleOnce

The SchedulerActor uses `Context.System.Scheduler.ScheduleTellOnce` per (location, model) pair instead of `Source.Tick`. This allows individual timing per model with precise offsets — each model fires at its own learned schedule.

**Why not Source.Tick per model?** Akka.Streams ticks are immutable — the interval can't be adjusted at runtime. The actor's `ScheduleOnce` allows dynamic recalculation after every fetch result.

**Why not a single heartbeat tick with SelectMany filter?** A global tick (e.g. 15 min) limits scheduling precision to the tick resolution. `ScheduleOnce` fires at the exact calculated time. It also simplifies the architecture — one mechanism for both discovery and steady-state.

### D2: SchedulerActor pushes targets into PipelineActor via Source.Queue + StreamRef

The SchedulerActor requests a `StreamRef` from the PipelineActor. The PipelineActor materializes a `Source.Queue<WeightedTarget>` and provides access. The SchedulerActor calls `queue.OfferAsync(target)` when a timer fires.

The PipelineActor replaces the MergeHub with a `Source.Queue<WeightedTarget>`. The queue is the single entry point for all targets — both scheduled polls and manual refreshes. Manual `RefreshModel`/`RefreshLocation` commands are sent to the SchedulerActor, which resolves them to targets and offers them into the same queue (bypassing schedule).

### D3: Hash computation and schedule feedback are stream stages after MQTT publish

The pipeline stream is extended with two stages after the existing MQTT publish:

```
Source.Queue<WeightedTarget>
  → Throttle(budget)
  → SelectAsyncUnordered(8, Fetch)
  → SelectAsync(N, PublishToMqtt)       ← existing concern, side-effect
  → Select(ComputeHash)                 ← synchronous, microseconds
  → Ask<Ack>(SchedulerActor)            ← built-in Akka.Streams Ask flow
  → Sink.Ignore
```

**Why publish before hash?** Publishing fresh data to HA is the primary goal and should not be blocked by scheduling bookkeeping. The hash + Ask is a secondary feedback loop.

**Why Akka.Streams Ask flow?** It provides backpressure — the stream waits for the SchedulerActor to acknowledge before processing the next result. This prevents the actor's mailbox from being flooded and ensures state updates are sequential.

### D4: Hash over forecast values only, with tomorrow-00:00-UTC cutoff

The hash is computed over `ForecastPoint.Values` and `DailyForecastPoint.Values` dictionaries, excluding:
- `ValidAt` / `Date` timestamps (shift with time, false positives)
- `Model`, `Location`, `Cycle`, `RetrievedAt` (identity/metadata, always change)

Only points with `ValidAt >= tomorrow 00:00 UTC` (hourly) or `Date >= tomorrow` (daily) are included. Points before this cutoff are in the "instable zone" — their values shift as hours pass and the API window moves, even without a model update. Points beyond tomorrow are stable and only change on actual model re-runs.

**Trade-off:** Short-horizon models (ICON-D2, ~48h) have a smaller hash window than long-range models (ECMWF, ~15 days). This is acceptable — even 24h of stable data is enough for reliable change detection.

### D5: SchedulerActor is a ReceivePersistentActor with SQLite journal

Events persisted: `DataChanged(LocationModel Key, int Hash, DateTimeOffset Utc)`. The actor recovers its `ModelPollState` dictionary on startup and immediately schedules `ScheduleOnce` timers from the recovered `nextPollUtc` values.

**Why event sourcing over snapshots-only?** The events are tiny (~50 bytes) and infrequent (~44/day). Event sourcing gives an audit trail of when model updates were detected. Snapshots can be added later as optimization if the journal grows (unlikely at this volume).

**Why SQLite?** It's embedded, zero-config, file-based — ideal for a single-instance Docker container with a volume mount. No external database dependency.

### D6: Two-phase scheduling — Discovery then Steady

**Discovery** (no cycle known): Poll every 20 minutes via `ScheduleOnce`. After two consecutive data changes, compute `cycle = lastChange - prevChange`. Transition to steady.

**Steady** (cycle known): `nextPoll = lastChange + cycle + 1 minute buffer`. If the expected change doesn't arrive, retry with exponential backoff (1min, 2min, 4min, 8min, max 15min). After 5 consecutive misses, fall back to discovery.

**On recovery:** If `nextPollUtc` is in the past, poll immediately. If the recovered state has a known cycle, go directly to steady — no re-discovery needed.

### D7: PollAll command is removed; RefreshModel/RefreshLocation remain

`PollAll` was the tick-driven "poll everything" command. With per-model scheduling, it's unnecessary. `RefreshModel` and `RefreshLocation` remain as messages to the SchedulerActor for event-driven refreshes (HA birth, manual trigger). The SchedulerActor resolves them to targets and offers them into the queue, bypassing the schedule timer.

## Risks / Trade-offs

**[Cold start takes ~6h to learn all cycles]** → Acceptable. Discovery polls every 20 min, and most models will show their first change within 3-6h. After the first restart with persistence, this cost is never paid again.

**[Hash false negatives from cutoff]** → If a model only changes values in the first 24h (instable zone) but not beyond, the hash won't detect it. This is unlikely — model re-runs recompute the entire forecast. If it becomes a problem, the cutoff can be reduced.

**[SQLite file corruption on unclean shutdown]** → SQLite uses WAL mode by default, which is crash-safe. Docker's `SIGTERM` handling gives the actor time for clean shutdown. Volume mounts persist the file.

**[ScheduleOnce drift]** → Timer precision depends on the actor system's scheduler granularity (typically ~100ms). For 3-6h cycles, millisecond drift is irrelevant. The 1-minute buffer absorbs any jitter.

**[Backpressure from Ask flow]** → The Ask flow blocks the stream until the SchedulerActor responds. At ~44 events/day, this is negligible. The Ask timeout should be generous (5s) since the actor does minimal work (hash compare + conditional persist + schedule next).
