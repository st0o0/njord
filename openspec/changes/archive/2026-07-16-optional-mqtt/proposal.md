## Why

njord currently requires a Mosquitto broker at startup — `Mqtt:Host` is validated as mandatory. This blocks running the service as a pure gRPC forecast provider (the gRPC API already exists and works independently of MQTT) and adds friction during development/testing when no broker is available. Making MQTT opt-out via a config flag lets njord serve forecasts without an MQTT dependency while keeping MQTT-to-HA as the default path.

## What Changes

- Add `Mqtt:Enabled` config flag (default `true`) to `MqttOptions`.
- Skip `Mqtt:Host` validation when MQTT is disabled.
- Conditionally register MQTTnet transport services (`MqttNetPublisher`, `IMqttConnection`, `IMqttTransport`, `MqttEgressTuning`) only when enabled.
- Conditionally register MQTT actors (`MqttConnectionActor`, `MqttEgressActor`, `DiscoveryActor`) only when enabled.
- Conditionally register `MqttConnectionHealthCheck` only when enabled — no check means no false-unhealthy noise.
- The ingest pipeline, enrichment, egress hub (`EgressActor`), and gRPC snapshot consumers continue to operate regardless of the flag.

## Non-goals

- Separate assembly or plugin architecture for MQTT — conditional DI in the existing project is sufficient.
- No-op transport implementations — actors simply don't start when disabled.
- Making individual MQTT actors independently toggleable (all-or-nothing is fine).

## Capabilities

### New Capabilities

- `optional-mqtt-egress`: Config-flag-driven conditional registration of the entire MQTT egress subsystem (transport, actors, health check).

### Modified Capabilities

- `service-configuration`: `MqttOptions` gains an `Enabled` property; `Mqtt:Host` validation becomes conditional on `Enabled`.
- `health-checks`: `MqttConnectionHealthCheck` is only registered when MQTT is enabled.
- `mqtt-actor-topology`: All three MQTT actors are conditionally registered based on the `Enabled` flag.

## Impact

- **Config**: `MqttOptions` class, `NjordOptionsValidator`, `appsettings.json`, `appsettings.Example.json`.
- **DI**: `NjordServiceSetup` (transport + health check registration).
- **Actors**: `NjordActorSystemSetup` (actor registration).
- **Aspire**: `Njord.AppHost/Program.cs` — Mosquitto container and env vars become conditional (or stay always-present; the service simply ignores them when disabled).
- **Tests**: existing tests pass unchanged (default `Enabled = true`); new tests verify the disabled path starts cleanly without a broker.
- **No API budget impact** — this change does not alter polling behavior.
