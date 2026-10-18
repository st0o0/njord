## Why

Everything Akka persists already lives in `Njord.Persistence` (the poll worker and budget tracker events and the two `Persisted*State` snapshots), but three things deviate from the FunkArr persistence style and from our own enum convention. `PersistedPollWorkerState.Phase` is an `int`, and the state converts it with casts (`(PollPhase)dto.Phase`, `(int)state.Phase`), which the enum conversion rule forbids and which silently turns a renumbered domain enum into a corrupt journal. The mappers `FromPersistence` and `GetPersistenceState` sit inside `PollWorkerState.cs` and `BudgetTrackerState.cs` between state transitions; FunkArr keeps them in a dedicated `PersistenceMapping` file next to persisted enums (`PersistedDownloadStatus`, `ToPersistence()`). Nothing proves that persisted types stay free of domain types.

## What Changes

- New `PersistedPollPhase` enum in `Njord.Persistence` with explicit values (`Discovery = 0`, `Steady = 1`) and `PersistedPollWorkerState.Phase` typed with it; the wire format stays `"Phase": 1`.
- Switch-expression extensions `ToPersistence(this PollPhase)` and `ToDomain(this PersistedPollPhase)` (exhaustive, throwing default) replace the casts.
- Persistence mapping leaves the state files: `PollWorkerPersistenceMappings` (`GetPersistenceState`, `FromPersistence`) in `Njord.Pipeline` and `BudgetPersistenceMappings` (`GetPersistenceState`, `FromPersistence`, `FromPersistedEvent`) in `Njord.Budget`, matching FunkArr's `PersistenceMapping` role; `PollWorkerState`/`BudgetTrackerState` keep only state and transitions.
- The domain-to-message mapping `GetSnapshot` (to `PollStateEntry`/`BudgetUsageResult`) moves out of the state files into the same style of mapping class.
- An architecture spec fails when a `Persisted*` or persistence event type references a type outside `System` and `Njord.Persistence` (no domain enums, no Messages types), and when `Njord.Persistence` declares an enum that is not named `Persisted*`.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `persistence-records`: persisted enums, persistence mappers outside the state types, self-contained persistence types.

## Impact

- `Njord.Persistence` (new enum, changed record), `Njord.Pipeline/PollWorkerState.cs` and a new mapping file, `Njord.Budget/BudgetTrackerState.cs` and a new mapping file, `PollWorker`/`BudgetTracker` call sites, `Njord.Architecture.Tests`, persistence and mapping specs.
- No persistence wire change: the journal and snapshot JSON stay byte-identical, proven by the existing Verify wire-format specs and a roundtrip of an old-format fixture. No behaviour change.
- Not breaking; commit type `refactor:`. Independent of the other open changes; apply after `parameter-registry-redesign` only to avoid overlapping test-file edits.
