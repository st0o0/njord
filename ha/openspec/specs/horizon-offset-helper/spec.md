## Purpose

Defines shared utility functions for calculating horizon offsets from timestamps and looking up values from horizon-indexed data structures.

## Requirements

### Requirement: Shared horizon offset calculation
The integration SHALL provide a shared utility function that calculates the current horizon offset from a given timestamp. The offset SHALL be `max(0, floor((now_utc - timestamp).total_seconds() / 3600))`.

#### Scenario: Offset after 2.5 hours
- **WHEN** the timestamp is 2.5 hours ago
- **THEN** the offset is 2

#### Scenario: Offset at exactly 0
- **WHEN** the timestamp is less than 1 hour ago
- **THEN** the offset is 0

#### Scenario: Offset never goes negative
- **WHEN** the timestamp is in the future (clock skew)
- **THEN** the offset is 0

### Requirement: Horizon value lookup
The utility SHALL provide a function to look up a value from a list of horizon entries by horizon string (e.g. "h3"), given a computed offset.

#### Scenario: Lookup current horizon
- **WHEN** the offset is 3 and horizons contain entries for "h0" through "h24"
- **THEN** looking up the current horizon returns the "h3" entry

#### Scenario: Lookup beyond available horizons
- **WHEN** the offset is 30 and the last available horizon is "h24"
- **THEN** the lookup returns None

### Requirement: Consensus entity uses shared helper
The `NjordConsensusWeatherEntity` SHALL be refactored to use the shared horizon-offset helper instead of its inline `_current_horizon_offset()` method.

#### Scenario: Consensus behavior unchanged
- **WHEN** the consensus entity calculates its current state after refactoring
- **THEN** results are identical to the previous inline implementation
