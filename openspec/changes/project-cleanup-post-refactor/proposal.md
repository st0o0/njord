## Why

The recent major refactors — `typed-pipeline-stream`, `wire-pipeline-analysis`,
`actor-architecture-cleanup` — left behind dead code: unused types, orphaned
message variants, stale DTOs, unused NuGet packages, empty project directories,
and inaccurate AGENTS.md references. This cleanup removes all verified-dead
artifacts and corrects documentation before any new feature work begins.

## What Changes

- **Delete 6 entire dead files**: `AccumulatedForecast.cs`, `ForecastRecord.cs`,
  `SensorComponentSpec.cs`, `QueryAllForecasts.cs`, `UpdateForecast.cs`,
  `Grpc/ServiceCollectionExtensions.cs`
- **Remove 3 dead types from live files**: `PipelineEvent.ModelCapability`,
  `HaBirthDetected`, `StreamsStopFailed` (update `PipelineSetupContainer` to
  `Ask<StreamsStopped>` instead of `Ask<object>`)
- **Remove dead class + test**: `NjordMessageExtractor` +
  `NjordMessageExtractorSpec`
- **Remove dead health-state property**: `MqttConnectedSince` +
  `_mqttConnectedSinceTicks` field (written but never read; health checks use
  only `IsMqttConnected` and `MqttDisconnectedSince`)
- **Remove unused Scheduler snapshot infrastructure**: `SchedulerSnapshotDto`,
  `ModelPollStateDto`, `SchedulerDtoMapping.ToSnapshot`/`FromSnapshot`,
  `SchedulerDtoSerializationSpec`, `SchedulerDtoRoundtripSpec`
  (SchedulerActor explicitly ignores snapshots)
- **PipelineActor cleanup**: remove unused `IWithStash`, fix indentation on
  line 192, move `StreamSupervisionSpec` to `Njord.Core.Tests`
- **Remove 5 unused NuGet packages** from `Directory.Packages.props`:
  `Google.Protobuf`, `Grpc.Net.Client`, `Grpc.Tools`,
  `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers`
- **Fix 4 stale AGENTS.md references**: `IMqttPublisher` → `IMqttTransport`,
  "No GraphDSL" → clarify ZipWith exception, `NjordActorSystemSetup` →
  `AkkaSetupContainer`, `SensorService` → `SensorGrpcService`
- **Delete 3 empty project directories**: `Njord.Domain/`, `Njord.AppHost/`,
  `Njord.ServiceDefaults/`

## Non-goals

- Removing `*Failed` message variants (`QueryBudgetUsageFailed`,
  `QueryPollStatesFailed`, `TriggerImmediatePollFailed`,
  `QueryForecastFailed`) — kept by convention; cost is 1 line each.
- Adding `PipeTo` error handling for `OfferAsync` in PipelineActor — backpressure
  is practically impossible at current poll rates; deferred as an observability
  improvement.
- Updating archived OpenSpec specs that reference removed DTOs — archive is
  historical record.

## Capabilities

### New Capabilities

(none — pure cleanup)

### Modified Capabilities

- `pipeline-actor`: Remove `IWithStash`, fix indentation, remove
  `PipelineEvent.ModelCapability`
- `health-checks`: Remove unused `MqttConnectedSince` property
- `snapshot-persistence-dtos`: Remove unused `SchedulerSnapshotDto` /
  `ModelPollStateDto` and their mappings/tests
- `message-domain-separation`: Remove dead `StreamsStopFailed`,
  `HaBirthDetected`; update shutdown protocol to typed `Ask<StreamsStopped>`

## Impact

- **Projects touched**: Njord.Weather, Njord.Compute, Njord.Mqtt,
  Njord.Messages, Njord.Grpc, Njord.Core, Njord.Pipeline,
  Njord.Persistence, Njord.Core.Tests, Njord.Pipeline.Tests,
  Njord.Persistence.Tests
- **Test count**: net reduction (removing NjordMessageExtractorSpec,
  SchedulerDtoSerializationSpec, SchedulerDtoRoundtripSpec; moving
  StreamSupervisionSpec)
- **NuGet packages**: 5 removed from central props
- **No API or runtime behavior changes**
- **No polling changes** — no budget impact
