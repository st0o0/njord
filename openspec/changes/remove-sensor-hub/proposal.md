## Why

The SensorHub receives indoor temperature and humidity readings from Home Assistant via gRPC, used by the Indices feature (VPD calculation, FrostProtection) to blend live sensor data with forecast data. Open-Meteo now provides VPD natively, and the indoor-sensor integration adds complexity (a dedicated actor, gRPC service, pipeline side-channel, separate projects) for a feature with very limited adoption. In the new proxy architecture, sensor input is replaced by API-native values and config-only fallbacks.

## What Changes

- **BREAKING**: Remove gRPC `SensorService` — both RPCs (`Push`, `StreamPush`) are removed. ha-njord must remove sensor push functionality.
- Remove `Njord.Sensors` project (SensorKind, SensorReading, SensorReadings — 3 source files).
- Remove `Njord.SensorHub` project (SensorHub actor, SensorHubState, Telemetry, SensorSetupContainer — 4 source files).
- Remove `Njord.Sensors.Tests` (11 tests) and `Njord.SensorHub.Tests` (26 tests) projects entirely.
- Remove `src/Njord.Messages/Sensors/` folder (RequestSensorReadings, LocationSensorReadings, QuerySensorReadings, UpdateReading, PushResult).
- Remove `SensorGrpcService`, `sensor.proto`, `SensorProtoMappings`.
- Remove `SensorOptions` and `SensorOptionsValidator` from `Njord.Core/Configuration/Sensors/`.
- Remove pipeline sensors side-channel: `ZipWith(sensors)` becomes direct flow, `RequestSensorReadings` ask removed from ForecastPipeline.PreStart.
- Modify `IndexAnalyzer.Compute`: remove `SensorReadings?` parameter. VPD and FrostProtection use configured indoor temperature (already the fallback today) or API-native VPD values.
- Remove SensorHub registration from NjordHost, CoreSetupContainer (telemetry meter), SensorSetupContainer.
- Update architecture tests (LayerReferenceSpec, NjordArchitecture assembly list).

## Non-goals

- Removing the Indices feature itself — it stays, just without live sensor input.
- Adding Open-Meteo native VPD as a replacement — that comes in the core-domain-model change.
- API budget impact: zero (no polling changes).

## Capabilities

### New Capabilities

_(none — this is a removal change)_

### Modified Capabilities

- `sensor-hub`: The entire SensorHub capability is removed.
- `sensor-hub-state`: Sensor state management is removed.
- `grpc-sensor-service`: The gRPC SensorService is removed.
- `pipeline-composition`: Pipeline sensors side-channel is removed. IndexAnalyzer no longer receives SensorReadings.
- `activity-indices`: IndexAnalyzer signature changes — no SensorReadings parameter, indoor temp from config only.

## Impact

- **Njord.Sensors**: Project removed from solution.
- **Njord.SensorHub**: Project removed from solution.
- **Njord.Sensors.Tests**: Project removed from solution.
- **Njord.SensorHub.Tests**: Project removed from solution.
- **Njord.Grpc**: SensorGrpcService, sensor.proto, SensorProtoMappings removed.
- **Njord.Messages**: Sensors/ folder removed.
- **Njord.Core**: SensorOptions, SensorOptionsValidator, ISensorHub marker removed.
- **Njord.Pipeline**: ForecastPipeline simplified — no sensors ZipWith, no RequestSensorReadings.
- **Njord.Indices**: IndexAnalyzer.Compute signature simplified, VPD/FrostProtection use config-only indoor temp.
- **Architecture tests**: Assembly lists and reference rules updated.
- **ha-njord**: Must remove sensor push feature in a separate PR.
- **Test count**: ~37 tests removed (11 Sensors + 26 SensorHub), plus sensor-specific pipeline and gRPC tests.
