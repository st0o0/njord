## 1. MqttMessage Protocol & Interface Split

- [x] 1.1 Create `src/Njord/Egress/MqttMessage.cs` — `sealed record MqttMessage(string Topic, string Payload, bool Retain)`
- [x] 1.2 Create `src/Njord/Egress/IMqttConnection.cs` — interface with `ConnectAsync(callbacks)`, `SubscribeAsync`, extracted from current `IMqttPublisher`
- [x] 1.3 Create `src/Njord/Egress/IMqttTransport.cs` — interface with single `SendAsync(string topic, string payload, bool retain, CancellationToken ct)` for the publish sink
- [x] 1.4 Update `src/Njord/Egress/MqttNetPublisher.cs` to implement both `IMqttConnection` and `IMqttTransport`; remove old `IMqttPublisher` interface
- [x] 1.5 Create `src/Njord/Egress/EgressMessages.cs` — add `RequestEgressSink` and `EgressSinkResponse(SinkRef<MqttMessage>)` message types

## 2. MqttEgressActor (replaces MqttConnectionActor)

- [x] 2.1 Create `src/Njord/Egress/MqttEgressActor.cs` — actor that: materializes egress graph (MergeHub<MqttMessage> with Source.Queue inputs for discovery/availability/tombstone → Publish Sink via IMqttTransport), owns connection lifecycle (connect/reconnect/LWT), subscribes to HA status + device config wildcard, handles inbound messages, responds to `RequestEgressSink` with materialized SinkRef
- [x] 2.2 Create `src/Njord.Tests/Egress/MqttEgressActorSpec.cs` — tests: connect publishes online via hub, HA birth re-publishes discovery via hub, stale config tombstoned via hub, disconnect triggers reconnect, RequestEgressSink returns valid SinkRef, graceful stop publishes offline
- [x] 2.3 Remove old `src/Njord/Egress/MqttConnectionActor.cs`

## 3. PipelineActor (replaces PipelineHostedService)

- [x] 3.1 Create `src/Njord/Pipeline/PipelineActor.cs` — actor that: requests SinkRef from egress actor (Ask + Stash), materializes pipeline graph (MergeHub<Command> → Expand → Throttle → Fetch → Select(ToMqttMessage) → SinkRef.Sink) using Context.Materializer(), watches egress actor for Terminated → rematerializes
- [x] 3.2 Create `src/Njord.Tests/Pipeline/PipelineActorSpec.cs` — tests: actor waits for SinkRef before materializing, FetchOutcome.Success maps to MqttMessage via StreamRef, FetchOutcome.Failure is filtered, egress restart triggers rematerialization
- [x] 3.3 Remove `src/Njord/Pipeline/PublishStage.cs` (responsibility moved into pipeline graph's terminal Select + SinkRef)
- [x] 3.4 Remove `src/Njord/Pipeline/PipelineHostedService.cs`
- [x] 3.5 Update `src/Njord/Pipeline/PollPipeline.cs` — change return type to produce `Source<MqttMessage, NotUsed>` (commands → expand → throttle → fetch → map to MqttMessage) instead of materializing to KillSwitch; the PipelineActor attaches this to the SinkRef

## 4. Host Integration

- [x] 4.1 Update `src/Njord/Program.cs` — replace `AddHostedService<PipelineHostedService>` with Akka.Hosting actor registration for `MqttEgressActor` and `PipelineActor`; remove old `MqttConnectionActor` registration; ensure egress is registered before pipeline
- [x] 4.2 Update `src/Njord/Egress/EgressServiceCollectionExtensions.cs` — register `IMqttConnection` and `IMqttTransport` (both resolved from `MqttNetPublisher` singleton); remove old `IMqttPublisher` registration
- [x] 4.3 Remove old `src/Njord/Egress/IMqttPublisher.cs`

## 5. Test Cleanup

- [x] 5.1 Update `src/Njord.Tests/Egress/MqttEgressIntegrationSpec.cs` — adapt Testcontainers test to use new actor and verify end-to-end StreamRef flow
- [x] 5.2 Remove `src/Njord.Tests/Egress/MqttConnectionActorSpec.cs` (replaced by MqttEgressActorSpec)
- [x] 5.3 Update `src/Njord.Tests/Pipeline/PollPipelineSpec.cs` — adapt to new `PollPipeline` API that returns `Source<MqttMessage, NotUsed>`
- [x] 5.4 Remove `src/Njord.Tests/Pipeline/PublishStageSpec.cs` (PublishStage removed)

## 6. Validation

- [x] 6.1 Run full test suite: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj`
- [x] 6.2 Run build: `dotnet build src/Njord.slnx`
- [x] 6.3 Verify service starts cleanly: `dotnet run --project src/Njord/Njord.csproj` (with Njord__Mqtt__Host=localhost)
- [x] 6.4 Run `dotnet slopwatch` from repo root
