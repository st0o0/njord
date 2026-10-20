## ADDED Requirements

### Requirement: Each domain tracks its own response hash
PollWorker SHALL compute and store a response hash per DomainKind. A hash change indicates the upstream data has updated.

#### Scenario: Weather hash changes, AQ hash unchanged
- **WHEN** a poll cycle fetches Weather and AirQuality, and Weather returns a new hash but AirQuality returns the same hash
- **THEN** only Weather SHALL record a hash change and advance its cycle learning

### Requirement: Each domain independently learns its update cycle
Each domain SHALL transition from Discovery to Steady independently when it measures 2+ consistent hash-change intervals. The learned cycle SHALL be persisted.

#### Scenario: AQ learns hourly cycle
- **WHEN** AirQuality hash changes at t=0, t=60min, t=120min
- **THEN** AirQuality SHALL transition to Steady with a learned cycle of ~60 minutes

#### Scenario: Flood learns 12-hour cycle
- **WHEN** Flood hash changes at t=0 and t=720min
- **THEN** after one more confirmation Flood SHALL transition to Steady with ~720 minute cycle

### Requirement: Only due domains are polled per tick
PollWorker SHALL poll only domains whose NextPollAt has passed. Domains not yet due SHALL be skipped.

#### Scenario: Weather due, Flood not due
- **WHEN** Weather.NextPollAt is in the past and Flood.NextPollAt is 4 hours from now
- **THEN** only a FetchDomain for Weather SHALL be sent, not Flood

### Requirement: Steady domain falls back to Discovery on misses
A domain in Steady phase that sees N consecutive polls with no hash change SHALL fall back to Discovery phase.

#### Scenario: Flood stops updating
- **WHEN** Flood is in Steady with 12h cycle but 5 consecutive polls show no hash change
- **THEN** Flood SHALL transition back to Discovery phase

### Requirement: Learned cycles survive restart
Per-domain phase, learned cycle, hash and miss count SHALL be persisted and recovered on restart.

#### Scenario: Restart preserves learned cycles
- **WHEN** PollWorker restarts after having learned Weather=6h and AQ=1h
- **THEN** the recovered state SHALL resume with those learned cycles without re-discovery
