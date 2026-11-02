## Why

The PollManager carried a complex region-resolution retry loop (ResolveRegion message, backoff timer, state tracking) and a removed-worker cleanup phase (GetShardRegionState query, StopPolling for orphaned entities). Both are unnecessary: the poll region is always registered before the PollManager starts (BackoffSupervisor guarantees retry on missing keys), and options are startup-only so removed locations require a pod restart anyway. This complexity makes the actor harder to understand and test.

## What Changes

- Remove the region-resolution retry loop (ResolveRegion, RegionStateReceived, RegionStateFailed messages, OnResolveRegion handler, resolve timer, ResolveAttempts state field)
- Remove the removed-worker cleanup (GetShardRegionState query, OnRegionState handler, RegionStateTimeout constant)
- Remove the Region field from PollManagerState; the poll region becomes a readonly field resolved directly via Context.GetActor<IPollRegion>() in the actor's field initializer
- Start workers directly in PreStart via _pollRegion.Tell(StartPolling) instead of through a self-tell resolve chain
- Remove dead state extensions (ApplyRegionResolved, ApplyResolveRetry)
- Fix OnRestartWorker and OnTriggerImmediatePoll to use the _pollRegion field instead of _state.Region
- Remove the "region not available" guard in OnTriggerImmediatePoll (the region is always available)
- Delete PollManagerLateRegionTests (the late-region scenario no longer exists)
- Simplify test helpers: remove GetShardRegionState handshake from StartedManagerAsync
- Delete the removed-target test (A_running_worker_of_a_removed_target_is_stopped)
- Delete the ApplyRegionResolved state test

## Non-goals

- Changing how PollWorker discovery or cycle learning works
- Changing the restart-on-termination logic (RestartWorker, BackoffPolicy, DeathWatch)
- Altering polling intervals, budget accounting, or any runtime behavior
- No API-budget impact: this is a pure internal simplification with no change to polling frequency or domain requests

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `actor-resolve-failure-handling`: PollManager no longer has its own resolve-retry loop; it follows the standard pattern of synchronous Context.GetActor<T>() at construction, relying on BackoffSupervisor for restart on missing keys

## Impact

- `src/Njord.Polling/PollManager.cs` — simplified actor, ~40 fewer lines
- `src/Njord.Polling/PollManagerState.cs` — Region and ResolveAttempts fields removed
- `src/Njord.Polling/PollManagerStateExtensions.cs` — ApplyRegionResolved and ApplyResolveRetry removed
- `src/Njord.Polling.Tests/PollManagerLateRegionTests.cs` — deleted
- `src/Njord.Polling.Tests/PollManagerStartupTests.cs` — simplified, one test removed
- `src/Njord.Polling.Tests/PollManagerQueryTests.cs` — simplified
- `src/Njord.Polling.Tests/PollManagerTriggerTests.cs` — simplified
- `src/Njord.Polling.Tests/PollManagerStateTests.cs` — one test removed
