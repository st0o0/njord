## Why

The njord container uses ~950 MB after 1h 20min of runtime. For a weather polling service this is excessive. Root causes identified via live container analysis:

1. **`ForecastSnapshotActor` saves a full-state snapshot on every single `UpdateForecast`** — 19 model-location pairs means 19 snapshot writes per poll cycle, each cloning the entire `Dictionary<string, ModelForecast>`. This creates massive GC pressure and grows the SQLite snapshot store indefinitely.
2. **`EnrichmentSnapshotActor` has the same pattern** — full snapshot on every update.
3. **Old Akka.Persistence snapshots are never deleted** — `DeleteSnapshots` is never called, so the SQLite store accumulates every version.
4. **`ForecastHistory` stores per-model values for all parameters** — each `ForecastRecord` holds `IReadOnlyDictionary<WeatherModel, IReadOnlyDictionary<string, double?>>` (~10 models × ~60 params per record). Over 30 days at hourly intervals, this grows large.

## What Changes

- **Batch snapshot saving in `ForecastSnapshotActor`**: save a snapshot every N updates (configurable, default: after all models in a cycle have reported) instead of on every single update. Delete the previous snapshot after a successful save.
- **Same batching for `EnrichmentSnapshotActor`**: snapshot every N updates, delete old snapshots.
- **`ForecastHistory` stores only consensus values**: drop per-model values from `ForecastRecord`. The history feature computes MAE by comparing model forecasts against consensus-at-h0 — the consensus is already stored, but the per-model values at recording time are redundant (they come from the live forecast snapshot, not from history).
- **Delete old snapshots after save**: both snapshot actors call `DeleteSnapshots` with the previous sequence number after a successful `SaveSnapshotSuccess`.

## Non-goals

- Switching persistence backends (SQLite → PostgreSQL) — that's orthogonal.
- Reducing the number of parameters or horizons — those are user config.
- Changing the gRPC snapshot query interface — `GetForecast`/`GetAllForecasts` responses stay the same.
- No API budget impact — this change does not alter polling behavior.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `snapshot-actors`: Snapshot saving frequency changes from every-update to batched; old snapshots are deleted after save.
- `historical-learning`: `ForecastRecord` drops per-model values, keeps only consensus values.

## Impact

- **Memory**: estimated reduction of 200–400 MB from reduced GC pressure and smaller history records.
- **SQLite**: stops growing indefinitely; old snapshots cleaned up.
- **Data**: `ForecastRecord` shape changes — existing persisted history events still deserialize (per-model field becomes empty on old records) but new records only carry consensus.
- **Tests**: snapshot actor tests need update for batched saving; history tests for the slimmer record format.
