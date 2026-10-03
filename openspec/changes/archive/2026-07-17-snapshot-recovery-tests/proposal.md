## Why

After container restarts the `ForecastSnapshotActor` is found dead — gRPC `GetForecast` calls timeout with `AskTimeoutException` and messages land in dead letters. The existing test suite verifies CRUD and snapshot thresholds but never restarts an actor to prove recovery actually works. Both snapshot actors use `[Serializable]` on private nested state classes, and `EnrichmentSnapshotActor` stores `Dictionary<string, object>` — patterns known to break across deployments. Without recovery tests, regressions are invisible until production.

## What Changes

- Add `Akka.Persistence.TestKit` as a test dependency for snapshot store failure injection.
- Add recovery tests for `ForecastSnapshotActor`: save snapshot → stop actor → create new actor with same PersistenceId → verify state is recovered.
- Add recovery tests for `EnrichmentSnapshotActor`: same pattern.
- Add snapshot store failure tests: inject `SnapshotStoreInterceptors.Failure` during recovery → verify actor behaviour (does it crash? restart? become responsive?).
- These tests will likely expose the root cause of the production dead-letter issue, enabling a targeted fix in a follow-up change.

## Non-goals

- Fixing the supervision/restart strategy — that is a separate change once the failure mode is understood.
- Changing the serialization format (`[Serializable]` → records, protobuf, etc.) — out of scope here, but the tests will document current fragility.
- Testing the `SchedulerActor` persistence (it uses event-sourcing and already has recovery via `Recover<DataChanged>`; its recovery is proven by the production logs showing `phase=Steady` after restarts).

## Capabilities

### New Capabilities

(none — this is a test-only change)

### Modified Capabilities

- `snapshot-actors`: Adding test coverage for the existing "State survives restart" scenarios that are specified but untested. No requirement changes.

## Impact

- `src/Directory.Packages.props` — new `Akka.Persistence.TestKit` package version entry.
- `src/Njord.Tests/Njord.Tests.csproj` — new package reference.
- `src/Njord.Tests/Grpc/ForecastSnapshotActorSpec.cs` — new recovery and failure test methods.
- `src/Njord.Tests/Grpc/EnrichmentSnapshotActorSpec.cs` — new recovery and failure test methods.
- No production code changes. No API-budget impact.
