## Context

Six actors share a dependency-resolution lifecycle pattern: resolve upstream actors via `GetActorAsync`, watch them, request StreamRefs, materialize a stream graph, and re-resolve on `Terminated`. The pattern has three bugs:

1. **Dead-letter flood**: `HandleTerminated` calls `ResolveUpstream` immediately. `GetActorAsync` returns the dead ref from the `ActorRegistry` (which never invalidates entries). `Watch(deadRef)` delivers `Terminated` immediately → tight loop producing 22,000+ dead letters in 500ms.
2. **Stale in-flight responses**: After `HandleTerminated` clears refs and re-resolves, old PipeTo responses (from before Terminated) can arrive and set refs. If both refs end up set — one stale, one fresh — `TryTransition` fires, materializing a graph with a broken StreamRef. The scheduled `RetryResolve` then arrives in `Ready` which doesn't handle it → lost.
3. **StreamSupervisor child death**: `Context.Materializer()` creates a `StreamSupervisor` child actor. When the old stream graph fails asynchronously (broken SinkRef/SourceRef), the StreamSupervisor child terminates. Akka.NET's `HandleChildTerminated` → `FinishTerminate` kills the parent actor if it's already in a terminating state, or an unhandled child `Terminated` triggers the DeathPact.

Four of the six actors (MqttEgressActor, DiscoveryActor, EnrichmentActor, ModelStateActor) are `ReceiveActor` subclasses with a nearly identical two-state FSM (WaitingForRefs → Ready). These are candidates for a shared base class. The remaining two (GrpcSnapshotConsumerActor with sequential 3-phase resolution, SchedulerActor as a `ReceivePersistentActor` with 5 states) get the same fixes applied inline.

## Goals / Non-Goals

**Goals:**
- Eliminate the dead-letter flood via dead-ref detection + exponential backoff retry.
- Prevent premature Ready transitions via `_lastTerminatedRef` gate in `TryTransition`.
- Clean teardown of old stream graphs via `SharedKillSwitch` to prevent StreamSupervisor child death from killing the parent actor.
- Consolidate the fix into a `StreamConsumerActor` base class so the pattern is correct in one place.

**Non-Goals:**
- Changing actor registration strategy (BackoffSupervisor vs. direct).
- Changing `Context.Materializer()` to system-level materializer.
- Altering stream graph shapes or enrichment business logic.
- Introducing a base class for `SchedulerActor` or `GrpcSnapshotConsumerActor`.

## Decisions

### Decision: SharedKillSwitch for stream graph lifecycle

**Choice:** Wire a `SharedKillSwitch` into every stream graph. On `HandleTerminated`, call `_killSwitch.Shutdown()` before re-resolving.

**Alternatives considered:**
- *System-level materializer* (`Context.System.Materializer()`): Avoids the child-death problem but changes stream lifecycle semantics — streams outlive the actor, defeating the purpose of actor-scoped cleanup.
- *New materializer per cycle*: Creating a fresh `Context.Materializer()` on each re-resolve would work but leaks the old materializer's StreamSupervisor (no explicit dispose). KillSwitch is explicit and idiomatic Akka.Streams.

**Rationale:** KillSwitch is the Akka.Streams-native mechanism for graceful stream termination. It leaves `Context.Materializer()` untouched (actor-scoped lifecycle preserved) while giving explicit control over when old graphs terminate.

### Decision: _watchedDeps HashSet to filter Terminated

**Choice:** Track explicitly watched dependency refs in `HashSet<IActorRef> _watchedDeps`. In `HandleTerminated`, ignore any `Terminated` not in the set.

**Rationale:** The actor receives `Terminated` for both explicitly watched upstream dependencies AND for auto-watched children (StreamSupervisor). Without filtering, a StreamSupervisor child death triggers HandleTerminated which clears refs and re-resolves — wrong behavior. The HashSet distinguishes "my dependency died" from "my stream infrastructure died".

### Decision: _lastTerminatedRef gate in TryTransition

**Choice:** Set `_lastTerminatedRef = msg.ActorRef` in `HandleTerminated`. Guard `TryTransition` with `_lastTerminatedRef is not null` → block. Clear in `RetryResolve` handler before re-resolving.

**Alternatives considered:**
- *Generation counter on PipeTo closures*: More explicit (stale responses discarded immediately) but requires a generation field in every Resolved message type and captured closures. Adds ceremony for the same effect.
- *Request-pending flags per dependency*: Doesn't scale — each new dependency needs another boolean.

**Rationale:** The gate blocks premature Ready transitions from stale responses without extra message types. Stale responses set refs but can't trigger MaterializeGraph. When the retry brings fresh responses, they overwrite the stale values. Simpler than generation counting, same outcome.

### Decision: Exponential backoff for dead-ref retry

**Choice:** `delay = min(1s × 2^retryCount, 30s)`. Reset `retryCount` on successful `TryTransition`.

**Rationale:** A fixed 1s delay works for the common case (registry updated quickly after supervisor restart). Exponential backoff prevents CPU spin if the dependency is permanently gone. The 30s cap keeps recovery responsive. Reset on success ensures the next failure starts at 1s again.

### Decision: Base class for 4 actors, inline fix for 2

**Choice:** `StreamConsumerActor : ReceiveActor, IWithStash` for the four two-state actors. GrpcSnapshotConsumerActor and SchedulerActor get the same mechanisms applied inline.

**Alternatives considered:**
- *Interface + extension methods (composition)*: More flexible but can't encapsulate the behavior state machine (WaitingForRefs/Ready) or the Stash.
- *Force all 6 into the base*: SchedulerActor inherits from `ReceivePersistentActor` (incompatible base), GrpcSnapshotConsumer has a 3-phase resolution that doesn't fit the parallel-resolve pattern without awkward workarounds.

**Rationale:** The four vanilla actors are nearly identical in structure. The base eliminates ~60 lines of boilerplate per actor and ensures the fix is correct in one place. The two outliers are different enough that forcing them into the hierarchy would add more complexity than it saves.

### Decision: Subclass registers handlers via virtual methods

**Choice:** The subclass implements `ConfigureWaitingForRefs()` and optionally `ConfigureReady()` to register its `Receive<>` handlers. The base calls these during `Become(WaitingForRefs)` / `Become(Ready)` and adds its own handlers (RetryResolve, Terminated, ReceiveAny stash).

**Rationale:** This keeps the subclass in control of its typed messages (EgressResolved, MqttSinkResponse, etc.) while the base owns the lifecycle messages. The subclass calls `TrackDependency()`, `IsDeadRef()`, `TryTransition()` from its handlers — explicit, no magic.

## Risks / Trade-offs

- **[Stale SinkRef/SourceRef stored briefly]** → Stale StreamRef values sit in the actor's fields until fresh responses overwrite them. They're never used because `TryTransition` is gated. Acceptable: no functional impact, and the fresh response always overwrites.
- **[KillSwitch adds a stage to every graph]** → Minimal overhead (passthrough stage). The alternative (letting old graphs die uncontrolled) is worse.
- **[DiscoveryActor's Sink.ActorRef PoisonPill]** → DiscoveryActor uses `Sink.ActorRef(self, PoisonPill.Instance)` for the capability stream. When the old graph is KillSwitch-shutdown, the stream completes and sends PoisonPill to Self. The base's `HandleTerminated` must shut down the KillSwitch BEFORE the old graph's completion message arrives. Since KillSwitch.Shutdown() is synchronous and the PoisonPill delivery is async (mailbox), ordering is safe. But the DiscoveryActor should migrate away from `PoisonPill.Instance` as the `onCompleteMessage` — a custom `StreamCompleted` message is safer. This is scoped into the migration task.
- **[Backoff delay slows re-request tests]** → Existing tests use 3-5s timeouts. The initial 1s backoff delay fits within those windows. Tests that relied on immediate re-resolve (the tight loop) need their assertions adjusted for the delay.
