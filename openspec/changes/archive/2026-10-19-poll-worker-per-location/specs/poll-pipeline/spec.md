## MODIFIED Requirements

### Requirement: PollManager target set is locations only
PollManager SHALL derive its target set from the configured locations list (not locations × models). StartPolling SHALL be sent to one PollWorker per location.

#### Scenario: Two locations configured
- **WHEN** the configuration has locations "Lucerne" and "Zurich"
- **THEN** PollManager SHALL start exactly 2 PollWorkers
