## 1. Revert debug changes

- [x] 1.1 Revert all uncommitted debug changes (epoch API, logging, `RecoverAfterTerminated`, `PostStop`/`PostRestart` overrides) in `src/Njord/Actors/StreamConsumerActor.cs`, `src/Njord/Mqtt/MqttEgressActor.cs`, `src/Njord/Mqtt/DiscoveryActor.cs`, `src/Njord/Egress/ModelStateActor.cs`, `src/Njord/Enrichment/EnrichmentActor.cs`, `src/Njord/Grpc/GrpcSnapshotConsumerActor.cs` — restore to committed HEAD state plus the `TryTransition` fix from `5e6de78`

## 2. Add RequestId to messages

- [x] 2.1 Add `long RequestId` to `RequestMqttSink` and `MqttSinkResponse` in `src/Njord/Mqtt/RequestMqttSink.cs` and `src/Njord/Mqtt/MqttSinkResponse.cs`
- [x] 2.2 Add `long RequestId` to `RequestEgressSink`, `EgressSinkResponse`, `RequestEgressSource`, `EgressSourceResponse` in `src/Njord/Egress/EgressMessages.cs`
- [x] 2.3 Add `long RequestId` to `RequestPipelineSource`, `PipelineSourceResponse` in `src/Njord/Pipeline/SchedulerMessages.cs` (skip `RequestPipelineSink`/`PipelineSinkResponse` if only used internally — check usage)

## 3. Update responding actors to echo RequestId

- [x] 3.1 `src/Njord/Mqtt/MqttConnectionActor.cs`: pass `msg.RequestId` into `MqttSinkResponse` in the `Receive<RequestMqttSink>` handler
- [x] 3.2 `src/Njord/Egress/EgressActor.cs`: pass `msg.RequestId` into `EgressSinkResponse` and `EgressSourceResponse` in the respective handlers
- [x] 3.3 `src/Njord/Pipeline/PipelineActor.cs`: pass `msg.RequestId` into `PipelineSourceResponse` (and `PipelineSinkResponse` if changed) in the respective handlers

## 4. Update StreamConsumerActor base class

- [x] 4.1 Add `private long _requestId` field and `protected long NextRequestId() => ++_requestId` method
- [x] 4.2 In `HandleTerminated`: increment `_requestId` (step 6, before `OnDependencyLost` and `ResolveDependencies`)
- [x] 4.3 In `TryTransition`: remove the `_lastTerminatedRef is not null` guard (keep the `AllRefsReady()` check), clear `_lastTerminatedRef` on success (the fix from `5e6de78`)
- [x] 4.4 Remove the epoch API (`_resolveEpoch`, `CurrentEpoch`, `IsStaleEpoch`) if present in committed code — these belong to the debug branch only

## 5. Update subclasses: store and check RequestId

- [x] 5.1 `src/Njord/Mqtt/MqttEgressActor.cs`: add `_mqttSinkRequestId` and `_egressSourceRequestId` fields; in `ConnectionResolved` handler use `var id = NextRequestId(); _mqttSinkRequestId = id;` then send `new RequestMqttSink(id)`; in `EgressResolved` handler same for egress; in response handlers check `response.RequestId != _xxxRequestId` → return
- [x] 5.2 `src/Njord/Mqtt/DiscoveryActor.cs`: same pattern — `_mqttSinkRequestId`, `_egressSourceRequestId`; send with `NextRequestId()`, check in response handlers
- [x] 5.3 `src/Njord/Egress/ModelStateActor.cs`: `_egressSinkRequestId`, `_pipelineSourceRequestId`; send with `NextRequestId()`, check in response handlers
- [x] 5.4 `src/Njord/Enrichment/EnrichmentActor.cs`: `_pipelineSourceRequestId`, `_egressSinkRequestId`; send with `NextRequestId()`, check in response handlers. `SensorHubResolved` handler unchanged (no request/response pattern)
- [x] 5.5 `src/Njord/Grpc/GrpcSnapshotConsumerActor.cs`: `_egressSourceRequestId`; send with `NextRequestId()`, check in `EgressSourceResponse` handler. `SnapshotActorsResolved` handler checks `_egressSourceRequestId` against a stored value (the chain only fires from a validated response)

## 6. Update test fakes to echo RequestId

- [x] 6.1 `src/Njord.Tests/Mqtt/MqttEgressActorSpec.cs`: update `MqttMessageProbe` to receive `RequestMqttSink` with `RequestId` and echo it in `MqttSinkResponse`
- [x] 6.2 `src/Njord.Tests/Mqtt/MqttEgressActorSpec.cs`: update `FakeEgressSourceProvider` to echo `RequestId` in `EgressSourceResponse`
- [x] 6.3 `src/Njord.Tests/Mqtt/DiscoveryActorSpec.cs`: update `MqttMessageProbe` and `FakeEgressSourceProvider` fakes
- [x] 6.4 `src/Njord.Tests/Actors/StreamConsumerActorSpec.cs`: update `TestStreamConsumer` and `ResettableTestStreamConsumer` — these don't use request/response messages (only `*Resolved` handlers), so they may need no change — verify
- [x] 6.5 Update any other test fakes in `src/Njord.Tests/` and `src/Njord.Tests.Shared/` that construct `Request*` or `*Response` messages — search for all usages

## 7. Validation

- [x] 7.1 `cd src && dotnet build Njord.slnx` — zero errors, zero warnings
- [x] 7.2 `cd src && dotnet run --project Njord.Tests/Njord.Tests.csproj` — all 705+ tests pass
- [x] 7.3 Run `MqttEgressActorSpec.Should_re_request_refs_after_watched_actor_terminates` 20× in a loop — 20/20 pass
- [x] 7.4 Run `StreamConsumerActorSpec.Retry_count_resets_on_successful_transition` 20× in a loop — 20/20 pass
- [x] 7.5 `cd src && dotnet format whitespace --verify-no-changes Njord.slnx` — clean
