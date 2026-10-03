## Why

`AGENTS.md` (added by `restructure-agent-docs`) forbids `Task.ContinueWith` and Akka's `Status.Failure` in production code, but six production call sites still use them, and two main specs (`pipeline-actor`, `mqtt-actor-topology`) explicitly mandate `Status.Failure(ex)` as the failure reply. Until they are migrated, the rules ship with a "Known deviations" note and agents keep copying the old pattern.

## What Changes

- Replace `ContinueWith` in `MqttConnectionActor.Connect()` with `PipeTo(success:, failure:)` using the existing `Connected` / `ConnectFailed` messages.
- Replace the `Status.Success` / `Status.Failure` termination messages of the failure-consumer `Sink.ActorRef` in `SchedulerActor` with project-owned records and add handlers.
- Replace `Status.Failure` replies to `Request*Sink` / `Request*Source` with project-owned failure records (`EgressSinkFailed`, `EgressSourceFailed`, `MqttSinkFailed`, `PipelineSinkFailed`, `PipelineSourceFailed`, each `(long RequestId, Exception Cause)`); success records are unchanged.
- Add failure handling to every requester: log at Warning and re-request with capped exponential backoff (today the failure is silently unhandled). `WeatherGrpcService` maps a failure to `RpcException(StatusCode.Unavailable)`.
- Replace the test-side `ContinueWith` / `Status.Failure` usages.
- Remove the "Known deviations" note from `AGENTS.md`; the rules become unconditional.
- **BREAKING (in-process only):** the failure reply type of five request messages changes. No wire, persistence or gRPC-contract change.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `pipeline-actor`: failure reply to `RequestPipelineSink` / `RequestPipelineSource` is a typed failure record instead of `Status.Failure`.
- `mqtt-actor-topology`: failure reply to `RequestMqttSink` is `MqttSinkFailed` instead of `Status.Failure`.
- `stream-composition`: new requirement that ref requesters handle typed failures with backoff, and that stream-termination messages are project-owned.

## Impact

- Code: `src/Njord/Egress/EgressActor.cs`, `EgressMessages.cs`, `Mqtt/MqttConnectionActor.cs`, `Mqtt/MqttSinkResponse.cs`, `Pipeline/PipelineActor.cs`, `Pipeline/SchedulerActor.cs`, `Pipeline/SchedulerMessages.cs`; requesters `Egress/ModelStateActor.cs`, `Mqtt/DiscoveryActor.cs`, `Mqtt/MqttEgressActor.cs`, `Enrichment/EnrichmentActor.cs`, `Grpc/GrpcSnapshotConsumerActor.cs`, `Grpc/WeatherGrpcService.cs`.
- Tests: `Njord.Tests/Egress/EgressActorSpec.cs`, `Njord.Tests/Pipeline/PipelineConnectionSpec.cs`, plus new/extended specs per actor.
- Docs: `AGENTS.md`.
- API budget: none. Polling is not added or altered (0 additional requests/month against the 300k free-tier limit).
- Depends on: `restructure-agent-docs` applied first (creates `AGENTS.md` and the "Known deviations" note).

## Non-goals

- Introducing abstract response base types (`VerbNounResponse` hierarchy); success record names stay.
- Restructuring where messages live (nested vs `*Messages.cs`).
- Changing supervision, stream topology, or SourceRef/SinkRef lifecycles.
- Enforcing the rules via analyzers or ArchUnit.
- Test-side `EventStream.Subscribe` for DeadLetter/Warning probes (allowed).
