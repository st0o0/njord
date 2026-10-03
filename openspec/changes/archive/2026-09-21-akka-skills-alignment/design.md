## Context

njord has 5 persistent actors that evolved organically. They hold mutable private fields, recover state inline, and mix business logic with actor plumbing. Messages use inconsistent naming (`Get*` vs `Query*`) and ad-hoc response patterns (nullable fields, bool flags). The akka-skills plugin codifies patterns proven across Akka.NET projects — this change aligns njord with those patterns.

Current actor inventory:

| Actor | Fields | Persistence | Snapshot cycle |
|---|---|---|---|
| BudgetTrackerActor | 5 primitives | Events + Snapshots (every 50) | `BudgetTrackerSnapshotDto` |
| SchedulerActor | Dict + trackers + queue | Events only (no snapshots) | None |
| ForecastSnapshotActor | Dict + counter | Snapshot-only (every 20) | `ForecastSnapshotDto` |
| EnrichmentSnapshotActor | Dict + counter | Snapshot-only (every 14) | `EnrichmentSnapshotDto` |
| ForecastHistoryActor | ForecastHistory + counter | Events + Snapshots | `ForecastHistorySnapshotDto` |

Additional finding: `Ack` is defined in both `Njord.Pipeline` (SchedulerMessages) and `Njord.Grpc` (SnapshotMessages) — deduplicate during this refactoring.

## Goals / Non-Goals

**Goals:**
- Each persistent actor's business logic testable as pure functions (no ActorSystem needed)
- Clean three-tier state separation: Internal State → Snapshot → Persisted DTO
- Consistent `Query*` naming for all read messages
- Typed `Completed`/`Failed` response hierarchies with `Exception Cause`
- CLAUDE.md accurately reflects message organisation (Pattern B)

**Non-Goals:**
- Changing persistence DTOs (extend-only contract preserved)
- Altering runtime behavior, stream graphs, or supervision
- Extracting a dedicated Messages project
- Adding/removing actors or changing actor topology

## Decisions

### D1: State record shape — one record per actor with extensions

Each persistent actor gets an immutable `record` holding its full internal state, placed in a companion `*State.cs` file alongside the actor. Extension methods on the state record provide `Apply(event)` → new state, `GetSnapshot()` → caller-facing view, and `GetPersistenceState()` → DTO for journal.

**Why extensions over instance methods:** Extensions keep the record a plain data carrier, enable static analysis, and match the akka-skills:actor-state pattern exactly. Alternative (methods on record) was rejected because it couples logic to the data shape and makes partial testing harder.

**File placement:** `BudgetTrackerState.cs` next to `BudgetTrackerActor.cs` in the same folder. Not a separate State/ folder — co-location with the actor keeps navigation simple.

### D2: Three-tier mapping — reuse existing DTO mapping classes

The existing `*DtoMapping` static classes (`BudgetTrackerDtoMapping`, `ForecastSnapshotMapping`, etc.) already convert between domain and DTO. These become the Persisted tier bridge:
- `State.GetPersistenceState()` calls the existing `*DtoMapping.ToSnapshot(...)` / `ToDto(...)`
- `State.FromPersistence(dto)` calls the existing `*DtoMapping.FromSnapshot(...)` / `FromDto(...)`

No new mapping layer needed — just wiring the existing mappers into the state record pattern.

### D3: Snapshot-only actors — simplified state pattern

ForecastSnapshotActor and EnrichmentSnapshotActor persist only snapshots (no events). Their state record still follows the same shape but `Apply()` handles commands directly (update dict), and `GetPersistenceState()` produces the snapshot DTO. No event→state replay needed.

### D4: SchedulerActor — state split

SchedulerActor has two concerns: persistent poll-state tracking (`_states` dict) and transient runtime state (queue, pipeline ref, retry count, Become phase). Only the persistent concern moves to an immutable state record. Transient fields stay as actor-level fields — they're reconstructed on restart, not recovered from persistence.

### D5: Query naming — mechanical rename with one dedup

All `Get*` query messages become `Query*`. The already-correct `QueryHistory` (ForecastHistoryMessages) stays as-is. The duplicate `Ack` type gets consolidated into a single shared `Ack` in `Njord.Domain` or a common messages file — callers import from one location.

Renames:
| Current | New |
|---|---|
| `GetBudgetUsage` | `QueryBudgetUsage` |
| `GetPollStates` | `QueryPollStates` |
| `GetForecast` | `QueryForecast` |
| `GetAllForecasts` | `QueryAllForecasts` |
| `GetEnrichment` | `QueryEnrichment` |
| `GetAllEnrichments` | `QueryAllEnrichments` |
| `GetSnapshot` (SensorHub) | `QuerySensorSnapshot` |

### D6: Response hierarchies — abstract base per actor domain

Each actor domain that returns query/command results gets an abstract response base:

```csharp
public abstract record BudgetResponse;
public sealed record BudgetUsageResult(long MonthlyUsed, long DailyUsed) : BudgetResponse;
public sealed record BudgetResponseFailed(Exception Cause) : BudgetResponse;
```

For snapshot actors with nullable "not found" responses, replace with explicit types:

```csharp
public abstract record ForecastQueryResponse;
public sealed record ForecastFound(ModelForecast Forecast) : ForecastQueryResponse;
public sealed record ForecastNotFound(string ModelKey) : ForecastQueryResponse;
public sealed record ForecastQueryFailed(Exception Cause) : ForecastQueryResponse;
```

Callers pattern-match instead of null-checking. `PushResult(bool, string?)` becomes `Completed`/`Failed` variants.

### D7: Refactoring order — simplest first, validate each

1. BudgetTrackerActor (4 primitives, isolated, few callers)
2. ForecastSnapshotActor (dict + counter, snapshot-only)
3. EnrichmentSnapshotActor (same shape as Forecast)
4. ForecastHistoryActor (domain container, events + snapshots)
5. SchedulerActor (most complex — dict, Become, transient state)

Each step: refactor → run full test suite → commit. This catches regressions immediately.

## Risks / Trade-offs

- **[Persistence compatibility]** State records introduce a new mapping path. Existing DTOs are unchanged, but a bug in `FromPersistence()` could corrupt recovery. → Mitigation: Existing DTO roundtrip tests validate the mapping. Add state roundtrip tests (Apply event → GetPersistenceState → FromPersistence → assert equal).
- **[Caller churn from renames]** `Get*` → `Query*` touches every caller (gRPC services, other actors, tests). → Mitigation: Compiler errors guide all changes; no runtime discovery. Do renames per-actor alongside state refactoring so each commit is self-contained.
- **[Response hierarchy verbosity]** More types than nullable/bool. → Trade-off accepted: explicit types eliminate null-reference risks and make exhaustiveness compiler-checked via pattern matching.
- **[SchedulerActor complexity]** Mixing persistent state record with transient Become-based state. → Mitigation: Clean split — only the `_states` dict goes into the state record; runtime fields remain mutable on the actor.

## Migration Plan

No deployment migration needed — this is an internal refactoring with no external API, wire format, or persistence schema changes. Existing persisted data recovers identically through the unchanged DTOs.

Rollback: revert commits. No data migration in either direction.

## Open Questions

None — all decisions are straightforward applications of the akka-skills patterns to the existing actor inventory.
