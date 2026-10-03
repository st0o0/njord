## Context

See proposal.md for motivation. Survey and feasibility are in `restructure-agent-docs` design.md, Decision 4; this design turns it into an ordered plan. Facts re-verified against the code:

- `PipeTo(Sender, Self, success, failure)` is used at `Egress/EgressActor.cs:33,48`, `Mqtt/MqttConnectionActor.cs:~100`, `Pipeline/PipelineActor.cs:~66,~78`; the failure branch returns `new Status.Failure(ex)` to the requester.
- Requesters: `Egress/ModelStateActor.cs`, `Mqtt/DiscoveryActor.cs`, `Mqtt/MqttEgressActor.cs`, `Enrichment/EnrichmentActor.cs`, `Grpc/GrpcSnapshotConsumerActor.cs` (all `StreamConsumerActor` subclasses), `Pipeline/SchedulerActor.cs` (own state machine), and `Grpc/WeatherGrpcService.cs` (`Ask<EgressSourceResponse>` in `StreamForecasts`/`StreamEnrichments`).
- None of the `Tell`-based requesters has a `Receive` for a failure; `Status.Failure` hits the `ReceiveAny(_ => Stash.Stash())` fallback of `StreamConsumerActor` and is stashed forever.
- `StreamConsumerActor` already has `ScheduleRetryResolve()` (backoff `min(2^n, 30)s`, `Actors/StreamConsumerActor.cs:52`); `SchedulerActor` has `RetryPipelineResolve` with the same formula.
- Existing specs mandate `Status.Failure` in `pipeline-actor` and `mqtt-actor-topology` (delta specs here).
- `EgressActor` has no spec covering its ref-vending failure path today.

## Goals / Non-Goals

**Goals:** remove all six production deviations; make failure replies handled instead of stashed; keep each phase independently shippable.

**Non-Goals:** see proposal.md. Additionally: no new retry policy; reuse the existing backoff.

## Decisions

### 1. Three phases, one commit-sized group each
(a) mechanical: `ContinueWith` + `Sink.ActorRef` messages. (b) protocol: failure records + requester handlers + gRPC mapping. (c) docs: drop the "Known deviations" note in `AGENTS.md`. Phase (a) needs no protocol change and can land alone.

### 2. Keep success record names; add `XxxFailed` siblings (no abstract base)
`EgressSinkResponse` etc. stay. New: `EgressSinkFailed`, `EgressSourceFailed` (`Egress/EgressMessages.cs`), `MqttSinkFailed` (`Mqtt/MqttSinkResponse.cs`), `PipelineSinkFailed`, `PipelineSourceFailed` (`Pipeline/SchedulerMessages.cs`), all `sealed record X(long RequestId, Exception Cause)`. The `RequestId` mirrors the success records so requesters ignore stale failures with the same check they use for stale responses.
*Alternative:* FunkArr-style `abstract record XResponse` with `Completed`/`Failed`. Rejected: renames/rewraps every success record and ~30 test usages for no behavioral gain; revisit if a third outcome appears.

### 3. Requester handling reuses existing backoff
(`StreamConsumerActor.ScheduleRetryResolve()` now de-duplicates pending retries, otherwise two failing dependencies would each schedule a retry and double the request count per round.)
`StreamConsumerActor` subclasses: `Receive<XFailed>` logs Warning and calls `ScheduleRetryResolve()`, which re-resolves dependencies and re-sends the request. `SchedulerActor`: handler in `WaitingForRefs` that logs and schedules `RetryPipelineResolve` using the existing exponential delay. Failure messages also need a stash/`Receive` in the base `ReceiveAny` window: handlers must be registered in both the waiting and ready behaviours where the request may still be outstanding (verify per actor during implementation).
*Alternative:* new shared retry helper. Rejected: two working mechanisms already exist.

### 4. gRPC: map failure to `RpcException(Unavailable)`
`WeatherGrpcService` uses `Ask<object>` and pattern-matches `EgressSourceResponse` / `EgressSourceFailed`; failure becomes `RpcException(new Status(StatusCode.Unavailable, ...))` (the gRPC `Status`, not Akka's). Previously an Ask faulted with the pipe exception, which gRPC surfaced as `Unknown`; `Unavailable` is the more accurate and retry-friendly code. A small private helper avoids duplicating the match in both stream methods.

### 5. `ContinueWith` replacement
`ConnectAsync(...).PipeTo(self, success: () => new Connected(), failure: ex => new ConnectFailed(ex))`. Behavior difference to cover with a spec: the old code unwrapped `AggregateException` via `GetBaseException()` and mapped cancellation to `InvalidOperationException("connect canceled")`; the new path delivers the raw exception (cancellation as `TaskCanceledException`). `ConnectFailed.Cause` is only logged, so this is acceptable; assert it in the spec.

### 6. Sink.ActorRef termination messages
`Sink.ActorRef<FetchFailed>(self, new FailureConsumerCompleted(), ex => new FailureConsumerFailed(ex))` (records in `Pipeline/SchedulerMessages.cs`). Today `Status.Success`/`Status.Failure` arrive unhandled. New handlers: completed → log Debug; failed → log Warning and treat like loss of the pipeline source (same path as `Terminated` on the pipeline: kill switch + re-resolve). Verify that path during implementation.

### 7. Test-side changes
`EgressActorSpec.cs:49`: replace `.ContinueWith(_ => tcs.TrySetResult())` by awaiting the `RunForeach` task (with `WaitAsync(timeout)`). `PipelineConnectionSpec.cs:318`: replace Akka `Status` messages in the test actor with local records or `Sink.ActorRef` messages. `EventStream.Subscribe` in `StreamConsumerActorSpec.cs:199` and `SchedulerActorSpec.cs:180` stay (documented as allowed).

## Risks / Trade-offs

- [A requester misses a failure message because it is in a behaviour without a handler and stashes it] → add a spec per requester sending the failure in each behaviour where a request can be outstanding; check `ReceiveAny` fallbacks.
- [Retry storm if materialization fails persistently] → existing backoff caps at 30 s; failures are also logged at Error by the producer.
- [gRPC clients see `Unavailable` instead of `Unknown`] → intended; check `grpc-v2-weather-service` / `grpc-forecast-streaming` specs for status-code assertions during implementation and add a delta if one exists.
- [Specs `pipeline-actor` and `mqtt-actor-topology` change normative text] → delta MODIFIED requirements include full prior content.

## Migration Plan

All messages are in-process; no wire, persistence or proto change, so no versioning or compatibility shim. Ship per phase; each phase leaves the suite green. Rollback: `git revert` per phase. Prerequisite: `restructure-agent-docs` applied (provides the `AGENTS.md` note removed in phase c).

## Open Questions

- Whether `EgressActor` should get its own delta spec for ref vending (no spec covers it today). Deferrable: scenarios are covered by tests and by the `stream-composition` requirement.
