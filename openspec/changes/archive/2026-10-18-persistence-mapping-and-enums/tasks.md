## 1. Persisted enum

- [x] 1.1 Add specs: old-format `PersistedPollWorkerState` JSON deserializes, the wire-format golden master stays unchanged, every `PollPhase` roundtrips and an undefined value throws
- [x] 1.2 Add `PersistedPollPhase` (explicit values) to `Njord.Persistence` and type `PersistedPollWorkerState.Phase` with it
- [x] 1.3 Add `ToPersistence(this PollPhase)` and `ToDomain(this PersistedPollPhase)` as exhaustive switches; remove both casts

## 2. Mappers out of the state files

- [x] 2.1 Add `PollWorkerPersistenceMappings` and move `FromPersistence`/`GetPersistenceState` there; move `GetSnapshot` to a message mapping class; specs for the full state roundtrip
- [x] 2.2 Add `BudgetPersistenceMappings` for `GetPersistenceState`, `FromPersistence` (month/day reset rules) and `FromPersistedEvent`; move the `GetSnapshot` usage-result mapping; specs for the rollover cases
- [x] 2.3 Update `PollWorker` and `BudgetTracker` call sites; the state files keep only state and transitions

## 3. Boundary enforcement and docs

- [x] 3.1 Add `PersistenceBoundarySpec` (self-contained types, `Persisted*` enum naming)
- [x] 3.2 Update `AGENTS.md` (persistence conventions: persisted enums, mapper location) and `docs/` pages that name the moved methods
- [x] 3.3 Run `dotnet format whitespace`, build `Njord.slnx`, run every test project, `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`
