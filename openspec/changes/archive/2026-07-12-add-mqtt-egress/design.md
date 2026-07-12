# Design — add-mqtt-egress

## Context

The pipeline emits one `CycleResult` per cycle (`Received` model forecasts,
`Failed`, `Unanswered`); the guardian actor logs a summary. Egress does not
exist. The Mosquitto broker runs on the HA host. Architecture guardrails that
bind here: ingest and egress never reference each other (egress consumes only
domain types), streams carry data / actors own lifecycle, discovery is
lifecycle-driven while telemetry is tick-driven, the entity set is static,
missing value = `unavailable`.

Verified against the HA MQTT docs (2026-07-12): device-based discovery at
`homeassistant/device/<object_id>/config` with mandatory `dev` (device) and
`o` (origin) blocks and a `cmps` map of components (each with `p` platform
and `unique_id`); shared options like `state_topic`/`availability` are
inherited from the payload root by all components. HA publishes
`online`/`offline` on `homeassistant/status` (birth/will); integrations
should re-send discovery on `online`. Availability supports lists with
`availability_mode` and `availability_template`; `~` abbreviates the base
topic inside `*_topic` values.

## Goals / Non-Goals

**Goals:**

- Every fetched value visible in HA within one cycle, attributable to
  (location, model, parameter, horizon).
- Clean discovery: retained device-based configs, correct availability, no
  entity ever created from data (config only).
- Survive broker restarts, HA restarts, and njord restarts without manual
  intervention or ghost entities.

**Non-Goals:** consensus, weather entity, TLS transports, pre-device-discovery
HA versions (see proposal).

## Decisions

### D1: Device-based discovery, one payload per (location, model)

With 54 components per device, per-entity discovery would mean 432 retained
config topics per location; device-based discovery needs 8. One retained
JSON per device carrying `dev`, `o`, shared `state_topic` + `availability`,
and 54 `cmps` entries. Verified payload structure (see Context). Re-published
on startup and on HA birth (`homeassistant/status` = `online`) — the
connection actor owns that subscription (guardrail).

### D2: One retained state topic per device, JSON keyed by horizon

`njord/<location>/<model>/state`, retained, QoS 1, one publish per device per
cycle: `{"h3":{"temperature":21.4,"wind_speed":2.1,…},"h6":{…},…}`.
Components select values via `value_template`
(`value_json.h24.temperature`). Retained states make HA restarts painless;
54 sensors share one topic, so a cycle costs 8 PUBLISH packets per location,
not 432.

### D3: Horizon anchoring — ceil to the next full grid hour

`+24 h` at tick 19:31 → grid point 20:00 + 24 h = next full hour ≥ tick+24 h.
A horizon sensor never shows a timestamp in the past. The chosen `valid_at`
per horizon goes into the state JSON for transparency.

### D4: Unavailability — three layers, all declarative

1. **Service down**: LWT flips `njord/status` to `offline`; every component
   lists it in `availability` (`availability_mode: all`).
2. **Value structurally absent** (beyond model horizon, e.g. meteoswiss_ch1
   at +72 h): the JSON field is `null`; an `availability_template` on the
   state topic marks that component unavailable.
3. **Model missing a cycle** (failed/unanswered): the device's state topic
   is not re-published; `expire_after` = 2 × poll interval lets the values
   age out to unavailable instead of lying forever.

No imperative per-entity availability publishing — the declarative trio
covers all three failure shapes.

### D5: MQTTnet behind a thin seam, actor owns the lifecycle

`MqttConnectionActor` wraps an `IMqttPublisher` seam (MQTTnet's client
behind an interface for testability): connect with backoff, LWT
registration, birth subscription, publish requests via messages. Discovery
and telemetry are separate message flows into the same actor (guardrail:
discovery lifecycle-driven, telemetry tick-driven). The pipeline sink
`Tell`s the cycle result to the egress side; the guardian keeps its
one-line summary log.

### D6: Identity scheme

- Device id / discovery object_id: `njord_<location>_<model>`
- unique_id per component: `njord_<location>_<model>_<parameter>_h<horizon>`
- Entity friendly names: `<parameter> +<horizon>h`; device name
  `njord <location> <model>`.
- Open-Meteo ids are already snake_case and topic-safe; location names are
  validated non-empty and get slugified defensively.
- Origin block: name `njord`, version from the assembly.
- Model devices are enabled by default (decision flip vs. the 2026-07-11
  note — without a consensus device they are the product).

### D7: Sensor metadata

`device_class` + `unit_of_measurement` from the domain parameter units
(°C/mm/m/s/%/hPa); `state_class` is deliberately omitted — forecasts are
predictions, not measurements, and long-term statistics over them would be
misleading. `suggested_display_precision 1`.

### D8: Configuration

`Njord:Mqtt` section: `Host` (required — startup validation fails without
it), `Port` (default 1883), `Username`/`Password` (optional; password never
logged, may come from env `Njord__Mqtt__Password`), `DiscoveryPrefix`
(default `homeassistant`), `BaseTopic` (default `njord`). Horizons list
(default `[3,6,12,24,48,72]`) lives beside `Models` in the main section —
the entity grid derives from config alone.

## Risks / Trade-offs

- [432 entities/location bloat the HA recorder] → documented `recorder:`
  exclude recommendation; no `state_class` keeps them out of long-term
  statistics.
- [Device discovery requires a recent HA] → accepted (non-goal); the broker
  target is the user's current HA install.
- [Retained discovery configs outlive config changes → ghost entities] →
  discovery composer publishes an empty retained payload for devices that
  disappear from config at startup (tombstone), covered by an integration
  test.
- [Large state JSON per device] → ~6 horizons × 9 params ≈ small; fine.
- [MQTTnet API churn] → isolated behind the `IMqttPublisher` seam.

## Migration Plan

Additive; first release with egress. `appsettings.json` gains the `Mqtt`
section (host must be set by the user — startup fails fast otherwise, same
philosophy as the budget guard). Rollback = revert; retained topics can be
cleaned with the tombstone mechanism or `mosquitto_sub`-assisted purge.

## Open Questions

- None blocking. Consensus-as-pseudo-model and HA-side helpers remain
  explicitly out of scope.
