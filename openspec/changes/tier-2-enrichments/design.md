## Context

The IDomainHandler + EnrichmentOptions infrastructure is in place. Adding a new enrichment is: write a handler (~50 lines), add a ConfigKey bool, register as IDomainHandler. Tier 2 handlers are more specialized than Tier 1 — some need per-enrichment config.

## Goals / Non-Goals

**Goals:** 7 new handlers, each with ConfigKey, tests, PipelineEvent type.
**Non-Goals:** Config UI, gRPC/MQTT serialization for new events (basic pass-through works).

## Decisions

### D1: SolarYield and EnergyDemand need per-enrichment config

New options sections:
- `Njord:SolarYield` — `PeakKw` (double, default 5.0), `Azimuth` (int, default 180), `Tilt` (int, default 30)
- `Njord:EnergyDemand` — `HeatingSetpoint` (double, default 20.0), `CoolingSetpoint` (double, default 26.0)

These are simple options classes with validators, registered alongside EnrichmentOptions.

### D2: OutdoorScore is Weather × AQ cross-domain

Like VentilationWindow and ExerciseAdvisory, OutdoorScore declares `Triggers = {Weather, AirQuality}`. It lives in Njord.Weather alongside the other cross-domain handlers.

### D3: ApproachingFront uses previous cycle comparison

Like ConfidenceTrend, it compares current vs previous LocationForecast. It looks at pressure trend (>3 hPa drop in 6h), wind direction shift (>45°), and precipitation onset timing.

### D4: FloodTimeline scans the daily discharge series

It finds the peak day, counts days above the p75 threshold, and estimates recession rate from the decline slope after peak.

### D5: All handlers in existing domain projects

No new projects. Weather handlers → Njord.Weather. Flood handler → Njord.Flood.
