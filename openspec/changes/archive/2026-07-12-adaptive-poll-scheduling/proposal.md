## Why

njord polls all 8 weather models every 60 minutes, producing ~192 API requests/day per location. Weather models update at different cadences (3h, 6h, 12h), so ~77% of requests return unchanged data. This wastes Open-Meteo free-tier budget and generates unnecessary network traffic. By learning each model's actual update rhythm from the data itself, njord can poll each model shortly after its data changes — reducing requests to ~44/day while delivering fresher data.

## What Changes

- Introduce a **SchedulerActor** (ReceivePersistentActor with SQLite) that owns per-model poll timing. It learns each model's update cycle by hashing fetch results and detecting data changes, then schedules polls to arrive shortly after the next expected update.
- The SchedulerActor requests a **StreamRef** (Source.Queue) from the PipelineActor and pushes `WeightedTarget` elements directly into the pipeline stream.
- The PipelineActor's stream gains two new stages after the existing MQTT publish: a synchronous **hash computation** stage and a built-in **Ask flow** that sends the hash to the SchedulerActor for comparison and persistence.
- The `Source.Tick` + `PollAll` mechanism is removed — all poll timing moves into the SchedulerActor's `ScheduleOnce` timers.
- Add **Akka.Persistence.Sqlite** so learned rhythms survive container restarts without re-discovery.

## Non-goals

- Static/hardcoded model update intervals — the scheduler learns purely from data.
- Changes to the MQTT topic scheme, discovery payloads, or egress stream graph.
- Consensus forecasting or cross-model aggregation.
- Configurable per-model poll overrides (may come later as a refinement).

## API Budget Impact

| Scenario | Requests/day (1 loc, 8 models) | Monthly (1 loc) |
|---|---|---|
| Current (60 min fixed) | 192 | ~5,760 |
| Adaptive (learned cycles) | ~44 | ~1,320 |
| 2 locations adaptive | ~88 | ~2,640 |

Worst case during discovery (cold start): ~576 requests in the first 24h (all models at 20-min intervals until cycles are learned), then drops to steady-state. Well within the 300k/month free-tier limit.

## Capabilities

### New Capabilities
- `poll-scheduler`: Adaptive per-model poll scheduling with hash-based data change detection, cycle learning, and persisted state via Akka.Persistence + SQLite.

### Modified Capabilities
- `poll-pipeline`: The tick source and PollAll expansion are removed; the pipeline now receives targets via a Source.Queue fed by the SchedulerActor. New hash and Ask stages are appended after MQTT publishing.
- `pipeline-actor`: The PipelineActor exposes a StreamRef (Source.Queue) to the SchedulerActor instead of attaching its own tick source. The SinkRef handshake with the egress actor remains unchanged.
- `stream-composition`: MergeHub entry point is replaced by Source.Queue; the tick source attachment requirement is removed. New hash and Ask stages are added to the graph.
- `pipeline-commands`: PollAll is removed as a command variant. RefreshModel and RefreshLocation remain for manual/event-driven refreshes (HA birth, etc.) and are handled by the SchedulerActor as schedule bypasses.

## Impact

- **New packages**: `Akka.Persistence.Sqlite` (and transitive SQLite dependency)
- **New actor**: `SchedulerActor` with persistent state, replaces tick-based scheduling
- **Modified actors**: `PipelineActor` (Source.Queue instead of MergeHub + Tick, new stream stages)
- **Modified stream graph**: Two new stages (hash Select, Ask flow) after MQTT publish
- **Removed code**: `TickSource.cs`, `PollAll` command variant, `Source.Tick` attachment
- **New domain type**: `ForecastDataHash` — hash computation over forecast values with tomorrow-00:00-UTC cutoff
- **Configuration**: New `DiscoveryInterval` (default 20 min) and `RetryBackoff` settings
- **Data**: SQLite database file in a Docker volume for persistence across restarts
