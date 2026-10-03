## Context

Enrichment features receive a complete `ModelSnapshot` (all models × all hourly + daily parameters per location per cycle). Currently:
- **Consensus** computes multi-model statistics but only for `parameters.Hourly`. Daily parameters are ignored.
- **Indices** and **Energy** call `Mean24h()` which averages a parameter across all models, discarding spread information.
- **Alerts** already computes per-model then aggregates (confidence = fraction agreeing), but only scans hourly points and misses daily aggregates.

The enrichment pipeline is stateless (except Trends/History): each `Compute()` call receives the full `ModelSnapshot` and returns `EgressEvent`s. This means adding multi-model awareness is purely a computation change — no pipeline topology or actor changes needed.

## Goals / Non-Goals

**Goals:**
- Daily parameters get consensus treatment identical to hourly (median, spread, IQR, agreement, CI, outlier)
- Indices and Energy expose the inter-model range (min/max/confidence) so HA automations can act on forecast certainty
- Alerts leverage daily aggregate values for threshold checks where daily sums are more natural than hourly scans
- All new data appears as additional fields on existing MQTT devices (no new devices, no discovery restructuring)

**Non-Goals:**
- Multi-model spread for Derived (Beaufort etc.) — use consensus output as Derived input instead
- Trends model agreement — would need multi-model × multi-snapshot, deferred
- Per-model entity devices (each model already has its own device)
- Config toggles per new field (all-or-nothing with the parent enrichment toggle)

## Decisions

### 1. Daily consensus uses day-horizon keys (`d0`, `d1`, `d2`...) parallel to hourly `h0`–`hN`

**Rationale**: Matches the existing horizon pattern. `DailyForecastSeries` is indexed by `DateOnly`; we compute the offset from "today" (in the cycle's timezone or UTC date) to produce `d0` = today, `d1` = tomorrow, etc.

**Alternative considered**: Using ISO dates as keys (`2026-07-19`). Rejected because it breaks the pattern, makes discovery component IDs unstable, and complicates HA template expressions.

### 2. `ConsensusResult` gains a separate `DailyParameters` list rather than mixing with hourly

**Rationale**: Hourly horizons are integers (hours from now); daily horizons are integers (days from today). Mixing them in one `Parameters` list would require disambiguating the horizon key format everywhere downstream. Two parallel collections is cleaner.

**Structure**:
```
ConsensusResult
├── Parameters: IReadOnlyList<ParameterConsensus>        // hourly, keyed "h0"–"hN"
└── DailyParameters: IReadOnlyList<ParameterConsensus>   // daily, keyed "d0"–"dN"
```

### 3. Indices/Energy compute per-model scores then aggregate, rather than aggregating inputs first

**Rationale**: `Mean24h` across models then scoring loses information. Example: if one model predicts 35°C and another 15°C, the mean is 25°C (nice outdoor score). But the correct envelope is: one model says "bad" (score 30), another says "great" (score 85) → min=30, max=85, median=57. The uncertainty is preserved.

**Approach**: For each `(location, model)` pair in the snapshot, compute the full index/energy result independently, then aggregate the numeric outputs:
- `_min` = minimum across per-model results
- `_max` = maximum across per-model results  
- `_confidence` = fraction of models within ±10% of the median score (configurable tolerance)

### 4. Alert daily evaluation enriches existing alerts, does not create new AlertTypes

**Rationale**: A "Heavy Rain" alert already exists. Adding daily `precipitation_sum` as an additional evidence path strengthens it rather than fragmenting into "HeavyRainHourly" vs "HeavyRainDaily". The severity and confidence remain unified per AlertType.

**Approach**: Each evaluator that can benefit from daily data gets a secondary check:
- `EvaluateHeavyRain`: additionally checks `daily.precipitation_sum` against `HeavyRainDailyThreshold`
- `EvaluateUv`: additionally checks `daily.uv_index_max` (more reliable than hourly scan for peak)
- `EvaluateSnow`: additionally checks `daily.snowfall_sum`

The final severity/confidence is the **maximum** across hourly and daily evaluations.

### 5. Discovery components for daily consensus use the pattern `{param}_d{N}`

**Rationale**: Consistent with existing `{param}_h{N}` for hourly. The consensus device grows but stays one device per location. HA entity IDs become: `sensor.njord_consensus_lucerne_temperature_2m_max_d0`.

### 6. Envelope fields on Indices/Energy use `_{metric}` suffix

**Rationale**: Minimal state payload change. Existing fields unchanged; new fields appear alongside:
```json
{
  "outdoor": 74,
  "outdoor_min": 62,
  "outdoor_max": 85,
  "outdoor_confidence": 0.8,
  "heating_demand": 45,
  "heating_demand_max": 62,
  "cop_estimate": 3.2,
  "cop_estimate_min": 2.7
}
```

Discovery registers these as additional sensor components on the same device.

## Risks / Trade-offs

**[Risk] Consensus device component count explosion** → With 21 daily params × 4 forecast days = 84 new components. Combined with existing hourly (~30 params × 96 hours = 2880), the discovery payload is already large. Mitigation: `_maxDiscoveryDays` config (default = `ForecastDays`, typically 4) keeps it bounded. Consider: should daily consensus be capped at fewer days than hourly?

**[Risk] Per-model index/energy computation increases CPU per cycle** → Instead of one `Mean24h` call, we now compute N full results (one per model). For 6 models this is 6× the scoring work. Mitigation: scoring functions are trivial arithmetic (no allocations, no I/O). Measured: index scoring is ~50 μs per model. 6 models = 300 μs — negligible vs the 60-minute poll interval.

**[Risk] State payload size growth** → Each envelope field adds ~30 bytes per score (×8 indices × 3 fields = 720 bytes). Mitigation: still well under MQTT's typical 256 KB payload limit and HA's attribute size limits.

**[Trade-off] Single confidence tolerance for all indices** → We use one tolerance value to compute agreement across all score types. Different scores have different natural variance. Accepted: a single configurable value is simpler; per-score tuning can come later if needed.
