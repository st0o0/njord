## ADDED Requirements

### Requirement: Transient runtime state stays on the actor
For actors with transient runtime state (actor refs, Become phase, retry counters, pending timers), those fields SHALL remain as mutable fields on the actor class. Only persistent domain state moves to the state record.

#### Scenario: PollScheduler transient fields
- **WHEN** `PollScheduler` is inspected
- **THEN** the gateway ref, the retry count, the pending poll timers and the per-location poll-cycle trackers are mutable actor fields, while the poll-state dictionary lives in `PollSchedulerState`

## MODIFIED Requirements

### Requirement: Persistence delete-failures are logged
Persistent actors that call `DeleteMessages`/`DeleteSnapshots` as part of their snapshot
cleanup SHALL register handlers for `DeleteMessagesFailure` and `DeleteSnapshotsFailure`
that log a warning, in addition to the existing `DeleteMessagesSuccess`/
`DeleteSnapshotSuccess` handlers.

#### Scenario: Journal cleanup failure is logged, not dropped
- **WHEN** a `DeleteMessagesFailure` or `DeleteSnapshotsFailure` message is received by
  `BudgetTracker`, or by `PollScheduler` in its ready behavior
- **THEN** the actor logs a warning including the failure cause instead of letting the
  message reach dead letters unhandled

## REMOVED Requirements

### Requirement: Transient state stays on actor
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Transient runtime state stays on the actor" in this capability.
