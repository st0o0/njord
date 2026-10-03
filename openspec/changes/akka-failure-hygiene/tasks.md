## 1. Phase A: ContinueWith and Sink.ActorRef messages (no protocol change)

- [ ] 1.1 Write failing `src/Njord.Tests/Mqtt/MqttConnectionActorSpec.cs` spec: a connect attempt that throws results in a `ConnectFailed`-driven reconnect schedule (and that a canceled connect does the same)
- [ ] 1.2 Replace `ContinueWith` in `src/Njord/Mqtt/MqttConnectionActor.cs` (`Connect()`, ~line 156) with `PipeTo(self, success: () => new Connected(), failure: ex => new ConnectFailed(ex))`; make 1.1 pass
- [ ] 1.3 Add `FailureConsumerCompleted` and `FailureConsumerFailed(Exception Cause)` to `src/Njord/Pipeline/SchedulerMessages.cs`
- [ ] 1.4 Write failing `src/Njord.Tests/Pipeline/SchedulerActorSpec.cs` specs: completion message is handled without stashing; failure message triggers re-resolve of the pipeline
- [ ] 1.5 Use the new records in `Sink.ActorRef` in `src/Njord/Pipeline/SchedulerActor.cs` (`OnSourceReceived`, ~line 248) and add handlers; make 1.4 pass
- [ ] 1.6 Replace `ContinueWith` in `src/Njord.Tests/Egress/EgressActorSpec.cs:49` with awaiting the `RunForeach` task (`WaitAsync` timeout)
- [ ] 1.7 Replace Akka `Status.Success`/`Status.Failure` in the test actor at `src/Njord.Tests/Pipeline/PipelineConnectionSpec.cs:318` with local records
- [ ] 1.8 Run the Phase A validation commands (section 5) and `dotnet slopwatch`

## 2. Phase B: typed failure replies for Request*Sink / Request*Source

- [ ] 2.1 Add `EgressSinkFailed` and `EgressSourceFailed` to `src/Njord/Egress/EgressMessages.cs`, `MqttSinkFailed` to `src/Njord/Mqtt/MqttSinkResponse.cs`, `PipelineSinkFailed` and `PipelineSourceFailed` to `src/Njord/Pipeline/SchedulerMessages.cs` (all `sealed record X(Exception Cause)`)
- [ ] 2.2 Write failing producer specs: `EgressActorSpec` (sink/source materialization failure replies `EgressSinkFailed`/`EgressSourceFailed`), `MqttConnectionActorSpec` (`MqttSinkFailed`), `PipelineActor` spec (`PipelineSinkFailed`/`PipelineSourceFailed`, Error logged); use a failing materializer or a stopped hub to force the failure
- [ ] 2.3 Replace `Status.Failure` in `src/Njord/Egress/EgressActor.cs:33,48`, `src/Njord/Mqtt/MqttConnectionActor.cs:~105`, `src/Njord/Pipeline/PipelineActor.cs:~72,~84` with the new records; keep the existing Error logging; make 2.2 pass
- [ ] 2.4 Write failing requester specs (one per actor, `[Fact(Timeout = 5000)]`, probe sends the failure message): `ModelStateActorSpec`, `DiscoveryActorSpec`, `MqttEgressActorSpec`, `EnrichmentActor` spec, `GrpcSnapshotConsumerActor` spec each expect a re-request after backoff (FakeTimeProvider/scheduler as in existing specs); `SchedulerActorSpec` expects re-resolve + re-request after `PipelineSinkFailed`/`PipelineSourceFailed` in `WaitingForRefs`
- [ ] 2.5 Add `Receive<XFailed>` handlers (Warning log + `ScheduleRetryResolve()`) to `src/Njord/Egress/ModelStateActor.cs`, `src/Njord/Mqtt/DiscoveryActor.cs`, `src/Njord/Mqtt/MqttEgressActor.cs`, `src/Njord/Enrichment/EnrichmentActor.cs`, `src/Njord/Grpc/GrpcSnapshotConsumerActor.cs`; check each behaviour where a request can be outstanding (not only the waiting one)
- [ ] 2.6 Add the failure handlers in `src/Njord/Pipeline/SchedulerActor.cs` (`WaitingForRefs`) using the existing `RetryPipelineResolve` backoff; make 2.4 pass
- [ ] 2.7 Write failing `src/Njord.Tests/Grpc/WeatherGrpcServiceSpec.cs` specs: `StreamForecasts` and `StreamEnrichments` throw `RpcException` with `StatusCode.Unavailable` when the egress actor replies `EgressSourceFailed`
- [ ] 2.8 Update `src/Njord/Grpc/WeatherGrpcService.cs` (`StreamForecasts`, `StreamEnrichments`) to `Ask<object>` + pattern match via one private helper; make 2.7 pass; grep `openspec/specs/grpc-*` for status-code assertions and add a delta if any conflicts
- [ ] 2.9 Run the Phase B validation commands (section 5) and `dotnet slopwatch`

## 3. Phase C: docs

- [ ] 3.1 Remove the "Known deviations" note from the Akka conventions section of `AGENTS.md`; confirm `grep -rnE "Status\.Failure|ContinueWith" src/Njord` returns nothing and `src/Njord.Tests` has no matches except none (all migrated)
- [ ] 3.2 Run `openspec validate akka-failure-hygiene`

## 4. Commit

- [ ] 4.1 One Conventional Commit per phase (`refactor: ...`); do not push

## 5. Validation

- [ ] 5.1 From `src/`: `dotnet build Njord.slnx`
- [ ] 5.2 From `src/`: `dotnet run --project Njord.Tests/Njord.Tests.csproj` (full suite green)
- [ ] 5.3 From `src/`: `dotnet run --project Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Mqtt.MqttConnectionActorSpec"`
- [ ] 5.4 From `src/`: `dotnet run --project Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Pipeline.SchedulerActorSpec"`
- [ ] 5.5 From `src/`: `dotnet run --project Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Egress.EgressActorSpec"`
- [ ] 5.6 From `src/`: `dotnet run --project Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Grpc.WeatherGrpcServiceSpec"`
- [ ] 5.7 From the repo root: `dotnet slopwatch`
