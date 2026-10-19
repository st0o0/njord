## 1. Remove gRPC SensorService

- [x] 1.1 Delete `src/Njord.Grpc/SensorGrpcService.cs`
- [x] 1.2 Delete sensor proto definition (`protos/njord/v2/sensor.proto` or the sensor service section)
- [x] 1.3 Delete `src/Njord.Grpc/Mapping/SensorProtoMappings.cs`
- [x] 1.4 Remove SensorService from gRPC endpoint mapping in `src/Njord.Grpc/GrpcEndpointExtensions.cs`
- [x] 1.5 Delete `src/Njord.Grpc.Tests/SensorGrpcServiceSpec.cs` and `src/Njord.Grpc.Tests/SensorProtoMappingsSpec.cs`

## 2. Remove Messages/Sensors

- [x] 2.1 Delete `src/Njord.Messages/Sensors/` folder (RequestSensorReadings, LocationSensorReadings, QuerySensorReadings, UpdateReading, PushResult)
- [x] 2.2 Fix compilation errors in projects referencing these types

## 3. Remove pipeline sensors side-channel

- [x] 3.1 Remove `ZipWith(sensors)` from `ForecastPipeline` — the flow becomes `MergeHub<Forecast> → StatefulSelectMany → BroadcastHub`
- [x] 3.2 Remove `RequestSensorReadings` ask from ForecastPipeline.PreStart
- [x] 3.3 Remove `LocationSensorReadings` handler from ForecastPipeline
- [x] 3.4 Delete `src/Njord.Pipeline.Tests/ForecastPipelineSensorSpec.cs` or remove sensor-specific tests from the pipeline spec

## 4. Update IndexAnalyzer

- [x] 4.1 Remove `SensorReadings?` parameter from `IndexAnalyzer.Compute` in `src/Njord.Indices/IndexAnalyzer.cs`
- [x] 4.2 Update `VaporPressureDeficit` to use indoor temp from settings/config only (not sensor readings)
- [x] 4.3 Update `FrostProtection` to use indoor temp from settings/config only
- [x] 4.4 Update `src/Njord.Indices.Tests/IndexAnalyzerSpec.cs` — remove sensor-reading test cases, verify config-only indoor temp
- [x] 4.5 Update pipeline's `StatefulSelectMany` to call `IndexAnalyzer.Compute` without sensors parameter

## 5. Remove SensorHub actor and projects

- [x] 5.1 Delete `src/Njord.SensorHub/` project directory
- [x] 5.2 Delete `src/Njord.Sensors/` project directory
- [x] 5.3 Delete `src/Njord.SensorHub.Tests/` project directory
- [x] 5.4 Delete `src/Njord.Sensors.Tests/` project directory
- [x] 5.5 Remove all four projects from `src/Njord.slnx`
- [x] 5.6 Remove project references to Njord.Sensors and Njord.SensorHub from any .csproj files

## 6. Remove SensorOptions and marker

- [x] 6.1 Delete `src/Njord.Core/Configuration/Sensors/SensorOptions.cs` and `SensorOptionsValidator.cs`
- [x] 6.2 Delete `src/Njord.Core.Tests/Configuration/Sensors/SensorOptionsValidationSpec.cs`
- [x] 6.3 Remove `ISensorHub` marker interface from `src/Njord.Core/Actors/ActorKeys.cs`
- [x] 6.4 Remove SensorHub registration from `src/Njord/NjordHost.cs` (SensorSetupContainer)
- [x] 6.5 Remove SensorHub telemetry meter from `src/Njord/Configuration/CoreSetupContainer.cs`

## 7. Update architecture and integration tests

- [x] 7.1 Update `src/Njord.Architecture.Tests/LayerReferenceSpec.cs` — remove Sensors and SensorHub from assembly reference rules
- [x] 7.2 Update `src/Njord.Architecture.Tests/NjordArchitecture.cs` — remove from assembly list
- [x] 7.3 Update `src/Njord.IntegrationTests/` — remove SensorService references
- [x] 7.4 Update `src/Njord.Tests/` host specs that reference SensorHub or SensorOptions

## 8. Update documentation

- [x] 8.1 Update `CLAUDE.md` — remove SensorHub, Sensors references, sensor fallback chain, SensorOptions
- [x] 8.2 Update `AGENTS.md` — remove SensorHub from architecture, pipeline description
- [x] 8.3 Update `docker-compose.example.yml` if it references sensor configuration

## 9. Validation

- [x] 9.1 `dotnet build Njord.slnx` — full solution compiles
- [x] 9.2 `dotnet run --project Njord.Indices.Tests/Njord.Indices.Tests.csproj` — index tests pass with config-only indoor temp
- [x] 9.3 `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj` — pipeline tests pass without sensor side-channel
- [x] 9.4 `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj` — gRPC tests pass without SensorService
- [x] 9.5 `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj` — architecture rules pass
- [x] 9.6 Run full test suite loop:
  ```bash
  for p in Njord.*Tests; do
    [ "$p" = Njord.Tests.Shared ] && continue
    [ "$p" = Njord.Sensors.Tests ] && continue
    [ "$p" = Njord.SensorHub.Tests ] && continue
    dotnet run --project "$p/$p.csproj" --no-build
  done
  ```
