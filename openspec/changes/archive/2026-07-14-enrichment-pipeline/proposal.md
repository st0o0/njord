## Why

After every poll njord holds raw data from up to 8 independent weather models in RAM — a multi-model ensemble that is currently forwarded 1:1 to MQTT. All the added value (consensus, uncertainty, warnings, daily-life indices) remains untapped and would have to be computed by downstream consumers (HA templates, external services). This is inefficient, error-prone, and wastes the biggest advantage of multi-model data: statistical aggregation at the source.

## What Changes

- **EnrichmentActor** — new actor consuming the BroadcastHub<FetchOutcome>, maintaining a `ModelSnapshot` via `Scan`, and distributing to specialized consumers through a second BroadcastHub<ModelSnapshot>.
- **Consensus consumer (M1)** — Median, trimmed mean, spread, IQR, agreement score, outlier identification, confidence interval, model availability. Published as pseudo-model `consensus` with the same horizons.
- **Alert consumer (M2)** — Threshold warnings with multi-model confidence: frost, heat, storm, heavy rain, UV, fog, snow, pressure drop / weather front, thunderstorm (CAPE).
- **Derived consumer (M3)** — Derived meteorological values: dew-point comfort, Beaufort, wind chill, diurnal amplitude, sunshine percentage, WMO plain-text, inversion detection.
- **Trends consumer (M4)** — Temporal analysis: trend direction, weather-change detection, precipitation timing, extrema timing, consensus stability, predictability decay.
- **Index consumer (M5)** — Daily-life indices: laundry drying, outdoor score, running/cycling comfort, BBQ weather, irrigation, degree days, solar yield, ventilation, frost protection, VPD plant stress.
- **Energy consumer (M6)** — Building management: heating demand, heat-pump COP, shading, battery strategy, night cooling.
- **History consumer (M7)** — Learning features: model accuracy tracking, weighted consensus, forecast drift, seasonal preference, anomaly detection. Requires persistence.
- **Topic scheme extension** for enriched data and HA Discovery payloads for new devices/sensors.
- **Configuration** controlling which consumers/enrichments are active.

## Non-goals

- **Akka.Cluster** — the data volume (≤10 locations, 8 models, 60-min interval) does not justify a cluster. All consumers run in-process.
- **Custom Akka.Streams stages** — exclusively built-in operators (Scan, Select, SelectMany, Where, Throttle, GroupBy, etc.).
- **Separate MQTT connection per consumer** — all consumers use the existing MergeHub/transport of MqttEgressActor via SinkRef.
- **Consensus in HA** — no Jinja2 templates or HA helpers for aggregation. njord computes everything.
- **Real-time UI or dashboard** — may come as a separate change (stage 2/3).
- **Changes to the poll scheduler** — SchedulerActor and its per-model timing remain untouched.
- **API budget impact** — no additional polling. Enrichment operates exclusively on already-fetched data.

## Capabilities

### New Capabilities
- `enrichment-actor`: EnrichmentActor with ModelSnapshot (Scan), second BroadcastHub, SinkRef binding to MqttEgressActor, consumer lifecycle and configuration.
- `model-snapshot`: Running state of latest model data per location, update on every data change, change detection.
- `consensus-computation`: Median consensus, trimmed mean, spread, IQR, agreement score, outlier identification, confidence interval, model availability matrix across all available models per time point.
- `threshold-alerts`: Threshold warnings (frost, heat, storm, heavy rain, UV, fog, snow, weather front, thunderstorm) with multi-model confidence and configurable thresholds.
- `derived-values`: Derived meteorological values (dew-point comfort, Beaufort, wind chill, amplitude, sunshine %, WMO plain-text, inversion).
- `trend-analysis`: Temporal analysis (trend direction, weather change, precipitation timing, extrema timing, consensus stability, predictability decay).
- `activity-indices`: Daily-life and activity indices (laundry, outdoor, sport, BBQ, irrigation, degree days, solar, ventilation, frost protection, VPD).
- `energy-management`: Building/energy management values (heating demand, COP forecast, shading, battery strategy, night cooling).
- `historical-learning`: Model accuracy tracking, weighted consensus, drift detection, seasonal preference, anomaly detection (requires persistence).
- `enrichment-topics`: Topic scheme extension and HA Discovery for enriched devices/sensors.

### Modified Capabilities
- `mqtt-egress`: MqttEgressActor must provide a SinkRef for incoming MqttMessages from the EnrichmentActor (new MergeHub input).
- `stream-composition`: BroadcastHub consumer architecture extended with the EnrichmentActor as an additional consumer.

## Impact

- **New actor:** `EnrichmentActor` in `src/Njord/Enrichment/`.
- **New domain type:** `ModelSnapshot` — holds latest `ModelForecast` per (location, model), change detection.
- **New computation modules:** Pure static classes per consumer area (ConsensusComputer, AlertEvaluator, DerivedValues, TrendAnalyzer, IndexScorer, EnergyForecaster).
- **Topic scheme:** Extension of `TopicScheme` with consensus, alert, derived, index, energy topics.
- **Discovery:** `DiscoveryPayloadBuilder` extended with consensus device and enrichment sensors.
- **Config:** `NjordOptions` extended with enrichment configuration (active consumers, thresholds, weights).
- **Persistence (M7 only):** SQLite or Akka.Persistence for historical forecast data.
- **Tests:** Unit tests for all computation modules (pure functions), Akka TestKit for EnrichmentActor lifecycle, Verify snapshots for discovery payloads.
