## 1. Consumer base (src/Njord.Core/Actors/StreamConsumerActor.cs)

- [x] 1.1 Specs first in `src/Njord.Core.Tests/Actors/StreamConsumerActorSpec.cs` (replacing the dependency/dead-ref specs and `StreamConsumerRewireSpec.cs`): attach on start; queries answered before the first SourceRef; late registration; unanswered and failed requests retried with a new request id; stale reply ignored; re-attach after pipeline `Terminated` with the old subscription shut down; untracked `Terminated` ignored; re-attach after own graph failure/completion; replaced-graph completion ignored; stop does not re-attach; backoff grows; stability window resets it; re-attach counted once per lost subscription. Run and see them fail.
- [x] 1.2 Rewrite `StreamConsumerActor`: single phase, `IWithTimers`, `MaterializeGraph(Source<PipelineEvent, NotUsed>)`, request id as subscription id, watch the replying incarnation, kill switch per subscription, reply timeout, capped backoff with stability reset, rewire counter, Debug log of the received SourceRef.
- [x] 1.3 The Core spec plays the pipeline itself (a probe as registry entry, a second probe as the answering incarnation, SourceRefs on the system materializer), so `FakePipelineOutput` stays unchanged.

## 2. Consumers

- [x] 2.1 Specs first: `ForecastReadModelSpec` - stream requested before the first subscription is answered; `GatewayRestartSpec` - a real `ForecastReadModel` attached to the pipeline sends exactly one `RequestPipelineOutput` across a gateway restart and receives the trend after it; `MqttBackpressureSpec` (src/Njord.Mqtt.Tests) - a never-completing transport with a small `EventBufferSize` while a second consumer of the same hub receives every one of many events. Run and see them fail.
- [x] 2.2 `ForecastReadModel`: hub + drain in `PreStart`, queries and stream requests in the constructor, subscription graph into the MergeHub; drop the handshake code.
- [x] 2.3 `MqttStatePublisher`: `Buffer(EventBufferSize, DropHead)` → mapping → `Sink.ForEachAsync(1, dedup + publish)`; `MqttEgressTuning.EventBufferSize` (default 256); drop the handshake code.
- [x] 2.4 Existing specs still pass unchanged in their assertions: `MqttStateSpec`, `MqttStateRewireSpec`, `MqttPublishFailureSpec`, `ForecastReadModelSpec`, `ForecastReadModelRewireSpec`.

## 3. Pipeline start inputs

- [x] 3.1 Specs first: `ForecastPipelineSettingsSpec` - a started pipeline uses the config manager's current settings, and starts with the startup settings when none is registered; `ForecastPipelineSensorSpec` - a started pipeline uses the SensorHub's current map; `SensorHubSpec` (src/Njord.SensorHub.Tests) - `RequestSensorSnapshots` is answered to the sender; `ConfigManagerSpec` (src/Njord.Tests) - `QueryAnalysisSettings` answers the current settings. Run and see them fail.
- [x] 3.2 Add `QueryAnalysisSettings` / `AnalysisSettingsResponse` / `AnalysisSettingsResult` (src/Njord.Messages/Config/QueryAnalysisSettings.cs) and `RequestSensorSnapshots` (src/Njord.Messages/Sensors/RequestSensorSnapshots.cs).
- [x] 3.3 `ConfigManager` answers `QueryAnalysisSettings`; `SensorHub` answers `RequestSensorSnapshots`; `ForecastPipeline.PreStart` sends both via `TryGetActor` (Debug log when missing) and forwards `AnalysisSettingsResult` to the settings side channel.

## 4. Cleanup and docs

- [x] 4.1 Remove `Ack` and `src/Njord.Messages/Common/`; drop the unused `using Njord.Messages.Common;` in the PollScheduler specs.
- [x] 4.2 `AGENTS.md`: consumer paragraph of the architecture guardrails, `Njord.Messages` domain folders, current test total.

## 5. Validation

- [x] 5.1 From `src/`: `dotnet build Njord.slnx` with 0 warnings and 0 errors.
- [x] 5.2 `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj`, `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj`, `dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj` three times each without failures.
- [x] 5.3 Full loop over every `Njord.*Tests` project (AGENTS.md); architecture tests green; no `*.verified.*` changes.
- [x] 5.4 Manual run: `cd src/Njord && timeout -s INT 60 dotnet run` with `src/Njord/data` backed up and restored byte-identically; forecasts analyzed and reaching the read model, no njord dead letters or unhandled messages.
- [x] 5.5 `openspec validate --all --no-interactive`.
