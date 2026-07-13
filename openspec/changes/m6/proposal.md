## Why

M5 introduced general daily-life indices including heating/cooling degree days and solar yield. M6 goes deeper into building energy management — the domain where weather forecasts have the most direct financial impact. Smart home users with heat pumps, PV+battery systems, automated shading, or passive cooling want actionable signals: "Run the heat pump now while COP is high", "Charge the battery from solar, discharge tonight", "Lower the blinds at 14:00 when solar gain peaks", "Open windows tonight for free cooling". These decisions depend on multi-hour temperature, radiation, and wind forecasts that njord already has.

## What Changes

- **EnergyForecaster** — new pure static class computing building energy management values from a `ModelSnapshot`:
  - **Heating demand** (0–100): weighted score from outdoor temperature vs heating base, wind chill effect, and cloud cover (radiative cooling). Higher = more heating needed.
  - **Heat-pump COP estimate**: Carnot-based COP approximation from outdoor temp and target temp (default 35 °C flow). Higher outdoor temp → higher COP → better time to run the heat pump.
  - **COP-optimal hours**: list of the best N hours (default 3) in the next 24h to run the heat pump, ranked by estimated COP.
  - **Shading recommendation** (0–100): combines direct solar radiation, sun position proxy (hour-of-day + is_day), and indoor overheating risk (outdoor temp > 25 °C). Higher = deploy shading.
  - **Battery strategy**: "charge" / "hold" / "discharge" based on solar yield forecast, time-of-day, and anticipated overnight demand. Simple state machine: charge during high-solar hours, hold in transition, discharge overnight.
  - **Night cooling potential** (0–100): reuses ventilation logic from M5 but focuses on overnight hours (22:00–06:00) — favorable when outdoor temp drops below indoor, low humidity, no rain.
- **EnergyResult** — result record with `ToMqttMessages` following the established pattern.
- **Energy consumer stream** in the `EnrichmentActor`.
- **Topic scheme** and **HA Discovery** for energy device per location.
- **Configuration** — `EnrichmentOptions.Energy` (enabled/disabled, default `false`, configurable: flow temp, PV capacity, heating base temp).

## Non-goals

- **Real energy metering integration** (actual kWh consumption) — out of scope, njord has no sensor data.
- **Electricity price optimization** (spot market, time-of-use tariffs) — would need price feed integration.
- **HVAC control commands** — njord is advisory only, not a controller.
- **Building thermal model** (U-values, insulation) — too complex, use simplified proxies.
- **API budget impact** — zero additional API calls.

## Capabilities

### New Capabilities
- `energy-management`: Pure computation functions for building energy values (heating demand, COP estimate, COP-optimal hours, shading, battery strategy, night cooling) with a result record that serializes to MQTT messages.

### Modified Capabilities
- `enrichment-actor`: EnrichmentActor gains an energy consumer stream, materialized when `Energy.Enabled` is `true`.
- `mqtt-egress`: TopicScheme extended with energy topic helpers; DiscoveryPayloadBuilder extended with energy device discovery.

## Impact

- **New files:** `src/Njord/Enrichment/EnergyForecaster.cs`, `src/Njord/Enrichment/EnergyResult.cs`
- **Modified files:** `src/Njord/Enrichment/EnrichmentActor.cs`, `src/Njord/Configuration/EnrichmentOptions.cs`, `src/Njord/Egress/TopicScheme.cs`, `src/Njord/Egress/DiscoveryPayloadBuilder.cs`
- **New tests:** `EnergyForecasterSpec.cs`, `EnergyResultSpec.cs`, energy discovery tests
- **Dependencies:** None.
