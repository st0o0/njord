## Why

M1–M6 compute everything from the *current* model snapshot — they have no memory. But forecast quality improves dramatically when you know which models have historically been most accurate for your location. A model that consistently overshoots temperature by 2 °C should be downweighted in the consensus. Forecast drift (how much a model's prediction for the same future hour changes across successive runs) signals reliability. Seasonal patterns reveal which models perform better in winter vs summer. All of this requires persisted history: storing past forecasts and comparing them against what actually happened (using the shortest-horizon forecast as the "observed" proxy).

## What Changes

- **ForecastHistoryActor** — new persistent actor (Akka.Persistence with existing SQLite journal) that:
  - Subscribes to the `BroadcastHub<ModelSnapshot>` and persists each snapshot's consensus values as events.
  - Retains a configurable history window (default 30 days) via snapshot + event pruning.
  - On query, provides historical data to the HistoryAnalyzer.
- **HistoryAnalyzer** — pure static class computing learning features from persisted history:
  - **Model accuracy tracking**: MAE (Mean Absolute Error) per model per parameter, computed by comparing each model's forecast at horizon +24h against the "observed" value (the consensus at h0 when that hour actually arrived). Tracks rolling 7-day and 30-day MAE.
  - **Weighted consensus**: Computes model weights inversely proportional to their MAE, enabling accuracy-weighted consensus. Published as `weight_<model>` attributes.
  - **Forecast drift**: For each model, measures how much the forecast for a given future hour changes between successive model runs. High drift = unstable model. Published as drift score per model.
  - **Seasonal preference**: Tracks which models have the lowest MAE in the current season (spring/summer/autumn/winter). Published as a `best_model` per season.
  - **Anomaly detection**: Flags when the current consensus deviates significantly (> 2σ) from the historical mean for this time-of-year and hour. Published as boolean `anomaly` with deviation magnitude.
- **History consumer stream** in the `EnrichmentActor`.
- **Topic scheme** and **HA Discovery** for history device per location.
- **Configuration** — `EnrichmentOptions.History` (enabled/disabled, default `false`, retention days, MAE window).

## Non-goals

- **External observation data** (real weather stations, reanalysis) — only uses own forecast data as proxy observations.
- **Real-time model retraining** — weights are simple inverse-MAE, not ML.
- **Cross-location learning** — each location maintains its own independent history.
- **API budget impact** — zero additional API calls.

## Capabilities

### New Capabilities
- `historical-learning`: ForecastHistoryActor with Akka.Persistence for event storage, HistoryAnalyzer with pure computation functions (model accuracy, weighted consensus, drift, seasonal preference, anomaly detection), and a result record that serializes to MQTT messages.

### Modified Capabilities
- `enrichment-actor`: EnrichmentActor gains a history consumer stream, materialized when `History.Enabled` is `true`.
- `mqtt-egress`: TopicScheme extended with history topic helpers; DiscoveryPayloadBuilder extended with history device discovery.

## Impact

- **New files:** `src/Njord/Enrichment/ForecastHistoryActor.cs`, `src/Njord/Enrichment/HistoryAnalyzer.cs`, `src/Njord/Enrichment/HistoryResult.cs`
- **Modified files:** `src/Njord/Enrichment/EnrichmentActor.cs`, `src/Njord/Configuration/EnrichmentOptions.cs`, `src/Njord/Egress/TopicScheme.cs`, `src/Njord/Egress/DiscoveryPayloadBuilder.cs`
- **New tests:** `HistoryAnalyzerSpec.cs`, `HistoryResultSpec.cs`, `ForecastHistoryActorSpec.cs`, history discovery tests
- **Dependencies:** Akka.Persistence.Sqlite (already in the project).
