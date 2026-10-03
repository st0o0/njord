## Why

`StreamConsumerActor.HandleTerminated` calls `ResolveDependencies()` and re-enters `WaitingForRefs` via `Become`. When the initial resolve cycle had in-flight PipeTo responses (e.g. `MqttSinkResponse` from a dep that just died), those stale responses are still in the mailbox. The re-entered `WaitingForRefs` handlers process them, set ref fields (overwriting the `OnDependencyLost` clearing), and `TryTransition` can succeed with a dead SinkRef — crashing the actor via `MaterializeGraph`. The `_lastTerminatedRef` gate in `TryTransition` only blocks when the dead ref is still in the registry; when the registry already has a replacement, the gate doesn't fire and the stale responses slip through. This causes non-deterministic CI failures (`MqttEgressActorSpec.Should_re_request_refs_after_watched_actor_terminates` — 17/20 failure rate locally).

Root cause confirmed by decompiling `ReceiveActor.Become` (Akka 1.5.71): `Become` works correctly, but it replays the same handler set over a mailbox that still contains responses from the previous resolve cycle. The handlers can't distinguish stale from fresh responses because the messages carry no correlation token.

## What Changes

- Add a `long RequestId` field to all `Request*`/`*Response` message pairs used by `StreamConsumerActor` subclasses. The requesting actor generates a monotonic id; the responding actor echoes it back.
- `StreamConsumerActor` base class exposes `NextRequestId()` (increments a counter, which `HandleTerminated` also increments to invalidate outstanding ids).
- Subclass `*Response` handlers compare `response.RequestId` against the id they sent. Stale responses (wrong id) are silently dropped.
- Remove the `_lastTerminatedRef`-based `TryTransition` gate — the request-id correlation is the correct guard against stale responses. `_lastTerminatedRef` remains solely for `IsDeadRef()` detection in `*Resolved` handlers.
- Fix `TryTransition` to clear `_lastTerminatedRef` when all refs are ready (the original first-PR fix), since the stale-response problem is now handled by correlation.

## Capabilities

### New Capabilities

_None — this is a bug fix to an existing capability._

### Modified Capabilities

- `stream-consumer-actor`: The stale-response gating mechanism changes from `_lastTerminatedRef` check in `TryTransition` to request-id correlation on messages. HandleTerminated flow step ordering changes. New base-class API `NextRequestId()`.

## Impact

- **Messages changed (add `long RequestId`):**
  - `RequestMqttSink` / `MqttSinkResponse`
  - `RequestEgressSource` / `EgressSourceResponse`
  - `RequestEgressSink` / `EgressSinkResponse`
  - `RequestPipelineSource` / `PipelineSourceResponse`
- **Responding actors** (echo `RequestId` through): `MqttConnectionActor`, `EgressActor`, `PipelineActor`
- **StreamConsumerActor subclasses** (send id, check in response handler): `MqttEgressActor`, `DiscoveryActor`, `ModelStateActor`, `EnrichmentActor`, `GrpcSnapshotConsumerActor`
- **Tests:** `StreamConsumerActorSpec`, `MqttEgressActorSpec`, all tests that fake the responding actors need to echo `RequestId` back
- **No API-budget impact** — no change to polling or Open-Meteo requests.

## Non-goals

- Changing `StreamConsumerActor` from `ReceiveActor` to `UntypedActor` — the `Become` mechanics work correctly; the problem is stale messages, not the dispatch model.
- General request/response correlation for all actor messages — only the `StreamConsumerActor` request/response pairs need this.
- Removing `_lastTerminatedRef` entirely — it still serves the dead-ref detection purpose in `IsDeadRef()`.
