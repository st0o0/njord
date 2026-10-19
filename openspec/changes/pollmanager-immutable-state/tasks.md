## 1. Read current code

- [x] 1.1 Read PollManager.cs, PollCycleTracker.cs, PollManagerSpec.cs to understand all fields, handlers, and test patterns

## 2. Design PollManagerState record

- [x] 2.1 Created `src/Njord.Pipeline/PollManagerState.cs`: PollManagerConfig (targets, targetsById, modelsByLocation) + PollManagerState immutable record with ImmutableDictionary fields, static Create factory
- [x] 2.2 Created PollManagerStateExtensions in same file: ApplyReport, ApplyWorkerTerminated, ApplyRegionResolved, ApplyResolveRetry, ApplyPollingChanged, GetPollStates, GetPollHealth

## 3. Redesign PollCycleTracker as immutable

- [x] 3.1 Rewrote PollCycleTracker as sealed record with ImmutableDictionary<string, OpenCycle>
- [x] 3.2 Record returns (PollCycleTracker, PollCycleSummary?) tuple
- [x] 3.3 Static Empty factory, OpenCycles property

## 4. Refactor PollManager

- [x] 4.1 Extracted config into PollManagerConfig record (computed once in constructor)
- [x] 4.2 Replaced all mutable fields with single _state + _config fields
- [x] 4.3 Refactored each handler to use _state = _state.Apply(msg) + side effects
- [x] 4.4 Build clean

## 5. Add state tests

- [x] 5.1 Created PollManagerStateSpec: 14 pure unit tests for Apply methods, queries, and state transitions (no TestKit)
- [x] 5.2 PollCycleTrackerSpec updated for immutable API (returns tuples instead of mutating)
- [x] 5.3 All 15 existing PollManagerSpec tests pass (actor behavior unchanged)

## 6. Validation

- [x] 6.1 `dotnet build Njord.slnx` — clean, 0 errors, 0 warnings
- [x] 6.2 Pipeline.Tests: 174 tests (160 existing + 14 new state tests), 0 failures on PollManager-related tests. 1 pre-existing GatewayRestartSpec failure from GrpcConsumer redesign (not PollManager related).
- [x] 6.3 Architecture tests: 122 pass
- [x] 6.4 `dotnet format Njord.slnx --verify-no-changes` — clean
- [x] 6.5 Updated AGENTS.md: Pipeline 160→174, total 1697→1711
