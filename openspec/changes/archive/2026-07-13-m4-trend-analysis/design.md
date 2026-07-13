## Context

The enrichment pipeline (M0–M3) processes each `ModelSnapshot` independently — consensus, alerts, and derived values are all computed from the current snapshot alone. Trend analysis (M4) is the first consumer that needs *temporal comparison*: current snapshot vs previous snapshot.

The `BroadcastHub<ModelSnapshot>` delivers every changed snapshot. The trend consumer must hold the previous snapshot to compute deltas. This is a natural fit for `Scan` — the same operator already used to accumulate the `ModelSnapshot` itself.

## Goals / Non-Goals

**Goals:**
- Compare consecutive snapshots to detect changes in forecast consensus values
- Six independent analysis functions, all pure and testable
- Single JSON topic per location (not per-horizon) since trends summarize the whole forecast
- Disabled by default (`Trends.Enabled = false`) per enrichment pipeline design D8

**Non-Goals:**
- Multi-snapshot history or moving averages (v1 compares only previous vs current)
- Per-model trend analysis (trends operate on consensus values)
- Trend persistence across actor restarts (first snapshot after restart has no previous → no trend)

## Decisions

### D1: Scan in the consumer stream to pair snapshots

**Decision:** The trend consumer stream uses its own `Scan` operator to carry a `(ModelSnapshot? Previous, ModelSnapshot Current)` tuple. On the first snapshot, `Previous` is `null` and no trends are emitted. On subsequent snapshots, both are available for comparison.

**Why:** This is the simplest way to get "previous vs current" in a stream. No external state, no actor state, no persistence. The `Scan` closure naturally retains the previous value.

```
BroadcastHub<ModelSnapshot>
  .Scan((null, snapshot), (prev, current) => (prev.current, snapshot))
  .Where(pair => pair.Previous is not null)
  .Select(pair => TrendAnalyzer.Analyze(...))
```

**Alternative rejected:** Storing previous snapshot in the EnrichmentActor's actor state — mixes stream and actor concerns, harder to test.

### D2: Single trend topic per location (not per-horizon)

**Decision:** One JSON payload per location on `njord/{location}/trends` containing all trend analysis results. Not split by horizon.

**Why:** Trend data is inherently cross-horizon (predictability decay, extrema timing) or summary-level (weather change, precipitation timing). Per-horizon trends would mostly repeat the same structure with minimal differences. One JSON is cleaner for HA dashboard consumption.

### D3: Trend direction uses consensus median delta

**Decision:** Trend direction compares the consensus median at each horizon between the previous and current snapshot. Direction is determined by the sign of (current − previous), with a configurable dead-band threshold (default 0.5 °C for temperature, 0.5 m/s for wind, etc.) below which the trend is "stable".

The result per parameter per horizon is: `{ direction: "rising"|"falling"|"stable", delta: double }`.

For v1, trend direction is computed only for the primary parameters: `temperature_2m`, `wind_speed_10m`, `precipitation`, `cloud_cover`.

### D4: Weather-change detection uses WMO code categories

**Decision:** Weather change compares the WMO code (median across models) at the nearest horizon (h3) between snapshots. WMO codes are grouped into categories: clear (0–3), fog (45–48), drizzle (51–57), rain (61–67), snow (71–77), showers (80–86), thunderstorm (95–99). A change in category triggers a weather-change event with `from`/`to` category names.

### D5: Consensus stability uses IQR ratio

**Decision:** Consensus stability compares the IQR of the temperature consensus between snapshots. The ratio `current_iqr / previous_iqr` indicates convergence (< 1.0) or divergence (> 1.0). Published as a single value per location with a label: "converging", "stable", "diverging".

Thresholds: ratio < 0.8 → converging, > 1.2 → diverging, else stable.

### D6: Predictability decay uses spread gradient across horizons

**Decision:** Predictability decay measures how the consensus spread (for temperature) grows across horizons. It's computed as the linear regression slope of spread vs horizon hours. A steep positive slope means short-range is reliable but long-range diverges. Published as a single `decay_rate` (spread increase per hour) and a `reliable_hours` estimate (horizon at which spread exceeds a threshold, default 3 °C).

### D7: TrendAnalyzer as pure static class

**Decision:** Same pattern as `ConsensusComputer`, `AlertEvaluator`, `DerivedComputer`:

```
TrendAnalyzer.TrendDirection(prevMedian, currMedian, threshold) → (string Direction, double Delta)
TrendAnalyzer.WeatherChange(prevCode, currCode) → WeatherChangeResult?
TrendAnalyzer.PrecipitationTiming(ForecastSeries, now) → (int? StartsInHours, int? EndsInHours)
TrendAnalyzer.ExtremaTiming(ForecastSeries, tempParam, now) → (int? MaxInHours, int? MinInHours)
TrendAnalyzer.ConsensusStability(prevIqr, currIqr) → (string Label, double Ratio)
TrendAnalyzer.PredictabilityDecay(spreads, horizonHours) → (double DecayRate, int? ReliableHours)
```

### D8: Trend device per location

**Decision:** Device id `njord_{location}_trends`, model `trends`. Sensors:
- Per primary parameter: `trend_{param}` sensor with value "rising"/"falling"/"stable" and `delta` as JSON attribute
- `weather_change` sensor with value "no change" or "clear → rain" etc.
- `precip_starts` / `precip_ends` sensors (numeric, hours-from-now or unavailable)
- `temp_max_in` / `temp_min_in` sensors (numeric, hours-from-now)
- `stability` sensor ("converging"/"stable"/"diverging")
- `decay_rate` sensor (numeric, °C/h) and `reliable_hours` sensor (numeric)

## Risks / Trade-offs

**[First snapshot after restart has no trend]** → By design. The trend consumer needs a previous snapshot to compare against. After actor restart, the first snapshot produces no trend output. This is acceptable — the next snapshot (typically within one poll interval, 60 min) will produce trends.

**[Rapid model updates cause noisy trends]** → If models update at different intervals (GFS hourly, ECMWF every 6h), each model update shifts the consensus slightly, producing a "trend" that's really just model-mix noise. Mitigation: the dead-band threshold in D3 absorbs small fluctuations.

**[IQR comparison is only meaningful with ≥4 models]** → With fewer models, IQR is null and stability is not computed. This is handled gracefully (null → unavailable sensor).
