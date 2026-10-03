## Context

`BudgetTracker` is currently a plain singleton that counts API calls in memory. Every container restart loses the counters, making `GetStatus` report zero usage even mid-month. The project already uses Akka.Persistence SQL for `ForecastHistoryActor`, `SchedulerActor`, `ForecastSnapshotActor`, and `EnrichmentSnapshotActor` — the persistence infrastructure is fully operational.

The tracker is consumed by two call sites:
1. `WeightedBudgetGate.TryAcquire()` — calls `RecordCall(cost)` on every API request (write path, fire-and-forget).
2. `ConfigGrpcService.GetStatus()` — calls `GetUsage()` to populate `BudgetStatus` (read path, needs response).

## Goals / Non-Goals

**Goals:**
- Usage counters survive container restarts via Akka.Persistence event sourcing.
- Snapshot support to bound recovery time.
- Recovery automatically discards events from stale months/days.
- Maintain the existing fire-and-forget write semantics for the hot path.
- Add `process_start_utc` to `ServerStatus` proto for consumer context.

**Non-Goals:**
- Rate-limiter token state in gRPC status (orthogonal feature).
- Hourly/per-minute usage tracking.
- Separate persistence backend — uses existing Akka.Persistence SQL.

## Decisions

### Actor placement: `Njord.Pipeline` namespace
The actor lives alongside `WeightedBudgetGate` and `SchedulerActor` in the Pipeline namespace — it's pipeline infrastructure, not configuration. The `BudgetTracker` class in `Configuration/` is removed entirely.

**Alternative**: Keep it in `Configuration/` next to the deleted class. Rejected because the actor's primary consumer is the pipeline gate; `ConfigGrpcService` is a secondary read-only consumer.

### Messages as records in the actor file
Commands (`RecordApiCall`, `GetBudgetUsage`) and the response (`BudgetUsage`) are defined as nested records inside `BudgetTrackerActor`. They are internal protocol, not shared domain messages.

**Alternative**: Separate messages file. Rejected — only two call sites, not worth the indirection.

### Tell for writes, Ask for reads
`WeightedBudgetGate` uses `Tell(new RecordApiCall(cost))` — fire-and-forget, no backpressure on the hot path. `ConfigGrpcService` uses `Ask<BudgetUsage>(new GetBudgetUsage(), timeout)` with the existing 5s timeout and the same graceful-degradation pattern as the `SchedulerActor` ask (catch `AskTimeoutException`, return status without budget data).

**Alternative**: `Tell` + stale local cache for reads. Rejected — `Ask` is simple, the read is infrequent (operator/dashboard), and the pattern is already established.

### Persistence DTOs in `Njord.Persistence`
Two new DTO classes following existing conventions:
- `ApiCallRecordedDto` — event DTO with `[JsonProperty]` strings, `Version` field.
- `BudgetTrackerSnapshotDto` — snapshot DTO with month, day, monthly/daily counters.

Mapping goes through a static `BudgetTrackerDtoMapping` class, same as `SchedulerDtoMapping` and `ForecastHistoryDtoMapping`.

### Snapshot every 50 events, delete old events/snapshots
At ~8 calls/hour (default config), 50 events ≈ 6 hours of operation. Recovery replays at most 50 events after loading the latest snapshot. On snapshot success, delete prior events and prior snapshots (same pattern as `ForecastHistoryActor`).

### Stale-month/day reset on recovery
When recovering from a snapshot, compare the stored month/day against `TimeProvider.GetUtcNow()`. If the month changed, reset both counters to zero. If only the day changed, reset daily counter. Events replayed during recovery also check: skip events whose timestamp falls in a previous month.

### Actor registration via Akka.Hosting
Register as a singleton via `WithActors` in `NjordServiceSetup`, same as `SchedulerActor`. The `WeightedBudgetGate` receives the `IActorRef` via a marker key (`BudgetTrackerActor`) from `ActorRegistry`. `ConfigGrpcService` resolves it from `ActorRegistry` the same way it resolves `SchedulerActor`.

### Proto change: `process_start_utc`
Add `int64 process_start_utc = 6` to `ServerStatus`. This is purely additive — existing clients ignore unknown fields. It lets consumers distinguish "monthly_used=0 because fresh restart" from "monthly_used=0 because genuinely unused".

## Risks / Trade-offs

- **Recovery after long uptime without restart**: If the service runs for months without restart, up to 50 events accumulate between snapshots. At ~240 events/day max, this is negligible. → No mitigation needed.
- **Actor mailbox ordering**: `RecordApiCall` messages from concurrent pipeline streams are serialized by the actor mailbox. This is a feature (no lock needed), not a bottleneck — the pipeline already throttles to ≤600 req/min. → Acceptable.
- **Ask timeout on GetStatus**: If the actor is slow to recover (first `GetStatus` call after restart), the 5s ask timeout could fire. → Same graceful degradation as SchedulerActor: return status without budget section, log a warning.
- **Persistence failure**: If the journal is unreachable, Persist callbacks won't fire, but the actor still updates in-memory state and responds to queries. Usage counters are best-effort, not safety-critical. → Acceptable.
