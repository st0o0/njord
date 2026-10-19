## ADDED Requirements

### Requirement: PollManager state is a single immutable record
All PollManager runtime state SHALL be held in a single PollManagerState immutable record, updated via Apply extension methods that return a new instance.

#### Scenario: Report updates state
- **WHEN** PollManager receives a PollWorkerReport
- **THEN** the handler SHALL call `_state = _state.Apply(report, now)` and the state record SHALL contain updated worker states, restart attempts, and health timestamp

#### Scenario: State is independently testable
- **WHEN** testing state transition logic
- **THEN** tests SHALL construct a PollManagerState, call Apply with a message, and assert on the returned state — without any actor system

### Requirement: PollCycleTracker is immutable
PollCycleTracker SHALL be an immutable record with Apply methods that return (newTracker, PollCycleSummary?) tuples.

#### Scenario: Recording a poll outcome
- **WHEN** a poll outcome is recorded
- **THEN** PollCycleTracker.RecordOutcome SHALL return a new tracker instance and an optional PollCycleSummary (when the cycle completes)

#### Scenario: Cycle timeout
- **WHEN** a cycle times out
- **THEN** PollCycleTracker.TimeoutStale SHALL return a new tracker instance with stale cycles removed and their summaries emitted

### Requirement: PollManager is a thin message router
PollManager SHALL only contain message handling (Receive), side effects (Tell, metrics, timers), and infrastructure (region resolution, DeathWatch). No state computation logic.

#### Scenario: Handler structure
- **WHEN** PollManager handles a PollWorkerReport
- **THEN** the handler SHALL: (1) `_state = _state.Apply(report)`, (2) emit metrics from the returned summary, (3) schedule restarts if needed

### Requirement: Config state is separate from runtime state
Static configuration (targets, targetsById, modelsByLocation) SHALL be a separate immutable value computed once at construction, not part of the mutable state record.

#### Scenario: Config does not change at runtime
- **WHEN** PollManager starts
- **THEN** config fields (target list, target index, location-model grouping) SHALL be computed once and stored separately from the Apply-able runtime state
