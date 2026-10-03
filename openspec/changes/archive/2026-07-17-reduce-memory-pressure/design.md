## Context

Live container analysis (njord-dev, 2 locations × 10 models, ForecastDays=16, all enrichment enabled) showed 950 MB after 80 minutes. The `ForecastSnapshotActor` calls `SaveSnapshot` on every `UpdateForecast`, cloning the full 19-entry dictionary each time. Akka.Persistence never deletes old snapshots.

## Goals / Non-Goals

**Goals:**
- Reduce steady-state memory by cutting GC pressure from snapshot cloning.
- Stop SQLite snapshot store from growing indefinitely.
- Slim down `ForecastRecord` to reduce history memory footprint.

**Non-Goals:**
- Optimizing the `ModelForecast` data structure itself.
- Adding memory metrics or monitoring endpoints.

## Decisions

### D1: Snapshot after full cycle, not every update

`ForecastSnapshotActor` receives 19 `UpdateForecast` messages per poll cycle. Instead of snapshotting after each one, count updates and snapshot after a configurable threshold (default: same as the number of model-location pairs, i.e. once per cycle). The simplest approach: snapshot on a timer or after N updates since last snapshot.

A count-based approach is cleaner — snapshot every N `UpdateForecast` messages (default N=20, roughly one full cycle). This avoids needing to know the model count at compile time.

For `EnrichmentSnapshotActor`: same pattern — snapshot every N `UpdateEnrichment` messages (default N=14, roughly one cycle of 2 locations × 7 features).

### D2: Delete previous snapshot after successful save

After `SaveSnapshotSuccess`, call `DeleteSnapshots(new SnapshotSelectionCriteria(success.Metadata.SequenceNr - 1))` to remove all snapshots older than the current one. This keeps exactly one snapshot in the store.

### D3: `ForecastRecord` drops `ModelValues`

Currently:
```csharp
record ForecastRecord(
    DateTimeOffset Timestamp,
    string Location,
    IReadOnlyDictionary<WeatherModel, IReadOnlyDictionary<string, double?>> ModelValues,
    IReadOnlyDictionary<string, double?> ConsensusValues);
```

Change to:
```csharp
record ForecastRecord(
    DateTimeOffset Timestamp,
    string Location,
    IReadOnlyDictionary<string, double?> ConsensusValues);
```

The `ModelValues` field was used by `HistoryAnalyzer.ModelAccuracy` to compare model forecasts at horizon h24 against consensus at h0. But the model's h24 value at recording time is the same value available from the live forecast — storing it again in history is redundant. The MAE computation can use the current live forecast's value against the recorded consensus, or we accept that model accuracy is computed from whatever `ForecastHistoryActor.OnRecordSnapshot` captures.

Since the `HistoryAnalyzer` already receives the full history to compute MAE, the simplest path: keep `ModelValues` in the record but stop populating it in `OnRecordSnapshot` (pass an empty dictionary). This avoids a migration issue with existing persisted events. Then in a follow-up, remove the field entirely.

**Revised decision**: pass an empty dictionary for `ModelValues` in `OnRecordSnapshot` to avoid breaking deserialization of old events. The `HistoryAnalyzer` methods that reference `ModelValues` need updating to handle empty model data gracefully (return null MAE when no model values exist).

## Risks / Trade-offs

- **[Model accuracy temporarily unavailable]** → Until enough new-format records accumulate (48+ samples), model-specific MAE will be null and all models get equal weight. This is the same cold-start behavior that already exists.
- **[Snapshot loss window]** → With batched snapshots, a crash between saves loses the last N updates. On restart, the actor recovers the last snapshot and rebuilds from the next poll cycle. Acceptable for a weather forecast cache.
