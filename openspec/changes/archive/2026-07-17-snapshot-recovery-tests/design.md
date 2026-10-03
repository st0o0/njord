## Context

Both `ForecastSnapshotActor` and `EnrichmentSnapshotActor` are snapshot-only `ReceivePersistentActor`s. The existing test suite (`ForecastSnapshotActorSpec`, `EnrichmentSnapshotActorSpec`) verifies CRUD operations and snapshot thresholds but never stops and re-creates an actor to prove snapshot recovery works.

In production, after container restarts, the `ForecastSnapshotActor` is found dead (messages in dead letters, gRPC `AskTimeoutException`). The `ActorRegistry` holds a stale `IActorRef`. The root cause is unclear — it could be a serialization failure during `SnapshotOffer` recovery, a supervision issue, or something else. Recovery tests are needed to reproduce and diagnose.

The actors use `[Serializable]` on private nested state classes (`ForecastSnapshotState`, `EnrichmentSnapshotState`). `EnrichmentSnapshotState` stores `Dictionary<string, object>`, which is fragile for cross-deployment serialization.

## Goals / Non-Goals

**Goals:**
- Prove that snapshot recovery works (or doesn't) with the inmem snapshot store.
- Use `Akka.Persistence.TestKit` to inject snapshot store failures and observe actor behaviour during recovery.
- Expose any serialization or recovery issues that cause the production dead-letter problem.
- Each test scenario maps directly to an existing or new spec requirement.

**Non-Goals:**
- Fix whatever the tests expose (follow-up change).
- Test with SQLite/PostgreSQL persistence (the inmem store uses the same Akka serialization path).
- Change the serialization format.

## Decisions

### Decision 1: Use inmem for happy-path recovery, TestKit for failure injection

**Happy-path recovery tests** use `akka.persistence.snapshot-store.inmem` — the same in-process store the existing tests use. To test recovery, we stop the first actor via `GracefulStop` and create a new actor with the same `PersistenceId`. The inmem store retains snapshots across actor lifecycles within the same `ActorSystem`.

**Failure injection tests** use `Akka.Persistence.TestKit` which provides `TestSnapshotStore` with interceptors (`SnapshotStoreInterceptors.Failure`, `.Reject`, `.Delay`). These let us simulate "snapshot store returns error during recovery load" and observe whether the actor crashes, restarts, or enters a broken state.

**Why not TestKit for everything**: The TestKit snapshot store has more ceremony (requires specific HOCON config and setup). The inmem store is simpler for proving the serialization round-trip works.

### Decision 2: Unique PersistenceId per test to avoid cross-test pollution

Each test creates an actor with a unique `PersistenceId` (e.g., `$"forecast-{Guid.NewGuid():N}"`) so tests can run in parallel without interfering. This mirrors the pattern used in `SchedulerActorSpec`.

Since `ForecastSnapshotActor.PersistenceId` is hardcoded to `"forecast-snapshot"`, the recovery tests need a testable wrapper that accepts a custom PersistenceId. This is the same pattern as `TestableSchedulerActor` in the scheduler tests.

### Decision 3: Observe actor death via DeathWatch, not just Ask timeout

To assert that the actor is dead (not just slow), the test watches the actor with `Context.Watch()` / `TestProbe.Watch()` and asserts a `Terminated` message. This distinguishes "actor stopped" from "actor alive but stuck in recovery loop".

## Risks / Trade-offs

- **[Risk] Testable wrapper diverges from production actor**: The wrapper duplicates the actor's constructor logic. If the production actor changes, the wrapper may not be updated.
  **Mitigation**: Keep the wrapper minimal — only override `PersistenceId`, inherit all behaviour.

- **[Risk] Inmem snapshot store serialization differs from SQLite**: The inmem store may serialize/deserialize differently than the SQL persistence plugin.
  **Mitigation**: The inmem store still goes through Akka's serializer pipeline. If recovery works with inmem, a SQLite-specific issue would point to the persistence plugin, not the actor.

## Open Questions

- Does `Akka.Persistence.TestKit` support xUnit v3 on Microsoft.Testing.Platform, or does it require the classic `TestKit` base class? If it requires a base class, we may need to adapt the test structure.
