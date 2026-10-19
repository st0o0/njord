## Why

PollManager has 9+ mutable fields with state mutation scattered across 11 handlers. FunkArr's managers (DownloadManager, ScoringManager, HistoryState) use immutable state records with Apply extension methods — state logic is pure, testable without actors, and each handler is a single `_state = _state.Apply(msg)` line plus side effects. PollCycleTracker is also a mutable class that should become immutable.

## What Changes

- Extract PollManagerState immutable record from PollManager's fields
- Extract PollManagerStateExtensions with Apply methods for each event type
- Redesign PollCycleTracker as immutable (Apply-based, returns new instance + optional PollCycleSummary)
- PollManager shrinks to message routing + side effects (Tell, metrics, timers)
- State tests move from TestKit actor specs to pure unit tests on the state record
- Keep all existing behavior and message contracts

## Non-goals

- Not changing PollWorker or its persistence
- Not changing the shard region or message routing
- Not changing any message contracts (QueryPollStates, PollWorkerReport, etc.)
- Not changing PollManager's role as cluster singleton

## Capabilities

### New Capabilities

- `pollmanager-state`: Immutable state record with Apply pattern for PollManager

### Modified Capabilities

_(none — same behavior, cleaner internal structure)_

## Impact

- Njord.Pipeline: PollManager.cs refactored, PollManagerState.cs + PollManagerStateExtensions.cs new, PollCycleTracker.cs rewritten as immutable
- Njord.Pipeline.Tests: PollManagerSpec simplified, new PollManagerStateSpec for pure state tests
- No message contract changes, no behavior changes
