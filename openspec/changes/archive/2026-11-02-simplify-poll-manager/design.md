## Context

PollManager is a cluster singleton that starts one PollWorker per configured location. It currently resolves the poll shard region through a self-message loop with backoff retry and tracks the resolved reference in its immutable state. It also queries GetShardRegionState after startup to stop workers for locations that were removed from config. Both mechanisms add complexity without providing value: the BackoffSupervisor already handles startup failures, and options are startup-only (a config change requires a pod restart).

The working tree already contains partial changes (region field moved to a readonly, resolve messages removed) but has loose ends: dead state fields, broken references to _state.Region, missing PreStart, and untouched tests.

## Goals / Non-Goals

**Goals:**
- PollManager resolves the poll region once at construction, as a readonly field
- Workers are started in PreStart directly, no self-tell indirection
- All region-resolution state (Region, ResolveAttempts, ApplyRegionResolved, ApplyResolveRetry) is removed
- All removed-worker cleanup code (GetShardRegionState, OnRegionState, StopPolling for orphans) is removed
- Tests are updated to match the simplified startup sequence

**Non-Goals:**
- Changing worker restart logic (RestartWorker, DeathWatch, BackoffPolicy)
- Changing PollWorker behavior, discovery, or cycle learning
- Altering any observable runtime behavior (poll timing, budget, metrics)

## Decisions

### Direct field resolution instead of async resolve loop

The poll region reference becomes `private readonly IActorRef _pollRegion = Context.GetActor<IPollRegion>()`. If the region is not registered, construction throws and BackoffSupervisor restarts the actor — identical end result to the old retry loop, but the retry logic is in the supervisor where it belongs.

Alternative: keep an async resolve with a single retry. Rejected because BackoffSupervisor already provides this with better visibility (supervision events).

### No removed-worker cleanup

The old code queried GetShardRegionState to find entities not in the current config and sent StopPolling. Since options are IOptions<T> (startup-only, no hot reload), the only way a location disappears is a config change + pod restart. On restart, the shard region is fresh and only the configured locations get StartPolling. Orphaned entities from a previous incarnation passivate naturally (PollWorker has no passivation timeout, but a fresh shard region has no leftover entities either).

### PreStart starts workers immediately

With the region available as a field, PreStart iterates _config.Targets and sends StartPolling directly. No ResolveRegion self-message, no timer, no intermediate state.

### OnRestartWorker and OnTriggerImmediatePoll use _pollRegion directly

These handlers referenced _state.Region (nullable). They now use _pollRegion (always set, readonly). The "region not available" guard in OnTriggerImmediatePoll is removed — if the actor is alive, the region is resolved.

## Risks / Trade-offs

- [Risk: Actor starts before region is registered] The BackoffSupervisor restarts the actor after a crash. If the region registration is slow, the actor may crash and restart a few times. This matches the existing behavior of all other actors (ForecastPipeline, GrpcConsumer) and is the project's standard pattern per the actor-resolve-failure-handling spec.
- [Risk: Late-region test removal loses coverage] The PollManagerLateRegionTests tested a scenario that no longer exists. The standard resolve-failure behavior is covered by the actor-resolve-failure-handling spec and the BackoffSupervisor, not by PollManager-specific tests.
