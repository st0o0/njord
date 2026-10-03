## Why

`BudgetTracker` is a plain in-memory singleton — every container restart resets `monthlyUsed` and `dailyUsed` to zero. `GetStatus` therefore reports misleading budget consumption after redeploys, and operators have no way to know how much of the Open-Meteo free tier has actually been consumed this month. Since the project already runs Akka.Persistence (SQL) for `ForecastHistoryActor`, `SchedulerActor`, and the snapshot actors, turning the tracker into a `ReceivePersistentActor` is a low-friction fix that keeps usage data durable across restarts without adding new infrastructure.

## What Changes

- Replace the `BudgetTracker` class with a `BudgetTrackerActor` (`ReceivePersistentActor`) that persists every API-call event and snapshots periodically.
- Add persistence DTOs (`ApiCallRecordedDto`, `BudgetTrackerSnapshotDto`) in `Njord.Persistence` following the extend-only DTO conventions.
- Change `WeightedBudgetGate` to `Tell` the actor instead of calling `BudgetTracker.RecordCall()` synchronously.
- Change `ConfigGrpcService.GetStatus` to `Ask<BudgetUsage>` the actor instead of calling `BudgetTracker.GetUsage()`.
- Remove the `BudgetTracker` singleton class entirely.
- Add `process_start_utc` to the `ServerStatus` proto so consumers know the tracking epoch.

## Non-goals

- Hourly or per-minute rate-limit tracking — the `WeightedBudgetGate` token bucket already handles rate shaping at runtime.
- Exposing rate-limiter token state (tokens remaining, tokens/sec) via gRPC — useful but orthogonal.
- Persisting to a separate file or database — Akka.Persistence SQL is already configured.
- API-budget impact: this change does not alter polling frequency or request count. Zero additional Open-Meteo calls.

## Capabilities

### New Capabilities
- `budget-tracker-persistence`: Durable API-usage tracking via a persistent actor with event sourcing, snapshot support, and stale-month recovery.

### Modified Capabilities
- `server-status-api`: `GetStatus` sources budget usage from the actor via `Ask` instead of a synchronous call; adds `process_start_utc` field.
- `dynamic-budget-throttle`: `WeightedBudgetGate` uses `Tell` to an `IActorRef` instead of calling `BudgetTracker.RecordCall()`.

## Impact

- **Code**: `BudgetTracker.cs` removed; new `BudgetTrackerActor.cs` in `Njord/Pipeline/`; new DTOs in `Njord/Persistence/`; `WeightedBudgetGate` constructor changes; `ConfigGrpcService.GetStatus` changes; `NjordServiceSetup` registration changes.
- **Proto**: `ServerStatus` gains `int64 process_start_utc = 6`.
- **Tests**: Existing `BudgetTrackerSpec` replaced by actor-level tests using `Akka.Persistence.TestKit`. `ConfigGrpcServiceStatusSpec` and `BudgetThrottleStageSpec` updated for actor interaction.
- **Wire compatibility**: Additive proto field only (field 6) — existing clients unaffected.
