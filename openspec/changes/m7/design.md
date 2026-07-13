## Context

Akka.Persistence with SQLite is already configured in njord for the SchedulerActor. The journal/snapshot store uses the path from `NjordOptions.PersistencePath`. M7 adds a second persistent actor (`ForecastHistoryActor`) that stores forecast snapshots as events and provides historical data for analysis.

The key architectural decision is separating *persistence* (actor) from *analysis* (pure functions). The `ForecastHistoryActor` owns the journal; the `HistoryAnalyzer` is a pure static class that computes metrics from in-memory history state.

## Goals / Non-Goals

**Goals:**
- Persist consensus values per poll cycle for rolling history
- Compute model accuracy via self-referential verification (forecast vs later "observation")
- Produce weighted consensus that improves over time
- Detect forecast anomalies and drift
- Use existing SQLite persistence infrastructure
- Disabled by default (`History.Enabled = false`)

**Non-Goals:**
- External observation sources (weather stations, reanalysis APIs)
- Machine learning or neural network-based weighting
- Historical data export or API
- Cross-location model comparison

## Decisions

### D1: ForecastHistoryActor as ReceivePersistentActor

**Decision:** A new `ReceivePersistentActor` with PersistenceId `"forecast-history-{location}"`. It persists `ForecastRecorded` events containing: timestamp, location, per-model values at the reference horizon (h3), and consensus values. On recovery, it rebuilds an in-memory `ForecastHistory` state.

**Why:** Akka.Persistence is already configured. A persistent actor naturally handles recovery after restart and provides bounded event storage with snapshot/pruning.

**Alternative rejected:** Direct SQLite via EF Core or Dapper — would bypass the existing Akka infrastructure and add a dependency for one use case.

### D2: Self-referential verification ("observation" = consensus at h0)

**Decision:** The "observed" value for a past hour is the consensus median at the time that hour was the current hour (horizon h0). When a new snapshot arrives at time T, the consensus at T for T (h0) is treated as the ground truth. Previous forecasts for time T (made at T-3h, T-6h, T-24h etc.) are compared against this to compute accuracy.

**Why:** Without external observations, the best available proxy is the shortest-horizon consensus — models converge as the forecast horizon shrinks. This is standard in NWP verification when observations are unavailable.

### D3: Rolling history with configurable retention

**Decision:** The actor maintains a circular buffer of `ForecastRecord` entries (default 30 days × 24 hours = 720 records). Old records beyond retention are not deleted from the journal (Akka handles compaction via snapshots), but are excluded from analysis.

Snapshots are taken every 100 events to keep recovery fast. Events older than the retention window are implicitly ignored during recovery by checking timestamps.

### D4: Model accuracy as MAE

**Decision:** Mean Absolute Error per model per parameter, computed over rolling windows (7-day and 30-day). For each historical record where both the model's forecast (at h24) and the "observation" (later consensus at h0 for the same hour) exist:

`MAE = mean(|forecast_value − observed_value|)`

Published per model as `mae_7d_<model>` and `mae_30d_<model>`.

### D5: Weighted consensus from inverse MAE

**Decision:** Model weight = `1 / (MAE + ε)` where `ε = 0.1` prevents division by zero. Weights are normalized to sum to 1. The weighted consensus is `Σ(weight_i × value_i)` across models.

Published alongside the standard median consensus as `weighted_temperature`, `weighted_wind`, etc. Also publishes the weights: `weight_<model>`.

### D6: Forecast drift as run-to-run variance

**Decision:** For each model, drift measures how much the +24h forecast for the same target hour changes between successive model runs. Drift = standard deviation of the forecast values across the last N runs (default 5) for the same target hour.

High drift = the model keeps changing its mind. Published as `drift_<model>` (°C or m/s).

### D7: Seasonal preference

**Decision:** Season is determined by month: spring (3–5), summer (6–8), autumn (9–11), winter (12–2). The model with the lowest 30-day MAE in the current season is published as `best_model_<season>`.

### D8: Anomaly detection via z-score

**Decision:** For each parameter, compute the historical mean and standard deviation at the current hour-of-day. If the current consensus deviates by more than 2σ, flag as anomaly. Published as `anomaly` (boolean) and `anomaly_deviation` (σ units).

### D9: History actor lifecycle

**Decision:** The `ForecastHistoryActor` is created per location by the `EnrichmentActor` when `History.Enabled` is `true`. It receives `ModelSnapshot` messages (forwarded from the BroadcastHub consumer), persists them, and responds to `QueryHistory` messages with the current `ForecastHistory` state. The EnrichmentActor's history consumer stream sends snapshots to the history actor and queries it for analysis.

### D10: Single history topic per location

**Decision:** Topic `njord/{location}/history` with one flat JSON. Device id `njord_{location}_history`, model `history`.

## Risks / Trade-offs

**[Self-referential verification is circular]** → Using consensus-at-h0 as "truth" means verification quality depends on model agreement at short range. Acceptable for relative model ranking (which model is *best*), not for absolute accuracy claims.

**[Journal growth]** → At 1 event per poll cycle (60 min) × 30 days = ~720 events per location. With snapshots every 100 events, recovery reads at most 100 events + 1 snapshot. Negligible for SQLite.

**[Cold start]** → First 7 days have insufficient history for meaningful MAE. The analyzer returns null for metrics below minimum sample size (default 48 records = 2 days).

**[Weighted consensus requires all M1–M6 data]** → The history consumer depends on consensus being active. If consensus is disabled, weighted consensus cannot be computed.
