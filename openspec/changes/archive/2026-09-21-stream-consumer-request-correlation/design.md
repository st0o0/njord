## Context

`StreamConsumerActor` manages a two-phase lifecycle (WaitingForRefs → Ready) with recovery on `Terminated`. Five production subclasses use it. Recovery calls `Become(WaitingForRefs)` to replay the state machine, but stale PipeTo responses from the pre-termination cycle remain in the mailbox. The new handlers process them as fresh, leading to `MaterializeGraph` with dead StreamRefs and an actor crash.

The existing `_lastTerminatedRef` gate in `TryTransition` was designed to block stale responses, but it fails when the registry already holds a replacement ref (the gate only fires when the dead ref is re-resolved from the registry, not when stale responses set ref fields).

Current request/response messages are parameterless (`sealed record RequestMqttSink;`). There is no way for a response handler to know which resolve cycle the response belongs to.

## Goals / Non-Goals

**Goals:**
- Eliminate stale-response actor crashes after `HandleTerminated` — zero flaky CI failures from this class of bug
- Clean architectural fix using message-level correlation, not state flags or timing tricks
- Maintain the existing `ReceiveActor` + `Become` state machine — it works; the problem is message identity

**Non-Goals:**
- Rewriting `StreamConsumerActor` as `UntypedActor`
- Adding correlation to messages outside the `StreamConsumerActor` request/response pairs
- Changing the `HandleTerminated` flow order (steps 1–8 in the spec remain intact)

## Decisions

### Decision 1: Monotonic `long` request id, not `Guid`

The base class maintains a `private long _requestId` counter. `NextRequestId()` returns `++_requestId`. `HandleTerminated` also increments `_requestId`, which instantly invalidates all in-flight request ids.

**Why not Guid:** Guid is heavier (16 bytes vs 8) and doesn't support the "invalidate all" increment pattern. The counter never needs to be globally unique — it's actor-local.

**Alternatives considered:**
- Epoch-only (closure-captured counter in handlers): fails because `Become` re-registers handlers with the new epoch value, so stale responses pass the check. The problem is that the counter lives in the handler closure, not in the message.
- `object` identity token: same closure problem.

### Decision 2: RequestId on messages, not sender-checking

Each `Request*` message carries the `long RequestId`. Each `*Response` echoes it back. The response handler compares `response.RequestId` against the id it stored when sending.

**Why not sender-checking (`_watchedDeps.Contains(Sender)`):** The responding actor (e.g. EgressActor) may still be alive and tracked — its OLD SourceRef is invalid (the stream it fed was killed), but `Sender` would pass the tracked-dep check. RequestId correlates the specific request/response pair, not just the actor.

### Decision 3: Subclass stores sent request ids in private fields

Each subclass stores the request id it sent per dependency type:

```
private long _mqttSinkRequestId;
private long _egressSourceRequestId;
```

The `*Resolved` handler sends the request and stores the id. The `*Response` handler checks `response.RequestId == _mqttSinkRequestId`. This is explicit, debuggable, and requires no base-class magic.

**Alternative considered:** Base class tracks all sent ids automatically — too much abstraction for 4 message pairs across 5 subclasses.

### Decision 4: TryTransition drops the `_lastTerminatedRef` gate

With request-id correlation guarding against stale responses, `TryTransition` no longer needs the `_lastTerminatedRef is not null` check. It becomes:

```csharp
if (!AllRefsReady()) return;
_lastTerminatedRef = null;
_retryCount = 0;
MaterializeGraph(_killSwitch);
EnterReady();
Stash.UnstashAll();
```

`_lastTerminatedRef` remains for `IsDeadRef()` in `*Resolved` handlers (detecting the registry returning the same dead ref).

### Decision 5: HandleTerminated keeps calling ResolveDependencies + Become

The flow stays as specified (steps 1–8). `Become(WaitingForRefsBehavior)` works correctly — the new handlers ARE installed. Stale responses ARE processed by the new handlers, but now they're silently dropped because their `RequestId` doesn't match the subclass's stored id (which was updated by the new `ResolveDependencies` call).

### Decision 6: GrpcSnapshotConsumerActor's chained pattern

`GrpcSnapshotConsumerActor` chains: `EgressSourceResponse` → `GetActorAsync` → `SnapshotActorsResolved` → `TryTransition`. The `EgressSourceResponse` handler gets the `RequestId` check. `SnapshotActorsResolved` is a local self-message triggered by the (validated) response, so it doesn't need its own RequestId. It does still need a staleness check — the subclass stores a flag or the request id and checks it in the `SnapshotActorsResolved` handler.

### Decision 7: Remove epoch API from base class

The `CurrentEpoch`, `IsStaleEpoch`, and `_resolveEpoch` field added during debugging are removed. `NextRequestId()` replaces them. The epoch approach was fundamentally flawed (closure capture problem); keeping it would be confusing.

## Risks / Trade-offs

**[Risk] Responding actors must echo RequestId** → Low risk. The change is mechanical: add `msg.RequestId` to the response constructor in PipeTo. 3 actors, 4 handlers. The compiler enforces it (record constructor arity changes).

**[Risk] Test fakes must echo RequestId** → Low risk. Test fakes (`MqttMessageProbe`, `FakeEgressSourceProvider`, etc.) need the same mechanical change. Compiler enforces.

**[Risk] Message size increases by 8 bytes per request/response] → Negligible. These are actor-local messages, not serialized over the wire.

**[Trade-off] Subclass boilerplate increases slightly** → Each subclass adds 1–2 `long` fields and a comparison in each response handler. This is explicit and debuggable — worth it vs a fragile implicit mechanism.

## Open Questions

None — the design is fully constrained by the investigation.
