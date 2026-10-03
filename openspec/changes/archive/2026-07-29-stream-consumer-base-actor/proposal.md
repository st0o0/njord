## Why

Six actors (MqttEgressActor, DiscoveryActor, EnrichmentActor, ModelStateActor, GrpcSnapshotConsumerActor, SchedulerActor) copy-paste the same "resolve upstream dependencies → watch → materialize stream graph → handle terminated" pattern. The pattern has three interconnected bugs that produce 22,000+ dead letters in 500ms, cause premature transitions to Ready with broken StreamRefs, and kill actors when their StreamSupervisor child terminates from old graph cleanup. Fixing each actor inline would mean six copies of the same fix logic, making the next bug in this pattern equally expensive.

## What Changes

- Introduce a `StreamConsumerActor` base class that encapsulates the dependency-resolution lifecycle, dead-ref retry with exponential backoff, stale-response gating, and stream graph lifecycle via `SharedKillSwitch`.
- Migrate MqttEgressActor, DiscoveryActor, EnrichmentActor, and ModelStateActor to inherit from `StreamConsumerActor`.
- Fix GrpcSnapshotConsumerActor and SchedulerActor inline with the same mechanisms (different base classes prevent inheritance).
- Add reproduction tests for the dead-letter flood, stale-response premature-Ready, and KillSwitch-based graph teardown.

## Non-goals

- Changing the actor registration strategy (BackoffSupervisor vs. direct registration) — that is a separate concern.
- Altering stream graph shapes or business logic within any actor.
- Changing `Context.Materializer()` to system-level materializer — the base class keeps actor-scoped materializer and solves child-death via KillSwitch instead.
- API-budget impact: zero — no polling changes.

## Capabilities

### New Capabilities

- `stream-consumer-actor`: Base class contract — dependency tracking, dead-ref detection with exponential backoff retry, stale-response gating via `_lastTerminatedRef`, `SharedKillSwitch` lifecycle for clean graph teardown, and the `WaitingForRefs → Ready` state machine managed by the base.

### Modified Capabilities

- `mqtt-actor-topology`: MqttEgressActor and DiscoveryActor migrate to `StreamConsumerActor` base; HandleTerminated behavior changes (KillSwitch shutdown, backoff retry instead of immediate re-resolve, stale-response gating).
- `enrichment-actor`: EnrichmentActor migrates to `StreamConsumerActor` base; same HandleTerminated behavior changes.
- `egress-stream-graph`: ModelStateActor migrates to `StreamConsumerActor` base; same HandleTerminated behavior changes.
- `snapshot-actors`: GrpcSnapshotConsumerActor inline fix — adds `_watchedDeps` guard, `_lastTerminatedRef` gate, KillSwitch, and backoff retry.
- `poll-scheduler`: SchedulerActor inline fix — adds `_watchedDeps` guard, `_lastTerminatedRef` gate, KillSwitch on both materialized graphs, and backoff retry.

## Impact

- **New file**: `src/Njord/Actors/StreamConsumerActor.cs` (base class).
- **Modified actors** (inherit from new base): `MqttEgressActor`, `DiscoveryActor`, `EnrichmentActor`, `ModelStateActor`.
- **Modified actors** (inline fix): `GrpcSnapshotConsumerActor`, `SchedulerActor`.
- **Test files**: New spec classes for dead-letter flood, stale-response, and KillSwitch teardown. Existing re-request-after-termination tests adjusted for backoff delay timing.
- **Dependencies**: No new NuGet packages — `SharedKillSwitch` is in `Akka.Streams` (already referenced).
