## Context

M0+M1 delivered the EnrichmentActor infrastructure: Scan-based `ModelSnapshot`, second `BroadcastHub<ModelSnapshot>`, SinkRef binding to the egress MergeHub, and the consensus consumer as the first subscriber. The architecture is designed for additional consumers — each subscribes to the same BroadcastHub independently and sinks computed `MqttMessage`s into the shared SinkRef.

M2 adds the second consumer: threshold alerts. Unlike consensus (which aggregates values), alerts evaluate whether multi-model data crosses thresholds. The key output is **confidence** — the fraction of models agreeing the threshold is breached.

## Goals / Non-Goals

**Goals:**
- 9 alert types as pure evaluation functions, independently testable
- Multi-model confidence as the primary metric (not just "will it happen?" but "how sure are we?")
- All thresholds configurable with sensible meteorological defaults
- One HA device per location (`njord_{location}_alerts`) with `binary_sensor` components
- Alerts enabled by default (alongside consensus)

**Non-Goals:**
- Alert history, escalation, or duration tracking (stateless: recompute from snapshot)
- Push notifications (HA's job)
- Alert grouping, suppression, or deduplication beyond delta publishing
- Custom user-defined alert types

## Decisions

### D1: Pure evaluation functions, same pattern as ConsensusComputer

**Decision:** `AlertEvaluator` is a static class with one method per alert type. Each takes a `ModelSnapshot`, location, thresholds, and `TimeProvider`, and returns an `Alert` record.

**Why:** Proven pattern from M1. No Akka, no streams in the computation. Pure input → output. Unit-testable with simple test data.

### D2: Confidence = fraction of models agreeing

**Decision:** For each alert, confidence = (models crossing threshold) / (models with data). This is consistent across all 9 alert types.

**Why:** Multi-model confidence is the unique value of having 8 models. A frost warning with confidence 0.875 (7/8 models agree) is actionable. A warning at 0.125 is informational. HA users can automate at their preferred confidence threshold.

### D3: Alerts evaluate raw per-model data, not the consensus

**Decision:** Each alert evaluator scans the `ModelSnapshot.Entries` directly — it reads per-model hourly/daily series, not the consensus result.

**Why:** Confidence requires per-model evaluation. If we used the consensus median, we'd lose the "how many models agree" signal. The consensus consumer need not be enabled for alerts to work.

### D4: One HA device with binary_sensor components

**Decision:** All 9 alert types live on a single device `njord_{location}_alerts`. Each is a `binary_sensor` (ON when severity > None) with JSON attributes for severity, confidence, and diagnostics.

**Why:** HA binary sensors integrate naturally with automations ("when frost warning turns ON"). The JSON attributes carry the detail (how confident, expected low, earliest time). One device per location keeps the entity count manageable (9 entities vs 9 devices).

**Alternative rejected:** `sensor` with enum states — binary_sensor is more idiomatic for "alert active / inactive" in HA, and the severity/confidence detail lives in attributes.

### D5: Alert topics under `alerts/` segment

**Decision:** `njord/{location}/alerts/{alert_type}` with kebab-case type names. One retained JSON payload per alert type.

**Why:** Matches the roadmap's topic scheme. Separate from consensus (which mirrors model topics). Each alert type gets its own topic for independent HA availability and automation.

### D6: 24-hour evaluation window

**Decision:** All alerts evaluate a 24-hour window from "now" (via TimeProvider). This window captures the next-day forecast without requiring horizon configuration.

**Why:** Alerts answer "should I prepare for X today/tonight?" — 24 h is the natural planning horizon. The hourly parameters in the ModelSnapshot already cover this range for all models.

### D7: AlertThresholdOptions with meteorological defaults

**Decision:** All thresholds have defaults based on standard meteorological practice:
- Frost: 0 °C (DWD frost warning)
- Heat: 30/35/40 °C (DWD heat levels)
- Storm: 16.7 m/s (60 km/h, Beaufort 8)
- Heavy rain: 10 mm/h hourly, 25 mm daily (DWD heavy rain)
- Pressure drop: 5 hPa in 3 h (rapid deepening indicator)
- CAPE thunderstorm: 1000 J/kg (convective threshold)

**Why:** Users get useful alerts out of the box without needing meteorological expertise to configure thresholds.

## Risks / Trade-offs

**[False positives from conservative thresholds]** → Defaults are standard values but local climate may differ. Mitigation: all thresholds are configurable.

**[Stale alerts from old model data]** → If a model hasn't updated in 6+ hours, its data still influences confidence. This is correct: old data is the best available. The `models_used` count in attributes shows freshness.

**[9 alert topics × N locations]** → At 3 locations, 27 retained alert topics. Well within MQTT and HA limits. Delta publishing suppresses unchanged alerts.

**[Binary sensor limitations]** → Severity detail is in JSON attributes, not the sensor state. HA users need `attribute` templates for severity-based automations. This is standard HA practice for multi-valued sensors.
