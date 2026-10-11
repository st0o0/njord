## 1. Delete entire dead files

- [x] 1.1 Delete `src/Njord.Weather/AccumulatedForecast.cs`
- [x] 1.2 Delete `src/Njord.Compute/Analysis/ForecastRecord.cs`
- [x] 1.3 Delete `src/Njord.Mqtt/SensorComponentSpec.cs`
- [x] 1.4 Delete `src/Njord.Messages/Snapshots/QueryAllForecasts.cs`
- [x] 1.5 Delete `src/Njord.Messages/Snapshots/UpdateForecast.cs`
- [x] 1.6 Delete `src/Njord.Grpc/ServiceCollectionExtensions.cs`

## 2. Remove dead types from live files

- [x] 2.1 Remove `ModelCapability` variant from `src/Njord.Core/Egress/PipelineEvent.cs`. Update `src/Njord.Mqtt.Tests/MqttStateActorSpec.cs` if it constructs this type
- [x] 2.2 Remove `HaBirthDetected` from `src/Njord.Messages/Mqtt/MqttMessages.cs`
- [x] 2.3 Remove `StreamsStopFailed` from `src/Njord.Core/Actors/StreamShutdownMessages.cs`. Update `src/Njord.Pipeline/Configuration/PipelineSetupContainer.cs`: change `Ask<object>(new StopStreams(), askTimeout)` to `Ask<StreamsStopped>(new StopStreams(), askTimeout)` and remove the `if (reply is StreamsStopFailed)` check

## 3. Remove dead class + test

- [x] 3.1 Delete `src/Njord.Core/Actors/NjordMessageExtractor.cs`
- [x] 3.2 Delete `src/Njord.Core.Tests/Actors/NjordMessageExtractorSpec.cs`

## 4. Remove dead health state property

- [x] 4.1 In `src/Njord.Core/Health/NjordHealthState.cs`: remove `_mqttConnectedSinceTicks` field, remove `MqttConnectedSince` property, update `SetMqttConnected` to only set `_isMqttConnected = 1` and clear `_mqttDisconnectedSinceTicks` (remove the `Interlocked.Exchange(ref _mqttConnectedSinceTicks, ...)` line)

## 5. Remove unused Scheduler snapshot infrastructure

- [x] 5.1 Delete `src/Njord.Persistence/Pipeline/SchedulerSnapshotDto.cs`
- [x] 5.2 Delete `src/Njord.Persistence/Pipeline/ModelPollStateDto.cs`
- [x] 5.3 In `src/Njord.Pipeline/SchedulerDtoMapping.cs`: remove `ToSnapshot` and `FromSnapshot` methods (keep `ToDto`/`FromDto`)
- [x] 5.4 Delete `src/Njord.Pipeline.Tests/SchedulerDtoSerializationSpec.cs`
- [x] 5.5 Delete `src/Njord.Persistence.Tests/SchedulerDtoRoundtripSpec.cs`

## 6. PipelineActor cleanup

- [x] 6.1 In `src/Njord.Pipeline/PipelineActor.cs`: remove `IWithStash` from class declaration, remove `public IStash Stash { get; set; } = null!;` property
- [x] 6.2 In `src/Njord.Pipeline/PipelineActor.cs`: fix indentation on line 192 (32 spaces → match surrounding 20-space indent)
- [x] 6.3 Move `src/Njord.Pipeline.Tests/StreamSupervisionSpec.cs` to `src/Njord.Core.Tests/StreamSupervisionSpec.cs`. Update namespace to `Njord.Core.Tests`

## 7. Remove unused NuGet packages

- [x] 7.1 Remove `Google.Protobuf`, `Grpc.Net.Client`, `Grpc.Tools`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers` from `src/Directory.Packages.props`

## 8. Fix stale AGENTS.md references

- [x] 8.1 Replace `IMqttPublisher` with `IMqttTransport` in `AGENTS.md`
- [x] 8.2 Update "No `GraphDSL`, `Broadcast`, or `Merge` in the pipeline" to "No `Broadcast` or `Merge` in the pipeline (`GraphDsl.Create` is used only for the `ZipWith` sensor side-channel)"
- [x] 8.3 Replace `NjordActorSystemSetup` with `AkkaSetupContainer` in `AGENTS.md`
- [x] 8.4 Replace `SensorService` with `SensorGrpcService` in `AGENTS.md`

## 9. Remove dead project directories

- [x] 9.1 Delete `src/Njord.Domain/` directory (empty, not in slnx)
- [x] 9.2 Delete `src/Njord.AppHost/` directory (empty, not in slnx)
- [x] 9.3 Delete `src/Njord.ServiceDefaults/` directory (empty, not in slnx)

## 10. Validation

- [x] 10.1 Run `dotnet build Njord.slnx` from `src/` — must compile cleanly
- [x] 10.2 Run all test projects sequentially and verify all pass:
  ```
  for p in Njord.*Tests; do
    [ "$p" = Njord.Tests.Shared ] && continue
    dotnet run --project "$p/$p.csproj" --no-build
  done
  ```
- [x] 10.3 Run `dotnet format Njord.slnx --verify-no-changes` from `src/`
- [x] 10.4 Update test count in `CLAUDE.md` if it changed
