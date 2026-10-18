## Context

`Njord.Persistence` holds the events (`ApiCallRecorded`, `PollingStarted`, `ForecastChanged`, ...) and the snapshots `PersistedBudgetTrackerState` and `PersistedPollWorkerState`. Akka serializes them as JSON; the poll phase is stored as the integer `1`. FunkArr's persistence project keeps persisted enums (`PersistedDownloadStatus`, `PersistedMediaType`, ...) and one `PersistenceMapping` per owning project with `ToPersistence()` extensions, `GetPersistenceState()` and `FromPersistence()`. The domain `PollPhase` lives in `Njord.Messages.Pipeline`; `Njord.Persistence` must not reference it.

## Goals / Non-Goals

**Goals:** typed persisted enum, no casts, mappers out of the state files, an enforced boundary.

**Non-Goals:** changing event shapes, adding events, changing snapshot intervals, touching Akka serialization configuration.

## Decisions

1. **Explicit values, JSON stays numeric.** `PersistedPollPhase { Discovery = 0, Steady = 1 }` keeps the stored integer; the existing `PersistedPollWorkerState_wire_format` golden master must not change, and a spec deserializes an old-format document (phase as number) to prove compatibility. Unknown stored numbers are not tolerated: `ToDomain()` throws, which surfaces a corrupt or newer journal instead of mapping it silently.
2. **Names follow FunkArr.** `GetPersistenceState`/`FromPersistence` keep their names (they are also referenced by the existing specs and `AGENTS.md`); the new enum conversions are `ToPersistence()`/`ToDomain()`.
3. **One mapping class per owning project**, `internal static`, next to the state: `PollWorkerPersistenceMappings` (Pipeline) and `BudgetPersistenceMappings` (Budget). `PollWorkerStateExtensions` keeps only transitions (`Apply`, `Start`, ...). `GetSnapshot` moves to `PollWorkerMessageMappings`/`BudgetMessageMappings` (the later `snapshot-naming` change renames these to `ToPollStateEntry()`/`ToBudgetUsage()`).
4. **Boundary spec by reflection.** `PersistenceBoundarySpec` loads `Njord.Persistence`, walks every public type's properties, constructor parameters and generic arguments, and fails on any referenced type whose namespace is not `System*` or `Njord.Persistence*`. It also fails on enums without the `Persisted` prefix.
5. **Journal event types unchanged.** The events contain only primitives, `DateTimeOffset` and `TimeSpan`; the spec passes without edits there.

## Risks / Trade-offs

- A renumbered or extended domain enum can no longer shift persisted data silently, but a new `PollPhase` member now needs a `PersistedPollPhase` member too; the exhaustive switches and the roundtrip spec make that a compile or spec failure, not a runtime surprise.
- Throwing on unknown stored phases means an older binary reading a newer journal fails recovery; that is the intended behaviour for extend-only persistence and is documented in the spec.

## Migration Plan

Journal-compatible: no data migration. Order: specs for the enum and mappers against current behaviour, add the enum and conversions, move the mappers, add the boundary spec, update `AGENTS.md` and the persistence docs if they name the moved methods.
