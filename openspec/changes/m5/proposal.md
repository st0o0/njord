## Why

njord now delivers raw model data, consensus, derived values (Beaufort, wind chill, etc.), alerts, and trends — but none of these directly answer the questions people actually ask: "Can I hang laundry outside today?", "Is it a good day for a run?", "Should I water the garden?". Daily-life indices translate multi-parameter weather data into single actionable scores that Home Assistant dashboards can display as simple gauges or binary yes/no indicators. Computing them at the source avoids complex Jinja2 templates and guarantees consistency across all locations.

## What Changes

- **IndexScorer** — new pure static class computing daily-life indices from a `ModelSnapshot`:
  - **Laundry drying** (0–100): combines temperature, humidity, wind speed, precipitation probability, sunshine. High = good drying conditions.
  - **Outdoor score** (0–100): general outdoor suitability from temperature comfort, wind, rain probability, cloud cover. High = pleasant day.
  - **Running comfort** (0–100): temperature range 5–20 °C ideal, penalizes high humidity, strong wind, rain, extreme heat.
  - **Cycling comfort** (0–100): similar to running but penalizes wind more heavily, tolerates wider temperature range.
  - **BBQ weather** (0–100): dry, warm, light wind, low rain probability. High = fire up the grill.
  - **Irrigation need** (0–100): inverse of rain probability + high temperature + low humidity + high evapotranspiration. High = water your garden.
  - **Degree days** — heating (HDD) and cooling (CDD) degree days from consensus temperature vs base temperatures (default 18 °C heating, 24 °C cooling).
  - **Solar yield** (0–100): estimated PV efficiency from shortwave radiation, cloud cover, temperature (panels lose efficiency in heat).
  - **Ventilation** (0–100): favorable conditions for natural ventilation — outdoor temp < indoor (assumed 22 °C), low humidity, some wind, no rain.
  - **Frost protection** — hours until frost risk and confidence (reuses alert frost data + trend timing).
  - **VPD plant stress** — Vapour Pressure Deficit category (low/optimal/high/critical) from temperature and humidity, relevant for greenhouses and gardens.
- **IndexResult** — result record with `ToMqttMessages` following the established pattern.
- **Index consumer stream** in the `EnrichmentActor`.
- **Topic scheme** and **HA Discovery** for index device per location.
- **Configuration** — `EnrichmentOptions.Indices` (enabled/disabled, default `false`, optional tuning: base temperatures, indoor temp assumption).

## Non-goals

- **Personalized fitness indices** (heart rate zones, VO2max adjustments) — too personal, out of scope.
- **Historical comparison** ("better than average for this date") — requires M7 persistence.
- **Indoor air quality** — no sensor data, only outdoor weather.
- **API budget impact** — zero additional API calls. All indices are computed from already-fetched data.

## Capabilities

### New Capabilities
- `activity-indices`: Pure computation functions for all daily-life indices (laundry, outdoor, running, cycling, BBQ, irrigation, degree days, solar, ventilation, frost protection, VPD) with a result record that serializes to MQTT messages.

### Modified Capabilities
- `enrichment-actor`: EnrichmentActor gains an index consumer stream, materialized when `Indices.Enabled` is `true`.
- `mqtt-egress`: TopicScheme extended with index topic helpers; DiscoveryPayloadBuilder extended with index device discovery.

## Impact

- **New files:** `src/Njord/Enrichment/IndexScorer.cs`, `src/Njord/Enrichment/IndexResult.cs`
- **Modified files:** `src/Njord/Enrichment/EnrichmentActor.cs` (new consumer stream), `src/Njord/Configuration/EnrichmentOptions.cs` (new `IndexOptions`), `src/Njord/Egress/TopicScheme.cs` (index topic methods), `src/Njord/Egress/DiscoveryPayloadBuilder.cs` (index device)
- **New tests:** `IndexScorerSpec.cs`, `IndexResultSpec.cs`, index discovery tests
- **Dependencies:** None — all computations use standard math on existing domain types.
