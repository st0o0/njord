## 1. Base Class

- [x] 1.1 Create `StreamConsumerActor` in `src/Njord/Actors/StreamConsumerActor.cs` with: state machine (WaitingForRefs/Ready), `_watchedDeps` HashSet, `_lastTerminatedRef` gate, `_retryCount` + exponential backoff, `SharedKillSwitch` lifecycle, `RetryResolve` handler, sealed `HandleTerminated` flow, virtual hooks (`ResolveDependencies`, `AllRefsReady`, `MaterializeGraph`, `ConfigureWaitingForRefs`, `ConfigureReady`, `OnDependencyLost`)
- [x] 1.2 Write `StreamConsumerActorSpec` — test dead-ref detection + backoff retry, stale-response gating (TryTransition blocked while `_lastTerminatedRef` set), KillSwitch shutdown on HandleTerminated, untracked Terminated ignored, exponential backoff capping at 30s and reset on success

## 2. Migrate Actors to Base Class

- [x] 2.1 Migrate `MqttEgressActor` to inherit from `StreamConsumerActor` — move dependency resolution, Watch, Tell, response handling into overrides; wire `killSwitch.Flow<EgressEvent>()` into MaterializeGraph; remove inline HandleTerminated/WaitingForRefs/Ready boilerplate
- [x] 2.2 Migrate `ModelStateActor` to inherit from `StreamConsumerActor` — same pattern as MqttEgressActor
- [x] 2.3 Migrate `EnrichmentActor` to inherit from `StreamConsumerActor` — handle GraphDsl multi-flow case in MaterializeGraph override
- [x] 2.4 Migrate `DiscoveryActor` to inherit from `StreamConsumerActor` — override `ConfigureReady()` for WaitingForCapabilities/Ready handlers, override `OnDependencyLost()` for queue.Complete(), replace `PoisonPill.Instance` in `Sink.ActorRef` onCompleteMessage with a safe custom message

## 3. Inline Fixes

- [x] 3.1 Fix `GrpcSnapshotConsumerActor` inline — add `_watchedDeps` guard, `_lastTerminatedRef` gate on EgressSourceResponse transition, KillSwitch in stream graph, exponential backoff retry on dead ref
- [x] 3.2 Fix `SchedulerActor` inline — add `_watchedDeps` guard, `_lastTerminatedRef` gate on TryTransitionToConnecting, KillSwitch on both materialized graphs (Source.Queue→SinkRef and SourceRef→Sink.ActorRef), exponential backoff retry on dead ref

## 4. Tests

- [x] 4.1 Add dead-letter-flood reproduction test — subscribe to DeadLetter EventStream, terminate a dependency, assert ≤10 dead letters after 500ms wait (covers MqttEgressActor as representative)
- [x] 4.2 Add stale-response rejection test — terminate dependency before graph materializes, verify actor does NOT transition to Ready with stale SinkRef
- [x] 4.3 Update existing re-request-after-termination tests (MqttEgressActorSpec, DiscoveryActorSpec, GrpcSnapshotConsumerTerminatedSpec) — adjust timeouts for 1s+ backoff delay, verify re-request arrives on new probe
- [x] 4.4 Run full test suite 20× to verify zero flaky failures

## 5. Cleanup

- [x] 5.1 Run `dotnet slopwatch` and verify no regressions
