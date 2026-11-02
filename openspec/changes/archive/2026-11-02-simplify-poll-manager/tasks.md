## 1. Clean up PollManagerState and extensions

- [x] 1.1 Remove `ResolveAttempts` from `PollManagerState` record and its initializer in `PollManagerState.Create()` (`src/Njord.Polling/PollManagerState.cs`)
- [x] 1.2 Remove `ApplyRegionResolved()` and `ApplyResolveRetry()` from `src/Njord.Polling/PollManagerStateExtensions.cs`
- [x] 1.3 Remove the `ApplyRegionResolved_sets_region_and_resets_attempts` test from `src/Njord.Polling.Tests/PollManagerStateTests.cs`

## 2. Simplify PollManager actor

- [x] 2.1 Remove dead constants `ResolveTimerKey` and `RegionStateTimeout` from `src/Njord.Polling/PollManager.cs`
- [x] 2.2 Remove unused import `Akka.Cluster.Sharding` from `src/Njord.Polling/PollManager.cs`
- [x] 2.3 Add `PreStart()` that iterates `_config.Targets` and sends `StartPolling(target, _state.DiscoveryInterval)` to `_pollRegion`
- [x] 2.4 Fix `OnRestartWorker` to use `_pollRegion` instead of `_state.Region`
- [x] 2.5 Fix `OnTriggerImmediatePoll` to use `_pollRegion` directly and remove the `_state.Region is null` guard (reply with `TriggerImmediatePollCompleted` unconditionally)

## 3. Update tests

- [x] 3.1 Delete `src/Njord.Polling.Tests/PollManagerLateRegionTests.cs`
- [x] 3.2 Simplify `StartedManagerAsync` in `src/Njord.Polling.Tests/PollManagerStartupTests.cs`: expect only `StartPolling` messages (remove `GetShardRegionState` expect and `CurrentShardRegionState` reply), remove unused `System.Collections.Immutable` and `Akka.Cluster.Sharding` imports
- [x] 3.3 Delete `A_running_worker_of_a_removed_target_is_stopped` test from `src/Njord.Polling.Tests/PollManagerStartupTests.cs`
- [x] 3.4 Simplify `StartedManagerAsync` in `src/Njord.Polling.Tests/PollManagerQueryTests.cs`: same pattern as 3.2
- [x] 3.5 Simplify `StartedManagerAsync` in `src/Njord.Polling.Tests/PollManagerTriggerTests.cs`: same pattern as 3.2

## 4. Validation

- [x] 4.1 Run `dotnet build Njord.slnx` from `src/`
- [x] 4.2 Run `dotnet format Njord.slnx --verify-no-changes` from `src/`
- [x] 4.3 Run all test projects from `src/` (each via `dotnet run --project <project>/<project>.csproj`)
