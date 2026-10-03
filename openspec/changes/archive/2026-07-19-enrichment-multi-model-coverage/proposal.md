## Why

All enrichment features receive multi-model data via `ModelSnapshot`, but most either ignore it (Consensus skips Daily parameters entirely) or flatten it into a single mean without exposing the inter-model spread. This leaves HA automations blind to forecast uncertainty — a heating controller can't distinguish "all models agree it'll be cold" from "one outlier skews the average."

## What Changes

- **Consensus Daily**: Extend `ConsensusResult.Compute` to iterate `parameters.Daily` alongside Hourly, producing Median/Spread/IQR/Agreement/CI per daily parameter per day-horizon (`d0`–`dN`). New entities on the existing consensus device.
- **Energy pessimistic/optimistic envelope**: Add `heating_demand_max`, `cop_estimate_min`, `cop_optimal_conservative` (hours where all models agree) alongside existing mean-based values. New fields on the existing energy device.
- **Indices range & confidence**: Add `_min`/`_max`/`_confidence` variants for each activity score (outdoor, running, cycling, etc.) computed per-model then aggregated. New fields on the existing indices device.
- **Alerts daily thresholds**: Evaluate alerts against daily aggregates (`precipitation_sum`, `uv_index_max`) in addition to the existing hourly scan. Enriches existing alert attributes (no new entities).

## Non-goals

- Derived enrichment multi-model spread (Beaufort, dewpoint comfort are direct physical transformations — spread on derived values is noise; use consensus output as input instead).
- Trends multi-model agreement (requires two snapshots already; adding model dimension would triple complexity for marginal gain).
- New enrichment devices or sub-topics — all additions are extra fields on existing devices.
- Changes to polling, API request shape, or budget (this change is pure post-processing).

## Capabilities

### New Capabilities

- `daily-consensus`: Multi-model aggregation (Median, Spread, IQR, Agreement, CI, Outlier) for all resolved Daily parameters, keyed by day-horizon. Extends existing consensus device discovery and state payloads.
- `enrichment-model-envelope`: Per-model computation of Index scores and Energy values, then aggregation into min/max/confidence envelope fields alongside existing single-value outputs.

### Modified Capabilities

- `consensus-computation`: Discovery and state payloads gain daily-horizon components; `ConsensusResult` gains a `DailyParameters` collection.
- `threshold-alerts`: Alert evaluators gain daily-aggregate checks (precip sum, UV max) feeding into existing severity/confidence/attributes.
- `energy-management`: `EnergyResult` gains worst/best-case fields computed per-model rather than mean-of-all.
- `activity-indices`: `IndexResult` gains min/max/confidence per score computed per-model rather than mean-of-all.

## Impact

- **Domain**: `ConsensusResult`, `EnergyResult`, `IndexResult`, `AlertResult` gain new fields (additive, non-breaking).
- **Enrichment features**: `ConsensusEnrichment`, `EnergyEnrichment`, `IndexEnrichment`, `AlertEnrichment` gain daily/per-model computation paths.
- **Egress**: `DiscoveryPayloadBuilder` and `StatePayloadBuilder` must emit new components/fields.
- **Tests**: All enrichment result specs need new cases for daily consensus, envelope fields, and daily alert thresholds.
- **Config**: No new config required (uses existing `ResolvedParameterSet.Daily` and model list). Optional: `EnrichmentOptions` could gain an `IncludeModelEnvelope` toggle per feature (default: true).
- **API budget**: Zero impact — no additional API calls; all new data is derived from existing `ModelSnapshot`.
