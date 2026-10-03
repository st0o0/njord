## Context

Two production bugs in the njord gRPC interface:

1. **SchedulerActor dead on startup.** `NjordActorSystemSetup.WithResolvableActors` registers SchedulerActor (3rd) before PipelineActor (4th). Each `Register<T>` call creates and starts the actor sequentially — constructor + `PreStart` run before the next actor is registered. SchedulerActor's `PreStart` calls `GetActor<PipelineActor>()`, which throws because PipelineActor isn't registered yet. After max restarts, the actor stops permanently. Every `GetStatus` gRPC call then hits dead letters and times out after 5s.

2. **StreamConfig stream closes every ~7-17 minutes.** The `StreamConfig` RPC sends an initial config then awaits a `TaskCompletionSource` indefinitely. No data is written to the stream unless config changes. Kestrel's default `MinResponseDataRate` (240 bytes/sec over 5s grace) eventually closes the idle response stream. The ha-njord client sees a clean close and does a full re-setup cycle (~15 calls).

Current test setup masks Problem 1: tests register a fake PipelineActor via `WithActors` (which runs before `WithResolvableActors`), so the ref is always available when SchedulerActor starts.

## Goals / Non-Goals

**Goals:**
- SchedulerActor survives startup regardless of actor registration order
- GetPollStates responds immediately in every actor state (already fixed in code, spec needs alignment)
- StreamConfig streams stay open indefinitely until client disconnect or config change
- Tests reproduce the production registration order

**Non-Goals:**
- Changing actor registration order or topology
- Application-level keepalive pings on gRPC streams
- Restructuring the SchedulerActor ↔ PipelineActor relationship

## Decisions

### Decision 1: Async actor resolution via GetActorAsync + PipeTo

**Choice:** Replace `GetActor<PipelineActor>()` with `GetActorAsync<PipelineActor>().PipeTo(Self)` in `PreStart`.

**Alternatives considered:**
- *Swap registration order:* Moves the problem to PipelineActor (which also needs SchedulerActor via `GetActor` in `MaterializePipeline`). Same circular dependency, different victim.
- *Two-phase registration:* Register one actor via `WithActors`, the other via `WithResolvableActors`. Fragile — leaks startup ordering into the setup class.
- *Message-based handshake:* Have PipelineActor notify SchedulerActor proactively. Larger refactor, changes the ownership model.

**Why GetActorAsync:** It's the Servus-idiomatic solution — waits for the actor to appear in the registry, returns a `Task<IActorRef>` that can be `PipeTo`'d. No structural changes needed. The actor simply adds a `WaitingForPipeline` state before `WaitingForRefs`.

### Decision 2: New WaitingForPipeline state

The SchedulerActor gains a new initial state `WaitingForPipeline` that:
- Handles `PipelineResolved(IActorRef)` → watches it, sends requests, transitions to `WaitingForRefs`
- Handles `GetPollStates` → responds immediately with current (empty) state
- Stashes everything else

State machine after the change:

```
PreStart
  │  GetActorAsync<PipelineActor>().PipeTo(Self)
  ▼
WaitingForPipeline ──PipelineResolved──▶ WaitingForRefs
  │ GetPollStates → respond                │ PipelineSinkResponse
  │ everything else → stash                │ PipelineSourceResponse
  │                                        ▼
                                       Connecting ──ScheduledPoll+offer──▶ WaitingForConnection
                                                                            │ ConnectionEstablished
                                                                            ▼
                                                                          Ready
```

### Decision 3: OnTerminated also uses async resolution

When PipelineActor terminates, `OnTerminated` currently calls `GetActor<PipelineActor>()` which returns the stale (dead) ref from the registry. This creates a watch → Terminated → watch loop on the dead ref. Fix: use `GetActorAsync` + `PipeTo` and transition to `WaitingForPipeline`, which naturally handles the wait.

### Decision 4: Disable MinResponseDataRate on gRPC port

**Choice:** Set `MinResponseDataRate = null` on the gRPC `ListenOptions`, keeping the HTTP/1.1 port at defaults.

**Alternatives considered:**
- *Global null:* Simpler but removes the rate check from health endpoints unnecessarily.
- *Application-level keepalive:* Sending periodic no-op writes on the stream. Works but couples the server to a timer, and gRPC already has HTTP/2 PING frames for connection-level keepalive.

**Why per-port:** Surgical — only affects the gRPC endpoint where long-lived streams are expected. HTTP/1.1 health endpoints keep their default protection.

## Risks / Trade-offs

- **[Risk] GetActorAsync hangs if PipelineActor never registers** → Mitigation: PipelineActor is always registered in the same `WithResolvableActors` block; if it fails to register, the entire system is broken and health checks will report unhealthy. No additional timeout needed.
- **[Risk] Disabling MinResponseDataRate could mask a truly stalled response** → Mitigation: Only disabled on gRPC port; gRPC clients have their own deadlines and keepalive mechanisms.
- **[Risk] New WaitingForPipeline state adds complexity** → Mitigation: Minimal — one new message type, one new `Become` call, same pattern as existing WaitingForRefs.

## Open Questions

None — both fixes are well-scoped with clear root causes.
