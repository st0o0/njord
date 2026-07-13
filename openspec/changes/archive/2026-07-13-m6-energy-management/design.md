## Context

M5 established degree days and solar yield as general indices. M6 adds domain-specific building energy signals that go beyond scores — COP estimates, optimal scheduling windows, and actionable strategy labels ("charge"/"discharge"). The architecture follows the same consumer pattern: pure static class, result record, consumer stream, delta publishing.

## Goals / Non-Goals

**Goals:**
- 6 energy management functions, all pure and testable
- COP estimation based on simplified Carnot model
- Actionable scheduling recommendations (best hours, battery strategy)
- Single energy topic per location
- Disabled by default (`Energy.Enabled = false`)

**Non-Goals:**
- Real energy metering or consumption data
- Electricity pricing or tariff optimization
- Building physics simulation (thermal mass, U-values)
- HVAC control output (njord is advisory only)

## Decisions

### D1: Simplified Carnot COP model

**Decision:** COP is estimated as `η × T_hot / (T_hot − T_cold)` where `T_hot` is the heat pump flow temperature (default 35 °C = 308.15 K), `T_cold` is the outdoor temperature (K), and `η` is a system efficiency factor (default 0.45). This is a simplified Carnot model — real COP depends on defrost cycles, part-load, refrigerant, etc., but the relative ranking of hours is accurate.

**Why:** Users want to know "when is the best time to run the heat pump", not the exact COP. The relative ranking across hours is stable even with simplified assumptions.

### D2: COP-optimal hours as top-N ranking

**Decision:** Scan the next 24h, compute COP at each hour, return the top N hours (default 3) sorted by COP descending. Published as a JSON array of `{ hour: int, cop: double }`.

**Why:** HA automations can trigger the heat pump during these windows. A simple "best 3 hours" is more actionable than a full 24h COP curve.

### D3: Battery strategy state machine

**Decision:** Three states based on time-of-day and solar yield:
- **charge**: solar yield score > 60 AND daytime (is_day = 1)
- **discharge**: nighttime (is_day = 0) OR solar yield < 20
- **hold**: transition periods (solar yield 20–60 or dusk/dawn)

This is a simplified heuristic. Real battery management needs SOC, load profiles, and tariffs.

### D4: Shading from direct radiation and temperature

**Decision:** Shading score combines: direct radiation intensity (weight 0.5), daytime flag (weight 0.1), outdoor temperature > 25 °C overheating risk (weight 0.4). Published as 0–100 per-hour for the peak radiation hour, plus a "deploy_at" hour recommendation (first hour where shading > 70).

For the single-topic approach: publish the peak shading score and the recommended deployment hour.

### D5: Night cooling from overnight forecast

**Decision:** Night cooling potential evaluates hours 22:00–06:00 using the same ventilation formula from M5 but scoped to overnight. Published as a single score (0–100) representing the best overnight window.

### D6: Single energy topic per location

**Decision:** Topic `njord/{location}/energy` with one flat JSON. Device id `njord_{location}_energy`, model `energy`.

### D7: Configuration

```json
{
  "Njord": {
    "Enrichment": {
      "Energy": {
        "Enabled": false,
        "FlowTemp": 35.0,
        "CarnotEfficiency": 0.45,
        "HeatingBaseTemp": 18.0,
        "CopOptimalHours": 3
      }
    }
  }
}
```

## Risks / Trade-offs

**[COP estimate diverges from real-world]** → The simplified Carnot model overestimates absolute COP. Acceptable because the *relative ranking* of hours is what matters for scheduling.

**[Battery strategy ignores SOC and load]** → Without metering data, the strategy is purely forecast-based. It's a starting point for HA automations, not a complete BMS.

**[Night cooling only relevant in summer]** → In winter the score will be 0 (outdoor > indoor is false). This is correct behavior.
