## Why

Tier 1 enrichments (weather alerts, ventilation, exercise, laundry, commute, pollen trend, night ventilation, confidence trend) deliver basic lifestyle intelligence. Tier 2 adds domain-specific enrichments that provide unique value no other HA addon offers: PV yield estimation, gardening advisories, energy demand forecasting, composite outdoor scores, BBQ planning, flood timelines and approaching weather front detection.

## What Changes

7 new `IDomainHandler` enrichments, each with a `ConfigKey` in `EnrichmentOptions`:

1. **SolarYieldEstimate** (Weather) — estimates daily PV yield from radiation + cloud + sunshine, using optional per-location panel config (kWp, azimuth, tilt). Output: kWh/day, peak hour, comparison with previous cycle.
2. **GardeningAdvisory** (Weather) — evaluates planting safety (frost risk, temp), watering need (rain forecast, ET₀), and harvest window. Output: booleans + time windows.
3. **EnergyDemandForecast** (Weather) — computes heating/cooling degree-hours from apparent_temp over 48h vs configured setpoints. Output: demand curve, peak hour.
4. **OutdoorActivityScore** (Weather × AirQuality) — composite 0-100 score from temp comfort + rain + wind + cloud + UV + PM2.5 + pollen. Output: score + breakdown + best window.
5. **BBQScore** (Weather) — 0-100 score: temp 18-28 ideal, wind <5m/s, no rain, low cloud. Output: score + best time window.
6. **FloodTimeline** (Flood) — analyzes the discharge series to find peak time, duration above warning threshold, recession estimate. Output: peak timing + duration.
7. **ApproachingFront** (Weather, needs previous) — detects pressure drop rate, wind direction shift, precipitation timing from current vs previous cycle. Output: estimated hours until change, temp drop, wind shift direction.

## Non-goals

- Tier 3 enrichments (Climate, Seasonal) — separate changes.
- Per-enrichment config UI in HA — Config Builder only.
- API budget impact: zero (uses existing domain data).

## Capabilities

### New Capabilities

- `tier-2-enrichments`: 7 advanced enrichment handlers with configurable parameters.

### Modified Capabilities

- `enrichment-config`: EnrichmentOptions gains 7 new toggle properties (all default false).

## Impact

- **Njord.Weather**: SolarYield, Gardening, EnergyDemand, BBQ, ApproachingFront, OutdoorScore handlers.
- **Njord.Flood**: FloodTimeline handler.
- **Njord.Core**: EnrichmentOptions + new per-enrichment config sections (SolarYield panel, EnergyDemand setpoints).
- **Njord.Messages**: 7 new PipelineEvent records.
- **Tests**: 2-3 facts per handler = ~20 new tests.
