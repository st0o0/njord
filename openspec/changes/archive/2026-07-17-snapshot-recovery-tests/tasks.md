## 1. Add Akka.Persistence.TestKit dependency

- [x] 1.1 Add `Akka.Persistence.TestKit` to `src/Directory.Packages.props` and reference it in `src/Njord.Tests/Njord.Tests.csproj`.

## 2. ForecastSnapshotActor recovery tests

- [x] 2.1 Create a `TestableForecastSnapshotActor` (private in the spec class) that extends `ForecastSnapshotActor` logic but accepts a custom `PersistenceId`. Add to `src/Njord.Tests/Grpc/ForecastSnapshotActorSpec.cs`.
- [x] 2.2 Add test: `State_recovers_from_snapshot_after_actor_restart` — send 20+ updates to trigger a snapshot, `GracefulStop` the actor, create a new instance with the same PersistenceId, assert `GetAllForecasts` returns all data.
- [x] 2.3 Add test: `Updates_below_snapshot_threshold_are_lost_on_restart` — send fewer than 20 updates (no snapshot), stop, recreate, assert empty state.
- [x] 2.4 Add test: `Actor_accepts_updates_after_recovery` — recover from snapshot, send a new `UpdateForecast`, verify it responds with `Ack` and the new data is queryable.

## 3. EnrichmentSnapshotActor recovery tests

- [x] 3.1 Create a `TestableEnrichmentSnapshotActor` in `src/Njord.Tests/Grpc/EnrichmentSnapshotActorSpec.cs` with a custom `PersistenceId`.
- [x] 3.2 Add test: `State_recovers_from_snapshot_after_actor_restart` — send 14+ updates to trigger a snapshot, stop, recreate, assert enrichments are recovered.
- [x] 3.3 Add test: `Actor_accepts_updates_after_recovery` — recover, send new update, verify `Ack` and queryability.

## 4. Snapshot store failure tests

- [x] 4.1 Add a failure-injection test for `ForecastSnapshotActor`: configure the `TestSnapshotStore` to fail on snapshot load, start the actor, observe and document what happens (crash, restart, dead, responsive with empty state). Use `TestProbe.Watch()` to detect `Terminated`.
- [x] 4.2 Add same failure-injection test for `EnrichmentSnapshotActor`.

## 5. Validation

- [x] 5.1 Run all tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`.
- [x] 5.2 Run slopwatch: `dotnet slopwatch` from repo root.
