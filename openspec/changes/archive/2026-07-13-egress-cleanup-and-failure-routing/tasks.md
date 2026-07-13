## 1. Dead Code Removal — Domain

- [x] 1.1 Remove `FriendlyName` from `ParameterDef` record (`src/Njord/Domain/ParameterDef.cs`), remove all `FriendlyName` arguments from `BuildAll()` in `ParameterRegistry` (`src/Njord/Domain/ParameterRegistry.cs`), remove the two-parameter `GetByApiName(string, ParameterGranularity)` overload. Update any test assertions that reference `FriendlyName` in `src/Njord.Tests/Domain/ParameterRegistrySpec.cs` and `src/Njord.Tests/Configuration/ParameterOptionsValidationSpec.cs`.

## 2. Dead Code Removal — Pipeline Commands

- [x] 2.1 Remove `RefreshModel` and `RefreshLocation` records from `PipelineCommand` (`src/Njord/Pipeline/PipelineCommand.cs`). Remove `OnRefreshModel` / `OnRefreshLocation` methods and both `Command<>` registrations (constructor + `BecomeReady`) from `SchedulerActor` (`src/Njord/Pipeline/SchedulerActor.cs`). Delete the corresponding tests in `src/Njord.Tests/Pipeline/SchedulerActorSpec.cs` (the `RefreshModel_offers_target_immediately`, `RefreshLocation_offers_all_models` tests and any helper `OnRefreshModel`/`OnRefreshLocation` in the test actor).

## 3. Failure Routing — FetchOutcome + Pipeline

- [x] 3.1 Add `string Location` and `WeatherModel Model` fields to `FetchOutcome.Failure` (`src/Njord/Ingest/FetchOutcome.cs`). Update all `FetchOutcome.Failure` construction sites in `OpenMeteoClient.FetchAsync` (`src/Njord/Ingest/OpenMeteoClient.cs`) to pass the location and model. Update `IOpenMeteoClient` if the signature needs it. Fix test usages in `src/Njord.Tests/Ingest/OpenMeteoClientSpec.cs` and `src/Njord.Tests/Pipeline/PollPipelineSpec.cs`.
- [x] 3.2 Widen `BroadcastHub` from `FetchOutcome.Success` to `FetchOutcome` in `PipelineActor` (`src/Njord/Pipeline/PipelineActor.cs`): change `BroadcastHub.Sink<FetchOutcome.Success>` → `BroadcastHub.Sink<FetchOutcome>`, remove the `.Collect(Success)` before the hub, update `SourceRef` type from `ISourceRef<FetchOutcome.Success>` to `ISourceRef<FetchOutcome>`. Add `.Collect(Success)` to the feedback consumer (hash computation) so it still filters successes. Update `PipelineSourceResponse` and `RequestPipelineSource` message types if typed.
- [x] 3.3 Update `MqttEgressActor` (`src/Njord/Egress/MqttEgressActor.cs`) consumer graph: the `sourceRef.Source` now carries `FetchOutcome` — add `.Collect(Success)` before the `SelectMany` that builds state payloads.

## 4. Failure Routing — Scheduler

- [x] 4.1 Add a BroadcastHub failure consumer in `SchedulerActor` (`src/Njord/Pipeline/SchedulerActor.cs`): after receiving the SinkRef, also request a `SourceRef<FetchOutcome>` from PipelineActor, materialize a consumer that routes `Failure` to a new `OnFetchFailure` handler. Implement reason-based retry: `Transport` → existing miss backoff, `RateLimited` → `max(5 min, backoff)`, `ModelUnavailable`/`MalformedPayload` → no retry (log warning). Add the `RequestPipelineSource` / `PipelineSourceResponse` exchange to SchedulerActor startup.
- [x] 4.2 Write tests for failure routing in `src/Njord.Tests/Pipeline/SchedulerActorSpec.cs`: transport failure triggers backoff, rate-limited enforces 5-min minimum, model-unavailable skips retry. Use BDD-style names, `[Fact(Timeout = 5000)]`, sealed class.

## 5. Discovery Toggle

- [x] 5.1 Add `bool DiscoveryEnabled { get; set; } = true` to `MqttOptions` (`src/Njord/Configuration/MqttOptions.cs`). Guard `PublishDiscovery()`, HA status subscription, and HA birth re-publish in `MqttEgressActor` behind `_options.Mqtt.DiscoveryEnabled`. When disabled, skip `_connection.SubscribeAsync(_haStatusTopic)` in `OnConnectedAsync` and skip `PublishDiscovery()`. Simplify `OnInboundAsync` — when disabled, the handler is a no-op (only HA birth remains after tombstone removal, and that's gated on discovery).
- [x] 5.2 Write tests for discovery toggle in a new `src/Njord.Tests/Egress/MqttEgressActorSpec.cs` or extend existing egress tests: discovery disabled → no discovery payloads published, no HA status subscription; discovery enabled → payloads published as before.

## 6. Tombstone Queue Removal

- [x] 6.1 Remove from `MqttEgressActor` (`src/Njord/Egress/MqttEgressActor.cs`): `_tombstoneQueue` field, `_ownConfigTopics` HashSet + constructor computation, `_deviceConfigFilter` field, `tombQueue`/`tombSource` materialization in `MaterializeEgressGraph`, `tombSource.RunWith(hubSink)`, `_tombstoneQueue?.Complete()` in `PostStop`, the `_connection.SubscribeAsync(_deviceConfigFilter)` call in `OnConnectedAsync`, and the stale-config detection block in `OnInboundAsync`. After this, `OnInboundAsync` only handles HA birth (guarded by discovery toggle from task 5.1).

## 7. Validation

- [x] 7.1 Build: `dotnet build Njord.slnx` from `src/`.
- [x] 7.2 Run all tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`.
- [x] 7.3 Run slopwatch: `dotnet slopwatch` from repo root.
