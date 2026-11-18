## ADDED Requirements

### Requirement: Effective interval drifts toward observed change frequency
When a domain detects data changes, the system SHALL track the interval between consecutive changes. If the observed change interval is consistently shorter or longer than the configured interval, the effective poll interval SHALL drift toward the observed value.

#### Scenario: Faster-than-expected updates shorten the interval
- **WHEN** a domain configured at 360m observes 3 consecutive changes at ~180m intervals
- **THEN** the effective interval SHALL decrease toward 180m

#### Scenario: Slower-than-expected updates lengthen the interval
- **WHEN** a domain configured at 60m observes 3 consecutive changes at ~180m intervals
- **THEN** the effective interval SHALL increase toward 180m

### Requirement: Drift is clamped to a safe range
The effective interval SHALL never drift below `configured * 0.5` or above `configured * 2.0`. This prevents runaway drift from destabilizing budget or freshness.

#### Scenario: Drift does not go below half the configured interval
- **WHEN** a domain configured at 60m observes changes every 10m
- **THEN** the effective interval SHALL not go below 30m

#### Scenario: Drift does not go above double the configured interval
- **WHEN** a domain configured at 60m observes changes every 300m
- **THEN** the effective interval SHALL not go above 120m

### Requirement: Drift requires a minimum number of observations
The effective interval SHALL not drift until at least 3 consecutive data changes have been observed with consistent intervals. A single outlier SHALL not cause immediate drift.

#### Scenario: First change does not trigger drift
- **WHEN** a domain observes its first data change
- **THEN** the effective interval remains at the configured value

#### Scenario: Three consistent observations trigger drift
- **WHEN** a domain observes 3 consecutive changes at intervals within 20% of each other
- **THEN** the effective interval begins drifting toward the observed average

### Requirement: Drift resets on configuration change
When the operator changes a domain's configured interval (via restart with new config), the effective interval SHALL reset to the new configured value and drift starts fresh.

#### Scenario: Restart with new interval resets drift
- **WHEN** a domain had drifted its effective interval to 90m and restarts with a configured interval of 120m
- **THEN** the effective interval SHALL be 120m with no accumulated drift

### Requirement: Drift state is not persisted
Drift observations and the effective interval adjustment SHALL be held in memory only. After a container restart, each domain begins with its configured interval and re-learns drift from scratch. This keeps persistence records simple and avoids stale drift surviving long outages.

#### Scenario: Restart clears drift
- **WHEN** a PollWorker recovers from persistence after a container restart
- **THEN** all domains poll at their configured intervals with no drift applied
