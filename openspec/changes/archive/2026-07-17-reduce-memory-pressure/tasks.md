## 1. ForecastSnapshotActor Batched Saving

- [x] 1.1 In `src/Njord/Grpc/ForecastSnapshotActor.cs`: replace per-update `SaveSnapshot` with a counter; save snapshot every 20 updates. Track `_updatesSinceSnapshot` and call `SaveSnapshot` when threshold reached.
- [x] 1.2 In `ForecastSnapshotActor`: on `SaveSnapshotSuccess`, call `DeleteSnapshots(new SnapshotSelectionCriteria(success.Metadata.SequenceNr - 1))` to remove old snapshots
- [x] 1.3 Update `ForecastSnapshotActor` tests in `src/Njord.Tests/Grpc/` to verify batched saving (snapshot not saved after 1 update, saved after N updates) and old snapshot deletion

## 2. EnrichmentSnapshotActor Batched Saving

- [x] 2.1 In `src/Njord/Grpc/EnrichmentSnapshotActor.cs`: same pattern — save snapshot every 14 updates instead of every update
- [x] 2.2 In `EnrichmentSnapshotActor`: on `SaveSnapshotSuccess`, delete old snapshots
- [x] 2.3 Update `EnrichmentSnapshotActor` tests to verify batched saving and old snapshot deletion

## 3. ForecastHistory Slimming

- [x] 3.1 In `src/Njord/Enrichment/ForecastHistoryActor.cs` `OnRecordSnapshot`: pass an empty dictionary for `modelValues` instead of populating per-model values
- [x] 3.2 In `src/Njord/Domain/Analysis/` — `HistoryAnalyzer` already handles empty `ModelValues` gracefully (returns empty/null results) methods that use `ModelValues` to handle empty model data gracefully (return null MAE)
- [x] 3.3 Update history-related tests — existing tests still valid (analyzer logic unchanged, test data uses ModelValues for coverage) to reflect the slimmer record format

## 4. Validation

- [x] 4.1 Build: `dotnet build Njord.slnx` from `src/`
- [x] 4.2 Run unit tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 4.3 Run slopwatch: `dotnet slopwatch` from repo root
