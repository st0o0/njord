# Add MQTT Egress (1:1 per-model publishing with HA Discovery)

## Why

The pipeline fetches 8 models per location every hour — and logs a one-line
summary. Nobody can see the data. Strategy pivot (2026-07-12): instead of
computing a consensus first, njord publishes the per-model forecasts **1:1**
to Home Assistant via clean MQTT Discovery. Consensus can happen later — in
HA (helpers) or as a future njord change; the topic scheme keeps that door
open (`consensus` would join as a pseudo-model).

## What Changes

- **MQTT connection** owned by an actor (MQTTnet): connect/reconnect with
  backoff, Last Will `offline` on `njord/status`, `online` after connect,
  subscription to `homeassistant/status` for HA birth handling.
- **Device-based discovery** (verified against HA docs 2026-07-12): one
  retained config payload per (location, model) device at
  `homeassistant/device/<id>/config` with `dev`/`o`/`cmps` blocks — instead
  of one config topic per entity (54× fewer topics). Published at startup and
  re-published when HA announces `online`.
- **Full entity grid per model device**: one sensor per (parameter, horizon)
  — 9 parameters × horizons `+3/+6/+12/+24/+48/+72 h` (configurable list) =
  54 components per device, 432 entities per location at the 8-model default.
  Model devices are **enabled by default** (decision flip: without a
  consensus device they are the product).
- **Telemetry per cycle**: one retained state JSON per device
  (`njord/<location>/<model>/state`); components read it via
  `value_template`. Horizon anchoring: next full grid hour ≥ tick + horizon
  (ceil), so a horizon sensor never points into the past.
- **The entity set is static** (locations × models × parameters × horizons
  from config). A value a model cannot provide (beyond its horizon, failed
  fetch) surfaces as `unavailable` — via per-component availability plus
  `expire_after`, never as a stale or missing entity.
- New `Mqtt` configuration section (host, port, optional credentials,
  discovery prefix, base topic) with startup validation.
- The v1 log-summary sink is superseded: the sink now publishes telemetry and
  still logs one summary line per cycle.

## Capabilities

### New Capabilities

- `mqtt-egress`: connection lifecycle and availability, device-based
  discovery for the static entity grid, per-cycle telemetry publishing, and
  the unavailable-mapping rules.

### Modified Capabilities

- `service-configuration`: gains the validated `Mqtt` options section.
- `poll-pipeline`: the log-only v1 sink is replaced by the MQTT telemetry
  sink (summary logging stays).

## Impact

- **Code**: new `src/Njord/Egress/` (topic/payload builders, discovery
  composer, connection actor, DI), `src/Njord/Configuration/` (MqttOptions +
  validation), `src/Njord/Pipeline/` (sink wiring), `Program.cs`,
  `appsettings.json`. Tests mirror everything; discovery payloads get
  snapshot tests (Verify), broker behavior gets Testcontainers/Mosquitto
  integration tests.
- **Dependencies**: adds MQTTnet; test-side Verify.XunitV3 and
  Testcontainers (via `dotnet add package`, CPM).
- **API budget**: unchanged — no polling added or altered (0 additional
  requests).
- **HA side**: 432 entities per location — the docs will recommend a
  `recorder:` exclude for the njord glob to keep the HA database lean.

## Non-goals

- Consensus computation (deferred; quorum thinking is preserved in session
  memory and can be re-proposed — possibly as a `consensus` pseudo-model
  device).
- An HA `weather` entity (MQTT discovery has no weather platform; sensors
  only).
- TLS/WebSocket broker transports (plain TCP + optional credentials in v1).
- Support for HA versions without device-based discovery.
- Per-entity discovery topics as a fallback mode.
