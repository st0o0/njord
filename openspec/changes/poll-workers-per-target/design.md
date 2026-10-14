## Context

See proposal.md - Why. Current shape after `stable-pipeline-input` and `stable-pipeline-consumers`: `PollScheduler`
(src/Njord.Pipeline/PollScheduler.cs) is a `ReceivePersistentActor` (persistence id `scheduler`) and a cluster singleton
behind `BackoffSupervisor.Props(Backoff.OnFailure(...))`. It holds an `ImmutableDictionary<string, ModelPollState>`
keyed `"{location}|{model}"`, one cancelable `ScheduleTellOnce` per key, watches the gateway (`WaitingForGateway` +
stash), asks it with `FetchForecast` and `FetchTimeouts.Reply` and pipes the reply to itself, persists only
`PersistedDataChanged` (snapshot every 50 events: `PersistedPollSchedulerState`), and keeps a `PollCycleTracker`, the
poll health snapshot, the `Njord.Pipeline` metrics, `QueryPollStates`, `TriggerImmediatePoll` and `PollingChanged`.
Its callers are `OpsGrpcService` (`QueryPollStates`, `TriggerImmediatePoll` via `IPollScheduler`) and `ConfigManager`
(`PollingChanged`).

The host already forms a single-node cluster (`WithClustering` with its own seed node). Akka.Cluster.Hosting 1.5.73
provides `WithShardRegion<TKey>(name, entityPropsFactory, extractor, ShardOptions)`. FunkArr is the reference:
`*Manager` singletons orchestrate `*Worker` shard entities, messages implement `IWith*Id`, one
`ShardMessageExtractor : HashCodeMessageExtractor` in Core, workers ask collaborators with a timeout and `PipeTo`.

## Goals / Non-Goals

**Goals:**
- One actor per target owns that target's poll state, timer and request; nothing about one target can stall another.
- The worker's persisted state is its full state, so a restart resumes exactly where it was.
- The manager is a pure orchestrator and view: start/stop/restart, trigger, queries, cycle summaries, metrics, health.

**Non-Goals:**
- Changing the poll timing rules, the failure routing, the gateway protocol or any external message contract.
- Multi-node sharding concerns (rebalancing, remember-entities, passivation tuning).

## Decisions

### D1 Manager <-> worker protocol

All in `Njord.Messages/Pipeline` (Akka-free, one file each):
- `IWithTargetId { string TargetId }` - the shard routing key, entity id `"{location}|{model}"` (configured spelling).
- `PollTarget(LocationId Location, string ModelId)` with `Id` (`location|model`) and `ToString()` (`location/model`).
- `StartPolling(PollTarget Target, TimeSpan DiscoveryInterval)` - start, or update the discovery interval. Idempotent:
  a worker already started with that interval persists nothing and only reports.
- `StopPolling(string TargetId)` - persist "stopped", stop polling, stop the actor. The journal stays.
- `TriggerTargetPoll(string TargetId)` - poll now unless stopped or a request is in flight.
- `PollWorkerReport(PollTarget Target, PollWorkerOutcome Outcome, PollStateEntry State)` with
  `PollWorkerOutcome { Planned, Changed, Unchanged, Failed }` - worker -> manager after every start, after a recovery in
  the started state and after every fetch outcome. `PollStateEntry` is the existing query record, so the manager answers
  `QueryPollStates` by listing the latest entries.

The worker sends reports to `IPollManager` from the registry (the singleton proxy), not to the sender of `StartPolling`:
a worker restarted by its shard (after an exception) has lost its fields but must still report. *Alternative:* remember
the sender of `StartPolling` (rejected: lost on restart; would need a new start before reporting).

### D2 Worker state and events

`PollWorkerState` (src/Njord.Pipeline/PollWorkerState.cs) is a positional immutable record: `Target?`, `Active`,
`DiscoveryInterval`, `LastHash`, `LastChangeUtc`, `PrevChangeUtc`, `NextPollUtc`, `MissCount`, `Phase`, `Cycle`,
`TransientFailureCount`. `PollWorkerStateExtensions` hold one `Apply` overload per event (the transition rules of the
former `ModelPollState`, unchanged), `GetSnapshot()` -> `PollStateEntry`, `GetPersistenceState()` ->
`PersistedPollWorkerState`, and `PollWorkerState.FromPersistence(dto)`.

Events (`Njord.Persistence/Events/Polling`, positional, past tense, `DateTimeOffset`/`TimeSpan`):

| Event | Apply |
|---|---|
| `PollingStarted(Location, ModelId, DiscoveryInterval, Utc)` | first start: target set, active, next poll = `Utc`; later starts: active, new interval, a Discovery-phase target without transient failures whose next poll is ahead is replanned `next - old + new`, never before `Utc` |
| `PollingStopped(Utc)` | inactive |
| `ForecastChanged(Hash, Utc)` | data change (cycle learning, Steady at `last + cycle + 1 min`, else `+ discovery`) |
| `ForecastUnchanged(Utc)` | miss (steady backoff 1, 2, 4, 8, 15 min; 5th miss -> Discovery) |
| `PollFailed(Utc, MinimumDelay)` | transient failure (backoff, from the 5th on the discovery interval), next poll not before `Utc + MinimumDelay` (5 min for RateLimited/Budget, 0 otherwise) |
| `PollDeferred(Utc, Delay)` | next poll = `Utc + Delay` (PipelineUnavailable, 5 s), counters untouched |

Every outcome is persisted (the scheduler persisted only changes) because the next poll time and the counters are
part of the state that must survive a restart (spec: restarted worker keeps its next poll and phase). The discovery
interval is part of the state because `ForecastChanged`/`ForecastUnchanged`/`PollFailed` use it, so replaying needs the
interval that was current when the event happened. Snapshot every 50 events with journal cleanup, as before.
*Alternative:* persist "state-carrying" events (the resulting state) - rejected: the AGENTS rule is `Apply(event)` with
domain events, and recovery would not exercise the rules.

At ~3 events per hour per target (20-minute discovery) a worker reaches a snapshot about every 17 hours; the journal
then holds at most 50 rows per target.

### D3 Worker request handling

`PollWorker : ReceivePersistentActor, IWithTimers`, persistence id `poll-worker-{entityId}`. A single timer key
`poll`; `ScheduleNext` cancels it and either tells itself `PollDue` (next poll not in the future) or starts a single
timer for `NextPollUtc - now` (`TimeProvider`). `PollDue`/`TriggerTargetPoll` while inactive or while a request is in
flight do nothing (Debug log). A poll creates a `Guid` request id, resolves the gateway with
`Context.TryGetActor<IForecastGateway>` (missing -> Transport failure), asks `FetchForecast` with `FetchTimeouts.Reply`
and pipes `FetchReplied(requestId, reply)`; a failed Ask becomes `FetchForecastFailed(Transport, detail)` exactly as in
the scheduler. A reply with another id is ignored (an old request of a previous incarnation after a restart). The
reply's location/model spelling is irrelevant: the worker attributes it to its own target. The gateway is resolved per
poll, so a replaced gateway needs no watch, re-resolve or stash.

### D4 Manager

`PollManager : ReceiveActor, IWithTimers`, non-persistent: everything it holds is either configuration or a view of the
workers' persisted state, which they report on every start. *Alternative:* persistent manager (rejected: nothing to
recover that the workers do not already report).

- Target set: per location, `Models ∪ location.Models` (case-insensitive, configuration order) from `IOptions<NjordOptions>`
  (startup scope, restart-required). The scheduler did not filter by `ModelCoverageRegistry` either (coverage is only a
  startup validation warning), so neither does the manager.
- Start: resolve `IPollRegion` with `Context.TryGetActor`; not registered -> Warning and retry with backoff (1-30 s).
  When resolved: `StartPolling` to every target (Info `"Starting {Count} poll workers"`), then ask the region
  `GetShardRegionState` (10 s) and send `StopPolling` to running entity ids that are not configured.
- `PollWorkerReport`: unknown target -> Warning, ignored. Otherwise store the entry; watch the sender once; Changed ->
  health `PollHealthSnapshot(now)` + `njord_pipeline_data_changed_total`; Unchanged -> health; Changed/Unchanged/Failed
  -> `PollCycleTracker` (unchanged class) -> summary log + metrics.
- `Terminated` of a watched worker of a configured target -> `StartPolling` again after `Backoff.Create(1 s, max 30 s)`
  jitter, attempts reset by the next report of that target. Covers a worker stopped by a persistence failure.
- `PollingChanged` with a different discovery interval -> `StartPolling(target, new)` to all + Info log.
- `TriggerImmediatePoll` -> same matching as the scheduler, `TriggerTargetPoll` per target, reply immediately;
  region not resolved yet -> `TriggerImmediatePollFailed`.
- `QueryPollStates` -> entries in configuration order; before the first report a target's entry is its initial state.

Registered as singleton `poll-manager` with `BackoffOnFailure` (3-30 s, 0.2, unlimited) like the pipeline.

### D5 Markers and sharding options

`IPollScheduler` is renamed `IPollManager`: the marker names the role, and the role changed (it no longer schedules
anything itself; FunkArr's `*Manager` naming). Keeping the old name would make `OpsGrpcService` and `ConfigManager`
read as if they talked to a scheduler. `IPollRegion` is the shard region marker.

`WithShardRegion<IPollRegion>("poll-worker", resolver.Props<PollWorker>(entityId), new ShardMessageExtractor(),
new ShardOptions { ShouldPassivateIdleEntities = false, RememberEntities = false })`. No passivation: a worker waits
hours between polls and its timer must keep running; passivating would stop the timer. No remember-entities: the manager
starts the configured set on every start, and remembering would resurrect removed targets. `ShardMessageExtractor`
(src/Njord.Core/Actors) derives from `HashCodeMessageExtractor` (5 shards, FunkArr default) and maps `IWithTargetId`
only; any other message throws. `Njord.Core` gains the `Akka.Cluster.Hosting` package (cluster sharding) for it.

The region is registered before the manager singleton in the same `IActorRegistration`. The registration lives in an
internal `WithPolling(this AkkaConfigurationBuilder)` extension so the hosted spec runs exactly the production wiring.

### D6 Removed types

`PollScheduler`, `PollSchedulerState(+Extensions)`, `ModelPollState`, `HashDecision`, `ScheduledPoll`, `DataChanged`
(message), `PersistedDataChanged`, `PersistedPollSchedulerState`, `PersistedModelPollEntry`, the
`Njord.Persistence/Events/Pipeline` folder and their specs/verified files. `LayerReferenceSpec` anchors on another
`Njord.Messages` type.

## Risks / Trade-offs

- [More journal writes: one event per poll instead of one per change] → bounded by the snapshot + cleanup (≤ 50 rows
  per target); a few hundred small inserts per day on SQLite.
- [A manager restart sends the startup discovery interval, undoing a hot-reloaded one] → same as the scheduler's restart
  today; the next `PollingChanged` fixes it, and a manager restart only follows a bug.
- [A worker stopped by a persistence failure while the manager is down is not restarted] → the restarted manager sends
  `StartPolling` to every target anyway.
- [Region state query sees only entities of this node] → single-node cluster by design.
- [After an upgrade every target polls once immediately and re-learns its cycle] → accepted by the user (0.x); budget
  impact in proposal.md.
- [Recovered workers wait for their persisted next poll, so right after a restart the read model may be empty for up
  to one discovery interval / learned cycle] → the scheduler behaved the same for every target with a change after its
  last snapshot; `TriggerPoll` refreshes on demand.

## Migration Plan

See proposal.md - Migration plan. No data migration; the old `scheduler` rows are ignored.
