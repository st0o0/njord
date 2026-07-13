## Why

The enrichment pipeline (M0/M1) delivers consensus and alerts (M2) from multi-model data, but the raw model values leave significant meteorological insight on the table. Derived quantities — Beaufort scale, dew-point comfort zones, wind chill, diurnal temperature amplitude, sunshine percentage, WMO weather descriptions, and temperature inversions — are easy to compute from existing parameters yet tedious to replicate in Home Assistant templates. Computing them at the source keeps HA dashboards simple and consistent across all models and the consensus device.

## What Changes

- **DerivedComputer** — new pure static class computing derived meteorological values from a `ModelSnapshot`:
  - **Beaufort scale** from wind speed (10 m), integer 0–12.
  - **Wind chill** (North American formula) from temperature and wind speed when T ≤ 10 °C and wind > 4.8 km/h; otherwise `null`.
  - **Dew-point comfort** — categorical comfort level (Dry/Comfortable/Sticky/Oppressive/Dangerous) from dew-point temperature.
  - **Diurnal amplitude** — max − min temperature within a 24 h window per model, plus cross-model median.
  - **Sunshine percentage** — ratio of sunshine_duration to theoretical daylight hours (from `is_day` series or sunrise/sunset).
  - **WMO weather description** — plain-text English string from `weather_code` (WMO 4677 table).
  - **Inversion detection** — boolean flag when temperature at +2 m increases with altitude indicators (surface pressure vs MSL pattern), heuristic-based.
- **DerivedResult** — result record with `ToMqttMessages` following the same pattern as `ConsensusResult` and `AlertResult`.
- **Derived consumer stream** in the `EnrichmentActor` — subscribes to `BroadcastHub<ModelSnapshot>`, computes derived values per location, delta-publishes to MQTT via SinkRef.
- **Topic scheme** extended with derived topics.
- **HA Discovery** extended with a derived device per location.
- **Configuration** — `EnrichmentOptions.Derived` section (enabled/disabled, no further tuning needed for v1).

## Non-goals

- **Custom thresholds for comfort zones** — hardcoded meteorological standard boundaries are sufficient for v1. Configurable thresholds can be added later.
- **Heat index / WBGT** — these require wet-bulb temperature or globe temperature, which Open-Meteo does not provide. Wind chill is computable from available data.
- **Altitude-corrected inversions** — proper inversion detection needs vertical profile data (upper-air soundings). The heuristic uses surface pressure vs MSL pressure as a proxy — good enough for dashboard awareness, not for aviation.
- **API budget impact** — zero additional API calls. All derived values are computed from already-fetched model data.

## Capabilities

### New Capabilities
- `derived-values`: Pure computation functions for all derived meteorological quantities (Beaufort, wind chill, dew-point comfort, diurnal amplitude, sunshine percentage, WMO description, inversion detection) with a result record that serializes to MQTT messages.

### Modified Capabilities
- `enrichment-actor`: EnrichmentActor gains a third consumer stream (derived) alongside consensus and alerts, materialized when `Derived.Enabled` is `true`.
- `mqtt-egress`: TopicScheme extended with derived topic helpers; DiscoveryPayloadBuilder extended with derived device discovery.

## Impact

- **New files:** `src/Njord/Enrichment/DerivedComputer.cs`, `src/Njord/Enrichment/DerivedResult.cs`
- **Modified files:** `src/Njord/Enrichment/EnrichmentActor.cs` (new consumer stream), `src/Njord/Configuration/EnrichmentOptions.cs` (new `DerivedOptions`), `src/Njord/Egress/TopicScheme.cs` (derived topic methods), `src/Njord/Egress/DiscoveryPayloadBuilder.cs` (derived device)
- **New tests:** `DerivedComputerSpec.cs`, `DerivedResultSpec.cs`, derived discovery snapshot tests
- **Dependencies:** None — all computations use standard math on existing domain types.
