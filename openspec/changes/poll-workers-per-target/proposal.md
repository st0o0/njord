## Why

`PollScheduler` is one persistent singleton that owns every target's poll state, timer, fetch request and failure routing,
plus the cycle summaries, the poll-state query and the trigger. One target's problem (a stuck request, a crash while
handling a reply) affects all targets, its journal mixes every target's events, and its own restart throws away every
timer and request in flight. Change C of the agreed stream/actor concept splits it the FunkArr way: a singleton manager
that only orchestrates, and one persistent sharded worker per target that owns its own timer, request and state.

## What Changes

- **BREAKING (persistence)** `PollScheduler` (persistence id `scheduler`), `PollSchedulerState`, `ModelPollState` and
  the records `PersistedDataChanged`, `PersistedPollSchedulerState` / `PersistedModelPollEntry` are removed. Rows of the
  old scheduler journal and snapshot store are ignored; there is no migration. Learned update cycles are re-learned once.
- New **`PollWorker`**: a persistent cluster-sharding entity per configured target (entity id `"{location}|{model}"`,
  configured spellings, persistence id `poll-worker-{entity id}`). It owns the target's poll state (phase, learned cycle,
  next poll, miss and transient-failure counts, last hash) as an immutable `PollWorkerState` with `Apply(event)`, persists
  every state transition as a past-tense event (new records in `Njord.Persistence/Events/Polling`), snapshots every 50
  events and cleans its journal. Its timer asks the gateway (`FetchForecast`, fetch reply timeout) with `PipeTo`; the
  outcome handling is exactly the scheduler's (change detection and cycle learning, reason-specific backoff, rejection
  handling, Ask timeout = `Transport`). At most one request is in flight per worker. Every outcome and the resulting
  poll state are reported to the manager. No passivation, no remember-entities.
- New **`PollManager`**: a non-persistent cluster singleton behind backoff-on-failure. It derives the configured target
  set (locations x resolved models), sends `StartPolling(target, discovery interval)` to every target through the shard
  region on start and again when the discovery interval changes (`PollingChanged`), sends `StopPolling` to running
  workers whose target is no longer configured, restarts a worker that stopped unexpectedly, forwards
  `TriggerImmediatePoll` to the matching workers and keeps, from the workers' reports, the latest poll state per target
  (for `QueryPollStates`), the per-location poll-cycle summaries and metrics, the data-change counter and the poll health
  snapshot.
- Markers: `IPollScheduler` becomes **`IPollManager`**; new **`IPollRegion`** for the shard region. Sharded messages
  implement **`IWithTargetId`** and are routed by **`ShardMessageExtractor`** in `Njord.Core` (FunkArr pattern).
- `ScheduledPoll` and `DataChanged` (only used inside the scheduler) leave `Njord.Messages`; `StartPolling`,
  `StopPolling`, `TriggerTargetPoll`, `PollWorkerReport`, `PollTarget` and `IWithTargetId` are added.
- Every external contract stays: `QueryPollStates`/`PollStatesResult`, `TriggerImmediatePoll`/`TriggerImmediatePollCompleted`,
  `PollingChanged`, the gRPC Ops `GetStatus`/`GetTargets`/`TriggerPoll` behavior, log lines and metric names.

## Non-goals

- No change to the gateway, the pipeline, the consumers, the request volume or the poll timing rules.
- No migration of the old scheduler journal; no remember-entities, passivation or multi-node rebalancing.
- Locations and models stay restart-required; the target set is not hot-reloaded.
- No per-target query API beyond the existing `QueryPollStates`.

## API-budget estimate

Request volume is unchanged: the same targets are polled under the same discovery / steady / backoff rules, and each
worker has at most one request in flight (the scheduler could already only overlap a poll with a manual trigger).
Example with the default 20-minute discovery interval: 1 location x 8 models polling in discovery for a whole day is
8 x 72 = 576 requests/day (limit 10k/day) and about 17.3k/month (limit 300k/month, 80 % startup check = 240k); a steady
model polls less. The only one-off effect is re-learning: after the upgrade every target starts in discovery once (as on a
fresh install) until it has seen two data changes, which is at most the discovery rate above for roughly one or two
update cycles. A restart no longer moves snapshot-restored targets to "poll now": every worker resumes at its persisted
next poll, so restarts cost at most as many requests as before.

## Migration plan

1. Deploy: on first start the old `scheduler` journal/snapshot rows are never read (no actor uses that persistence id);
   every worker starts with an empty journal, polls once immediately and re-learns its cycle.
2. The old rows can stay; deleting them (`persistence_id = 'scheduler'`) is optional housekeeping.
3. Rollback to an older version would recover the old scheduler from its untouched rows (its state is as old as the
   upgrade); the new `poll-worker-*` rows are ignored by the old version.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `poll-scheduler`: polling moves from one persistent scheduler to a manager plus one persistent sharded worker per
  target; per-target persistence, at most one request in flight, reports, start/stop/restart of workers.
- `failure-routing`: failures and rejections are handled by the worker of the target; replies are attributed to the
  worker's own target; reports for unknown targets are ignored by the manager.
- `poll-status-query`: `QueryPollStates` is answered by `PollManager` from the workers' reports.
- `pipeline-commands`: the worker (not the scheduler) sends `FetchForecast`.
- `poll-pipeline`: a throwing fetch is followed by a worker's request (wording of the requester).
- `actor-state-pattern`: the persistent poll actor is `PollWorker` with `PollWorkerState`.
- `actor-state-naming`, `dto-mapping-dissolution`, `persistence-alignment`: the poll state record and its conversions
  are `PollWorkerState` / `PollWorkerStateExtensions`.
- `naming-collisions`, `persistence-records`, `persistence-roundtrip-tests`: the scheduler records are replaced by the
  poll-worker records.
- `persistence-backoff`: the poll actors' supervision (manager singleton with backoff, workers under sharding).
- `prometheus-metrics`: the pipeline metrics are recorded by `PollManager` from the reports.
- `domain-setup-containers`, `servus-bootstrap`, `service-configuration`: registration of the manager singleton and the
  poll shard region.
- `grpc-v2-ops-service`: the Ops service asks `IPollManager`.
- `structured-logging`: the poll log lines come from `PollWorker` and `PollManager`.

## Impact

- Code: `src/Njord.Pipeline` (new `PollManager`, `PollWorker`, `PollWorkerState`, `PollCycleTracker` kept; removed
  `PollScheduler`, `PollSchedulerState`, `ModelPollState`), `src/Njord.Pipeline/Configuration/PipelineSetupContainer.cs`,
  `src/Njord.Core/Actors` (`ActorKeys`, `ShardMessageExtractor`; Core gains the cluster-sharding package),
  `src/Njord.Messages/Pipeline`, `src/Njord.Persistence/Events`, `src/Njord.Grpc/OpsGrpcService.cs`,
  `src/Njord/Configuration/ConfigManager.cs`.
- Tests: `src/Njord.Pipeline.Tests` (scheduler specs ported to worker/manager specs, hosted sharding spec),
  `src/Njord.Persistence.Tests`, `src/Njord.Core.Tests`, `src/Njord.Grpc.Tests`, `src/Njord.Tests`,
  `src/Njord.Architecture.Tests`.
- Docs: `AGENTS.md`, `docs/` pages that describe the scheduler.
