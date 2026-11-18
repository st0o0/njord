## Why

PollWorker snapshot deserialization fails after a container restart, leaving all Akka cluster singletons unable to start (no polls, no budget growth, Unhealthy). The root cause: `GetPersistenceState()` uses a C# collection expression (`[.. enumerable]`) which creates a compiler-generated `<>z__ReadOnlyList` type. Newtonsoft.Json with `TypeNameHandling.All` embeds this internal type name in the snapshot; on recovery it cannot find a constructor and throws. Discovered during a 12h soak test where the scheduled container restart at T+6h killed the service permanently.

## What Changes

- Replace the collection expression in `PollWorkerPersistenceMappingExtensions.GetPersistenceState()` with `.ToList()` so the serialized `$type` is always `System.Collections.Generic.List` (a type Newtonsoft can construct).
- Add a roundtrip test that exercises the real `GetPersistenceState()` mapper output instead of manually constructed DTOs, closing the test gap that missed this bug.
- **BREAKING**: Existing snapshots written with the `<>z__ReadOnlyList` type are unrecoverable. Volume must be deleted after upgrading (acceptable at 0.x).

## Non-goals

- Changing the Newtonsoft `TypeNameHandling` setting (e.g. switching to `Auto`). That is a worthwhile hardening step but a separate change.
- Fixing the Discovery-to-Steady transition gap (also observed in the soak test, tracked separately).
- Adding migration logic for broken snapshots. At 0.x, volume deletion is the accepted recovery path.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `persistence-records`: The concrete collection type used in snapshot serialization is now constrained to well-known types that Newtonsoft can round-trip.
- `wire-format-golden-masters`: Roundtrip tests must cover the real mapper output path, not only hand-constructed DTOs.

## Impact

- `src/Njord.Polling/PollWorkerPersistenceMappingExtensions.cs` (1-line fix)
- `src/Njord.Persistence.Tests/PollWorkerRoundtripTests.cs` (new test using real mapper)
- `src/Njord.Polling.Tests/PollWorkerMappingsTests.cs` (verify mapper roundtrip)
- Existing SQLite/PostgreSQL snapshot stores with `<>z__ReadOnlyList` entries must be wiped after upgrade
- No polling or API-budget impact (this is a serialization fix, not a polling change)
