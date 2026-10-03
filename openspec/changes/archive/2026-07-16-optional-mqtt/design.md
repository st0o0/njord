## Context

njord's egress pipeline already fans out through `EgressActor` (MergeHub → BroadcastHub) to multiple consumers: `MqttEgressActor` for Home Assistant publishing and `GrpcSnapshotConsumerActor` for gRPC streaming. The MQTT subsystem is cleanly isolated behind `IMqttConnection`/`IMqttTransport` interfaces, with three dedicated actors (`MqttConnectionActor`, `MqttEgressActor`, `DiscoveryActor`) and a single MQTTnet transport implementation.

Currently, `Mqtt:Host` is validated as mandatory at startup, making the MQTT broker a hard dependency even when only the gRPC API is desired.

## Goals / Non-Goals

**Goals:**
- Allow njord to start and serve forecasts (via gRPC) without an MQTT broker by setting `Njord:Mqtt:Enabled` to `false`.
- Keep the default behavior unchanged (`Enabled = true`).
- Skip MQTT-related DI registrations and actor startup when disabled.
- Avoid false health-check alarms when MQTT is intentionally off.

**Non-Goals:**
- No-op transport implementations or separate assemblies.
- Per-actor toggles (all three MQTT actors are all-or-nothing).
- Runtime toggling — the flag is read at startup and stays fixed.

## Decisions

### D1: Config flag on `MqttOptions` with default `true`

Add `Enabled` (bool, default `true`) to `MqttOptions`. This keeps the property co-located with the rest of the MQTT config and requires zero changes for existing deployments.

**Alternative**: top-level `Njord:MqttEnabled` — rejected because it splits related config across levels.

### D2: Conditional DI registration in `NjordServiceSetup`

When `Mqtt.Enabled` is `false`, skip registration of:
- `MqttEgressTuning`
- `MqttNetPublisher` / `IMqttConnection` / `IMqttTransport`
- `MqttConnectionHealthCheck`

The `NjordOptions` binding and validation still runs — we need the options object to read `Enabled`. The validator skips `Mqtt.Host` validation when `Enabled` is `false`.

Reading `Mqtt.Enabled` at DI registration time: `NjordServiceSetup.SetupServices` already has `IConfiguration` — bind the section inline to read the flag before registering services. This avoids a chicken-and-egg with `IOptions<NjordOptions>` not yet being available.

### D3: Conditional actor registration in `NjordActorSystemSetup`

When `Mqtt.Enabled` is `false`, skip `WithResolvableActors` registration for `MqttConnectionActor`, `MqttEgressActor`, and `DiscoveryActor`.

`NjordActorSystemSetup.BuildSystem` already resolves `NjordOptions` from the service provider — use `njordOptions.Mqtt.Enabled` to guard the three registrations.

### D4: No health check when disabled, not a "disabled" health check

When MQTT is disabled, `MqttConnectionHealthCheck` is simply not registered. The `/healthz` endpoint reports only `PipelineHealthCheck`. This is simpler than adding a "disabled → Healthy" code path and avoids misleading "MQTT healthy" reports when no broker exists.

**Alternative**: register a static `Healthy("MQTT disabled")` check — rejected as unnecessary noise in health output.

## Risks / Trade-offs

- **[ModelStateActor resolves DiscoveryActor at PreStart]** → `ModelStateActor` calls `Context.GetActor<DiscoveryActor>()` at line 45 and uses the ref in the stream graph to send `ModelCapabilityLearned`. When `DiscoveryActor` isn't registered, this throws and crashes the actor. Mitigation: make the resolution conditional on `Mqtt.Enabled` (inject `NjordOptions`); store `_discoveryActor` as `null` when disabled, and skip `ModelCapabilityLearned` sends in the stream graph when the ref is null. The MQTT actors themselves (`MqttConnectionActor`, `MqttEgressActor`, `DiscoveryActor`) only reference each other — when none are registered, none attempt resolution.
- **[EgressActor has no MQTT consumer]** → `BroadcastHub` with zero subscribers simply drops elements. No backpressure issue, no memory leak. The gRPC snapshot consumer still subscribes independently.
- **[Aspire AppHost always starts Mosquitto]** → Low risk. The container runs but njord ignores it when disabled. No change needed in AppHost for v1.
