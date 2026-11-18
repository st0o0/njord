## REMOVED Requirements

### Requirement: State-level tests cover every transition in the learning cycle state machine
**Reason**: The learning cycle (Discovery/Steady/Suspended with cycle matching) is replaced by per-domain configured intervals with adaptive drift. The transition matrix no longer exists.
**Migration**: Replace with tests for the Active/Suspended state model and drift behavior as specified in per-domain-poll-intervals and adaptive-poll-drift.

### Requirement: Actor-level tests verify timing-critical Steady behavior
**Reason**: Steady phase is removed. Timer scheduling is now based on per-domain configured intervals, not learned cycles.
**Migration**: Replace with actor tests that verify per-domain timer scheduling uses the resolved interval (config override > default > global floor) and that drift adjustments are reflected in scheduling.
