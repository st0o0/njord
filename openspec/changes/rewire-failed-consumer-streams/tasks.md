## 1. Evidence and contract

- [ ] 1.1 Add subclass specs that compile against the current contract and prove the silent-dead behavior: `src/Njord.Mqtt.Tests/MqttStateRewireSpec.cs` (pipeline SourceRef fails / completes → later events are never published) and `src/Njord.Pipeline.Tests/ForecastReadModelRewireSpec.cs` (subscription fails → newer events never reach queries); record the failures
- [ ] 1.2 Add `src/Njord.Pipeline.Tests/ForecastPipelineRewireSpec.cs` (forecast SourceRef fails / completes while the gateway lives → no new `RequestForecastStream`; stale handshake reply ignored); record the failures
- [ ] 1.3 Contract change only: `MaterializeGraph` returns `Task` in `src/Njord.Core/Actors/StreamConsumerActor.cs`; update `MqttStatePublisher`, `ForecastReadModel` and the test subclasses in `src/Njord.Core.Tests/Actors/StreamConsumerActorSpec.cs` (base still ignores the task)

## 2. Base-class specs (failing first)

- [ ] 2.1 Add `src/Njord.Core.Tests/Actors/StreamConsumerRewireSpec.cs` (sealed, hosted TestKit, `[Fact(Timeout = TestTimeouts.Hosted)]`): failed graph → new SourceRef request and materialization; completed graph → rewire; completion of a graph replaced after `Terminated` ignored; actor stop → no rewire; growing backoff on repeated immediate failures; counter incremented with the `consumer` tag; record the failures
- [ ] 2.2 Add `src/Njord.Core.Tests/TelemetrySpec.cs` (plain `[Fact]`) for the counter's name, unit and meter

## 3. Implementation

- [ ] 3.1 `src/Njord.Core/Telemetry.cs` with `Meter("Njord.Core")` and `njord_stream_consumer_rewires_total`; add `Njord.Core` to `NjordMeters` in `src/Njord/Configuration/CoreSetupContainer.cs`
- [ ] 3.2 `StreamConsumerActor`: generation-tagged `GraphCompleted`/`GraphFailed` via `PipeTo`, `RewireGraph`, `GraphStable`, separate graph-loss backoff counter, `OnStreamLost()`, `ConsumerName`, generation bump in `HandleTerminated` and `PostStop`
- [ ] 3.3 `MqttStatePublisher`: return the sink completion, `OnStreamLost()` drops the SourceRef; dedup cache stays per materialization
- [ ] 3.4 `ForecastReadModel`: secondary hub fed by a `MergeHub` materialized once per incarnation; subscription returns its `WatchTermination` and recovers before the `MergeHub`; `OnStreamLost()` drops the SourceRef; state kept
- [ ] 3.5 `ForecastPipeline`: analysis graph completion via `WatchTermination`, generation-tagged messages, backoff with stability reset, `njord_pipeline_stream_rewires_total` in `src/Njord.Pipeline/Telemetry.cs`, request-id correlation of the forecast handshake

## 4. Validation

- [ ] 4.1 From `src/`: `dotnet build Njord.slnx` → 0 warnings, 0 errors
- [ ] 4.2 `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj`, `dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj`, `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj` three times each, all green
- [ ] 4.3 Full test loop from `AGENTS.md` green; update "Current total" in `AGENTS.md`
- [ ] 4.4 No `*.verified.*` changes; `openspec validate rewire-failed-consumer-streams --no-interactive`
