## Why

njord now holds a running multi-model snapshot and publishes a consensus pseudo-model (M0+M1). The raw ensemble data is ideal for threshold-based weather warnings: when multiple models agree that a threshold will be crossed, the confidence is high. Currently no warnings exist — users must scan raw numbers or set HA automations per parameter. This change adds 9 multi-model confidence alerts as the second enrichment consumer.

## What Changes

- **AlertEvaluator** — new pure static class in `src/Njord/Enrichment/` with one evaluation function per alert type. Each returns an `Alert` with type, severity, confidence (fraction of agreeing models), and diagnostic attributes.
- **9 alert types:**
  - **Frost** — temperature_2m_min ≤ threshold; confidence = model agreement; earliest frost time + expected low.
  - **Heat** — apparent_temperature_max ≥ tiered thresholds (30/35/40 °C); severity yellow/orange/red; confidence per level.
  - **Storm** — wind_gusts_10m ≥ threshold (default 16.7 m/s ≈ 60 km/h); confidence; expected max gust.
  - **Heavy rain** — precipitation sum ≥ hourly (10 mm) or daily (25 mm) threshold; confidence; expected total.
  - **UV** — uv_index at WHO levels (low/moderate/high/very_high/extreme); consensus UV.
  - **Fog** — temp − dewpoint < 2 °C AND wind < 3 m/s AND humidity > 90 %; confidence.
  - **Snow** — snowfall_sum > 0 + freezing_level_height; confidence; expected accumulation.
  - **Pressure drop** — pressure_msl drop ≥ 5 hPa in 3 h; confidence; weather front indicator.
  - **Thunderstorm** — CAPE > 1000 J/kg AND precip > 5 mm AND gusts > 15 m/s; severity none/low/moderate/high.
- **AlertResult** — record aggregating all alerts for a location. `ToMqttMessages(...)` serializes one `MqttMessage` per alert type on `njord/{location}/alerts/{alert_type}`.
- **AlertThresholdOptions** — all thresholds configurable under `NjordOptions.Enrichment.Alerts`.
- **Alert consumer stream** — new BroadcastHub subscriber in EnrichmentActor, gated by `Alerts.Enabled`.
- **Topic scheme extension** — `TopicScheme.AlertTopic(baseTopic, location, alertType)`.
- **Discovery payloads** — one HA device `njord_{location}_alerts` with binary_sensor components per alert type.

## Non-goals

- **Tiered alert escalation / history** — no "alert was active for X minutes" tracking. Each snapshot recomputes from scratch.
- **Push notifications** — njord publishes MQTT state; HA automations trigger notifications.
- **Custom alert definitions** — the 9 types are hardcoded; users toggle on/off and adjust thresholds.
- **API budget impact** — zero. Alerts operate exclusively on already-fetched data in the ModelSnapshot.
- **Consensus dependency** — alerts evaluate raw per-model data directly (multi-model confidence), not the consensus result. The consensus consumer need not be enabled for alerts to work.

## Capabilities

### New Capabilities
- `threshold-alerts`: AlertEvaluator with 9 pure evaluation functions, AlertResult record, AlertThresholdOptions configuration, alert consumer stream in EnrichmentActor, topic scheme and discovery payloads for alert sensors.

### Modified Capabilities
- `enrichment-actor`: EnrichmentActor materializes an additional consumer stream (alerts) gated by `EnrichmentOptions.Alerts.Enabled`.

## Impact

- **New files:** `src/Njord/Enrichment/AlertEvaluator.cs`, `src/Njord/Enrichment/AlertResult.cs`.
- **Modified files:** `EnrichmentOptions.cs` (AlertThresholdOptions), `EnrichmentActor.cs` (alert consumer stream), `TopicScheme.cs` (alert topics), `DiscoveryPayloadBuilder.cs` (alert device), `MqttEgressActor.cs` (alert discovery).
- **New tests:** `src/Njord.Tests/Enrichment/AlertEvaluatorSpec.cs`, `src/Njord.Tests/Enrichment/AlertResultSpec.cs`.
- **No new dependencies.**
