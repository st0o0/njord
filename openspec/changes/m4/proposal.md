## Why

njord publishes per-model forecasts, consensus, alerts, and derived values — all computed from the latest snapshot. What's missing is the temporal dimension: how the forecast is *changing* over time. Users want to know whether tomorrow's temperature is trending up or down between model runs, when rain is expected to start, when extremes will peak, whether the models are converging or diverging, and how far out the forecast is still trustworthy. These temporal signals are the difference between "it will be 22 °C" and "it was 20 °C last run, now it's 22 °C and rising — models agree more than an hour ago."

## What Changes

- **TrendAnalyzer** — new pure static class computing temporal analysis from current and previous `ModelSnapshot`:
  - **Trend direction** — per parameter per horizon: rising/falling/stable compared to the previous snapshot's consensus value, with magnitude (delta).
  - **Weather-change detection** — identifies when the WMO weather code shifts significantly between snapshots (e.g., clear → rain), with a change description.
  - **Precipitation timing** — scans the hourly series for the first and last non-zero precipitation in the next 24h, reported as hours-from-now.
  - **Extrema timing** — finds the hour of maximum temperature and minimum temperature in the next 24h, reported as hours-from-now.
  - **Consensus stability** — compares the consensus spread (IQR or standard deviation) between the current and previous snapshot; shrinking spread = models converging, growing = diverging.
  - **Predictability decay** — measures how the consensus spread grows across horizons; a steep growth means short-range is reliable but long-range is not.
- **TrendResult** — result record with `ToMqttMessages` following the established pattern.
- **Trend consumer stream** in the `EnrichmentActor` — subscribes to `BroadcastHub<ModelSnapshot>`, uses `Scan` to pair current with previous snapshot, computes trends, delta-publishes to MQTT via SinkRef.
- **Topic scheme** extended with trend topics.
- **HA Discovery** extended with a trend device per location.
- **Configuration** — `EnrichmentOptions.Trends` section (enabled/disabled, default `false` per the enrichment pipeline design).

## Non-goals

- **Forecast verification** (comparing forecast to observations) — that's M7 (Historical).
- **Trend over multiple snapshots** (moving average of trend direction) — single previous-vs-current comparison is sufficient for v1.
- **Per-model trend analysis** — trends are computed on the consensus values, not per-model.
- **API budget impact** — zero additional API calls. All trends are computed from already-fetched data comparing successive snapshots.

## Capabilities

### New Capabilities
- `trend-analysis`: Pure computation functions for all temporal analysis quantities (trend direction, weather-change detection, precipitation timing, extrema timing, consensus stability, predictability decay) with a result record that serializes to MQTT messages.

### Modified Capabilities
- `enrichment-actor`: EnrichmentActor gains a trend consumer stream using `Scan` to pair current/previous snapshots, materialized when `Trends.Enabled` is `true`.
- `mqtt-egress`: TopicScheme extended with trend topic helpers; DiscoveryPayloadBuilder extended with trend device discovery.

## Impact

- **New files:** `src/Njord/Enrichment/TrendAnalyzer.cs`, `src/Njord/Enrichment/TrendResult.cs`
- **Modified files:** `src/Njord/Enrichment/EnrichmentActor.cs` (new consumer stream with Scan), `src/Njord/Configuration/EnrichmentOptions.cs` (new `TrendOptions`), `src/Njord/Egress/TopicScheme.cs` (trend topic methods), `src/Njord/Egress/DiscoveryPayloadBuilder.cs` (trend device)
- **New tests:** `TrendAnalyzerSpec.cs`, `TrendResultSpec.cs`, trend discovery tests
- **Dependencies:** None — all computations use standard math on existing domain types.
