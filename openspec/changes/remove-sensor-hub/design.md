## Context

The SensorHub receives indoor temperature/humidity readings from Home Assistant via gRPC, stores them per location, and pushes them into the pipeline via a `ZipWith(sensors)` side-channel. The IndexAnalyzer uses these readings for VPD (Vapor Pressure Deficit) and FrostProtection calculations, with a fallback chain: live sensor → config value → hardcoded default. This involves 2 projects (Njord.Sensors, Njord.SensorHub), a gRPC service, a pipeline side-channel, and ~37 tests.

## Goals / Non-Goals

**Goals:**
- Remove all sensor input infrastructure (actor, gRPC service, projects, messages, pipeline side-channel).
- Simplify IndexAnalyzer to use config-only indoor temperature.
- Remove Njord.Sensors and Njord.SensorHub projects from the solution.

**Non-Goals:**
- Removing the Indices feature or changing index score calculations.
- Adding Open-Meteo native VPD as a data source (separate future change).
- Changing any other pipeline side-channels (settings side-channel is already removed by `remove-config-management`).

## Decisions

### D1: IndexAnalyzer uses config-only indoor temp

**Choice**: Remove `SensorReadings?` parameter from `IndexAnalyzer.Compute`. VPD and FrostProtection use `IndexOptions.IndoorTemp` (already exists as config, default 20°C).

**Rationale**: The sensor fallback chain was live → config → hardcoded. Removing the first link simplifies to config → hardcoded, which is the same as today for users who never set up sensor push.

**Alternative considered**: Keep a simpler sensor input (e.g., config-file-only indoor temp per location). Rejected — the config value already serves this purpose via IndexOptions.

### D2: Remove both sensor projects from solution

**Choice**: Remove `Njord.Sensors` (3 files: SensorKind, SensorReading, SensorReadings) and `Njord.SensorHub` (4 files) from the .slnx, delete directories.

**Rationale**: No consumer remains. SensorKind and SensorReadings were only used by the SensorHub actor and the pipeline's sensor side-channel.

### D3: Pipeline becomes direct flow

**Choice**: After this change (combined with config-management removal), the pipeline's `ZipWith` is gone entirely. The flow becomes: `MergeHub<Forecast> → StatefulSelectMany(computeAll) → BroadcastHub<PipelineEvent>`.

**Rationale**: Both side-channels (settings, sensors) are removed. The stateful stage reads everything it needs from constructor-injected values and the forecast stream.

## Risks / Trade-offs

- **[Users who used sensor push lose the feature]** → Very low adoption. The config fallback (`IndoorTemp`) provides the same value for users who set a fixed indoor temperature.
- **[VPD accuracy decreases without live sensor]** → Marginal. Indoor temperature is fairly stable (18-22°C) and the config default of 20°C is reasonable. Future: Open-Meteo provides VPD natively.

## Migration Plan

1. Remove SensorGrpcService, sensor.proto, SensorProtoMappings.
2. Remove Messages/Sensors/ folder.
3. Remove pipeline sensors side-channel (ZipWith).
4. Update IndexAnalyzer.Compute signature (remove SensorReadings parameter).
5. Update VPD and FrostProtection to read indoor temp from settings only.
6. Remove SensorHub actor, SensorHubState, ISensorHub marker, SensorSetupContainer.
7. Remove SensorOptions, SensorOptionsValidator.
8. Remove Njord.Sensors and Njord.SensorHub projects from solution.
9. Remove Njord.Sensors.Tests and Njord.SensorHub.Tests projects from solution.
10. Update architecture tests, host registration, telemetry meter list.
11. Update CLAUDE.md/AGENTS.md.
